"""codex-art-gen MCP server (业务解耦后)。

公开 3 个工具:
  1. dispatch_l1(batches[], concurrency?)    — 跑 L1 独立大图批次
  2. dispatch_l2(sheets[], concurrency?)     — 跑 L2 合图 + 本地 chroma_key + image_cut
  3. write_record(record_path, results)      — 把 dispatch 结果追加到 markdown 表格

MCP 本身**完全无业务**:
- 不读 prompts.md (业务在 tools/codex-art-gen-helper/)
- 不假设 REPO_ROOT 位置（所有路径必须绝对路径)
- 不带任何风格指南字符串

调用方负责把 envelope 展开好直传过来。
"""
from __future__ import annotations

import asyncio
import json
import os
from pathlib import Path

from mcp.server import Server
from mcp.server.stdio import stdio_server
from mcp.types import TextContent, Tool

from codex_runner import run_codex_exec
from sheet_cutter import cut_sheet, remove_chroma_key, rename_by_layout_order

DEFAULT_CONCURRENCY = 2

server = Server("codex-art-gen")


# ─── Prompt 模板（对齐 Codex 官方推荐输入格式） ────────────────────────
# 参考:
#   codex_input_template_L1_BATCH.md
#   codex_input_template_L2_SHEET.md

L1_PROMPT_TPL = """你是图片生成执行器。

任务：
读取下面的 batch JSON,在同一个 Codex session 内批量生成所有 items。

硬性规则：
- 不要读取项目文档。
- 不要解释、不要计划、不要询问。
- 对 items 中的每个条目生成一张独立图片。
- 每个 item 最多调用 1 次 image_gen。
- 可以并行处理多个 item;即使内部不能并行,也必须在同一个 Codex session 内完成整批。
- 单个 item 失败时跳过该项继续处理后续 item,不要自行重试。
- **每张图片必须真实落盘到 item.file 指定的绝对路径**(image_gen 默认会输出到 .codex/generated_images/...,你必须用 exec 命令把它复制/移动到 item.file 的目标路径)。
- 任务由调用方通过**磁盘文件是否存在 + 体积 > 1KB**来判定每张图是否成功,不依赖你的文字回复。

背景规则（按 item.transparent 分流）：
- 若 item.transparent=true:背景必须为纯色 chroma_key 底色(见 batch JSON 的 chroma_key 字段,默认 #00ff00),完全填满主体之外的所有空间(image_gen 的 transparent 在 gpt-image-2 上不可靠,本地会用 chroma-key 工具去键色)。主体本身**禁用**接近 chroma_key 的色调,避免被键出。不要渐变 / 阴影投射到键色底上。
- 若 item.transparent=false:背景按 item.prompt 自身要求绘制(常见用于场景纹理 / 背景图)。

通用规则：
- 不要文字、数字、水印、签名、边框、标签。
- 美术风格、配色由各 item.prompt 自带,本模板不规定。

batch JSON:
{batch_json}
"""

L2_PROMPT_TPL = """你是图片生成执行器。

任务：
读取下面的 sheet JSON,只调用 1 次 image_gen,生成 1 张包含多个小素材的 PNG 画布。

硬性规则：
- 只调用 1 次 image_gen。
- 不要读取项目文档。
- 不要解释、不要计划、不要询问。
- 不要自行重试。
- 不要切图。
- 不要生成多个文件。
- **画布必须真实落盘到 sheet.canvas 指定的绝对路径**(image_gen 默认会输出到 .codex/generated_images/...,你必须用 exec 命令把它复制/移动到 sheet.canvas 的目标路径)。
- 任务由调用方通过**磁盘上 sheet.canvas 文件是否存在 + 体积 > 1KB**来判定是否成功,不依赖你的文字回复。

画布规则：
- 画布尺寸使用 size。
- 背景必须为纯色 chroma_key 底色(默认 #00ff00, 见 sheet JSON 的 chroma_key 字段), 完全填满素材之外的所有空间(image_gen 的 transparent 在 gpt-image-2 上不可靠, 本地会用 chroma-key 工具去键色)。
- 素材本身**禁用**接近 chroma_key 的色调, 避免被键出。
- 按 grid_rows x grid_cols 网格排列。
- 每个素材必须独立, 不要相互接触。
- 每个素材位于自己的格子中央。
- 素材之间至少 32px 纯键色 padding(不要渐变 / 阴影投射到键色底上)。
- 不要文字、数字、水印、签名、边框、标签。
- 按从左到右、从上到下排列, 顺序必须与 items 一致。
- 美术风格、配色由各 item.prompt 自带, 本模板不规定。

sheet JSON:
{sheet_json}
"""


# ─── Tool: dispatch_l1 ─────────────────────────────────────────────

async def _run_one_l1_batch(batch: dict, sem: asyncio.Semaphore) -> dict:
    """单 batch:组装 prompt → codex exec → 逐张 chroma_key(transparent=true 时)→ 本地校验落盘。"""
    async with sem:
        batch_id = batch["batch_id"]
        items = batch["items"]
        writable_roots = batch.get("writable_roots", [])
        chroma_key = batch.get("chroma_key", "#00ff00")

        envelope = {
            "mode": "L1_BATCH",
            "batch_id": batch_id,
            "chroma_key": chroma_key,
            "items": items,
        }
        batch_json_str = json.dumps(envelope, ensure_ascii=False, indent=2)
        prompt = L1_PROMPT_TPL.format(batch_json=batch_json_str)

        # 为日志和 result.json 选个临时位置(放在第一个 writable_root 下)
        log_dir = Path(writable_roots[0]) if writable_roots else Path.cwd()
        result_path = log_dir / f".{batch_id}.result.json"
        log_path = log_dir / f".{batch_id}.log"
        for w in writable_roots:
            Path(w).mkdir(parents=True, exist_ok=True)

        codex_result = await run_codex_exec(
            prompt=prompt,
            result_json_path=result_path,
            writable_roots=writable_roots,
            log_path=log_path,
            model="gpt-5.5",
        )
        if not codex_result["success"]:
            return {
                "batch_id": batch_id,
                "ok": 0,
                "failed": len(items),
                "error": codex_result["error"],
                "items": [
                    {"index": it["index"], "file": it["file"], "status": "failed", "error": codex_result["error"]}
                    for it in items
                ],
            }

        # 纯磁盘核验:遍历原始 items,不信 codex 文字回复(防它复读 prompt 模板假装出图)
        ok, failed = 0, 0
        result_items: list[dict] = []
        for it in items:
            file_abs = Path(it["file"])
            entry: dict = {"index": it["index"], "file": str(file_abs)}
            if not file_abs.exists() or file_abs.stat().st_size <= 1024:
                entry["status"] = "failed"
                entry["error"] = "file missing or < 1KB (codex 未真出图或路径不对)"
                failed += 1
                result_items.append(entry)
                continue

            needs_key = bool(it.get("transparent", True))
            if needs_key:
                # 走 chroma_key:codex 出图是 RGB 绿底,本地转 RGBA 真透明,覆盖原文件
                tmp_file = file_abs.with_name(file_abs.stem + "_keyed.png")
                ck_res = remove_chroma_key(
                    src_png=file_abs,
                    dst_png=tmp_file,
                    key_color=chroma_key,
                    threshold=80,
                    soft_edge=30,
                    despill=0.5,
                )
                if not ck_res["success"]:
                    entry["status"] = "failed"
                    entry["error"] = f"chroma_key failed: {ck_res['error']}"
                    failed += 1
                    result_items.append(entry)
                    continue
                # 用 os.replace 原子覆盖回 file_abs(Windows 安全;file_abs 此刻已不存在,直接移动)
                os.replace(str(tmp_file), str(file_abs))

            entry["status"] = "ok"
            entry["size_bytes"] = file_abs.stat().st_size
            ok += 1
            result_items.append(entry)
        return {"batch_id": batch_id, "ok": ok, "failed": failed, "items": result_items}


async def tool_dispatch_l1(args: dict) -> dict:
    batches = args["batches"]
    concurrency = int(args.get("concurrency", DEFAULT_CONCURRENCY))
    sem = asyncio.Semaphore(concurrency)
    results = await asyncio.gather(
        *[_run_one_l1_batch(b, sem) for b in batches],
        return_exceptions=False,
    )
    return {"success": True, "results": results}


# ─── Tool: dispatch_l2 ─────────────────────────────────────────────

async def _run_one_l2_sheet(sheet: dict, sem: asyncio.Semaphore) -> dict:
    async with sem:
        sheet_id = sheet["sheet_id"]
        canvas_path = Path(sheet["canvas"])
        writable_roots = sheet.get("writable_roots", [])
        grid_rows = int(sheet["grid_rows"])
        grid_cols = int(sheet["grid_cols"])
        chroma_key = sheet.get("chroma_key", "#00ff00")
        items = sheet["items"]
        if not items:
            return {"sheet_id": sheet_id, "error": "empty items"}

        envelope = {
            "mode": "L2_SHEET",
            "sheet_id": sheet_id,
            "canvas": str(canvas_path),
            "size": "1024x1024",
            "transparent": False,
            "grid_rows": grid_rows,
            "grid_cols": grid_cols,
            "chroma_key": chroma_key,
            "items": items,
        }
        sheet_json_str = json.dumps(envelope, ensure_ascii=False, indent=2)
        prompt = L2_PROMPT_TPL.format(sheet_json=sheet_json_str)
        layout_order = [it["name"] for it in items]

        canvas_path.parent.mkdir(parents=True, exist_ok=True)
        for w in writable_roots:
            Path(w).mkdir(parents=True, exist_ok=True)

        log_dir = Path(writable_roots[0]) if writable_roots else canvas_path.parent
        result_path = log_dir / f".{sheet_id}.result.json"
        log_path = log_dir / f".{sheet_id}.log"

        codex_result = await run_codex_exec(
            prompt=prompt,
            result_json_path=result_path,
            writable_roots=writable_roots,
            log_path=log_path,
            model="gpt-5.5",
        )
        if not codex_result["success"]:
            return {
                "sheet_id": sheet_id,
                "canvas_path": str(canvas_path),
                "cuts_ok": 0,
                "cuts_failed": len(items),
                "error": codex_result["error"],
            }

        # 纯磁盘核验:不信 codex 文字回复,直接看 canvas 是否真落盘
        if not canvas_path.exists() or canvas_path.stat().st_size <= 1024:
            return {
                "sheet_id": sheet_id,
                "canvas_path": str(canvas_path),
                "cuts_ok": 0,
                "cuts_failed": len(items),
                "error": "canvas missing or < 1KB (codex 未真出图或路径不对)",
            }

        # 本地步骤 1：chroma-key 去键色
        alpha_canvas = canvas_path.with_name(canvas_path.stem + "_alpha.png")
        ck_res = remove_chroma_key(
            src_png=canvas_path,
            dst_png=alpha_canvas,
            key_color=chroma_key,
            threshold=80,
            soft_edge=30,
            despill=0.5,
        )
        if not ck_res["success"]:
            return {
                "sheet_id": sheet_id,
                "canvas_path": str(canvas_path),
                "cuts_ok": 0,
                "cuts_failed": len(items),
                "error": f"chroma_key failed: {ck_res['error']}",
            }

        # 本地步骤 2：image_cut 切图
        cut_out_dir = canvas_path.parent / f"{canvas_path.stem}_cut"
        cut_res = cut_sheet(alpha_canvas, cut_out_dir)
        if not cut_res["success"]:
            return {
                "sheet_id": sheet_id,
                "canvas_path": str(canvas_path),
                "alpha_canvas": str(alpha_canvas),
                "cuts_ok": 0,
                "cuts_failed": len(items),
                "error": cut_res["error"],
            }

        cut_items = _rename_l2_by_target(
            manifest=cut_res["manifest"],
            layout_order=layout_order,
            items=items,
        )
        ok = sum(1 for c in cut_items if c.get("status") == "ok")
        failed = len(cut_items) - ok
        return {
            "sheet_id": sheet_id,
            "canvas_path": str(canvas_path),
            "alpha_canvas": str(alpha_canvas),
            "cuts_ok": ok,
            "cuts_failed": failed,
            "cut_items": cut_items,
        }


def _rename_l2_by_target(manifest: dict, layout_order: list[str], items: list[dict]) -> list[dict]:
    """按 layout_order 把切出来的 sprite 移到 item.target_file。"""
    import shutil

    sprites = manifest.get("sprites", [])
    size = manifest.get("size", {})
    canvas_h = size.get("height", 1024)
    grid_rows = manifest.get("grid_rows") or 4
    row_h = canvas_h / grid_rows

    def sort_key(sp):
        bb = sp["bbox"]
        cx = bb["x"] + bb["width"] // 2
        cy = bb["y"] + bb["height"] // 2
        return (int(cy / row_h), cx)

    sprites_sorted = sorted(sprites, key=sort_key)
    name_to_item = {it["name"]: it for it in items}
    results: list[dict] = []

    for i, sp in enumerate(sprites_sorted):
        if i >= len(layout_order):
            break
        target_name = layout_order[i]
        item = name_to_item.get(target_name)
        if item is None:
            results.append({"name": target_name, "status": "failed", "error": "name not in items"})
            continue
        src = Path(sp.get("file") or sp.get("path"))
        if not src.exists():
            results.append({"name": target_name, "status": "failed", "error": f"sprite missing: {src}"})
            continue
        dest = Path(item["target_file"])
        dest.parent.mkdir(parents=True, exist_ok=True)
        try:
            shutil.move(str(src), str(dest))
            sz = dest.stat().st_size
            results.append({
                "name": target_name,
                "file": str(dest),
                "size_bytes": sz,
                "status": "ok" if sz > 1024 else "failed",
            })
        except Exception as e:
            results.append({"name": target_name, "status": "failed", "error": str(e)})

    matched = {r["name"] for r in results}
    for name in layout_order:
        if name not in matched:
            results.append({"name": name, "status": "failed", "error": "no sprite slot"})
    return results


async def tool_dispatch_l2(args: dict) -> dict:
    sheets = args["sheets"]
    concurrency = int(args.get("concurrency", DEFAULT_CONCURRENCY))
    sem = asyncio.Semaphore(concurrency)
    results = await asyncio.gather(
        *[_run_one_l2_sheet(s, sem) for s in sheets],
        return_exceptions=False,
    )
    return {"success": True, "results": results}


# ─── Tool: write_record ───────────────────────────────────────────

RECORD_HEADER = """# 美术资源生成记录

| 模式 | 批次/Sheet | 资源名 | 路径 | 状态 | 备注 |
|---|---|---|---|---|---|
"""


async def tool_write_record(args: dict) -> dict:
    record_path = Path(args["record_path"])
    record_path.parent.mkdir(parents=True, exist_ok=True)
    if not record_path.exists():
        record_path.write_text(RECORD_HEADER, encoding="utf-8")

    results = args["results"]
    rows = []
    for r in results:
        if "canvas_path" in r:
            mode, batch = "L2", r.get("sheet_id", "?")
            items = r.get("cut_items", [])
        else:
            mode, batch = "L1", r.get("batch_id", "?")
            items = r.get("items", [])
        for it in items:
            name = it.get("name") or Path(it.get("file", "")).stem
            file_field = it.get("file", "")
            status = it.get("status", "?")
            note = it.get("error", "") if status == "failed" else ""
            rows.append(f"| {mode} | {batch} | {name} | {file_field} | {status} | {note} |")
    with record_path.open("a", encoding="utf-8") as f:
        f.write("\n".join(rows) + "\n")
    return {"success": True, "record_path": str(record_path), "appended_rows": len(rows)}


# ─── MCP server boilerplate ────────────────────────────────────────

TOOLS = [
    Tool(
        name="dispatch_l1",
        description=(
            "L1 独立大图批次。每个 batch = 1 个 codex session 内多次 image_gen。"
            "参数 batches: [{batch_id, writable_roots[], items[{index,name,file(abs),size,transparent,prompt,negative}]}]"
        ),
        inputSchema={
            "type": "object",
            "properties": {
                "batches": {"type": "array", "items": {"type": "object"}},
                "concurrency": {"type": "integer", "default": DEFAULT_CONCURRENCY},
            },
            "required": ["batches"],
        },
    ),
    Tool(
        name="dispatch_l2",
        description=(
            "L2 小素材合图。每个 sheet = 1 个 codex session 内 1 次 image_gen → 本地 chroma_key + image_cut。"
            "参数 sheets: [{sheet_id, canvas(abs), writable_roots[], grid_rows, grid_cols, chroma_key?, "
            "items[{index,name,target_file(abs),prompt,negative}]}]"
        ),
        inputSchema={
            "type": "object",
            "properties": {
                "sheets": {"type": "array", "items": {"type": "object"}},
                "concurrency": {"type": "integer", "default": DEFAULT_CONCURRENCY},
            },
            "required": ["sheets"],
        },
    ),
    Tool(
        name="write_record",
        description="把 dispatch_l1/l2 的 results 追加到 markdown 表格。参数 record_path(abs), results[]。",
        inputSchema={
            "type": "object",
            "properties": {
                "record_path": {"type": "string"},
                "results": {"type": "array"},
            },
            "required": ["record_path", "results"],
        },
    ),
]


@server.list_tools()
async def list_tools():
    return TOOLS


@server.call_tool()
async def call_tool(name: str, arguments: dict) -> list[TextContent]:
    handlers = {
        "dispatch_l1": tool_dispatch_l1,
        "dispatch_l2": tool_dispatch_l2,
        "write_record": tool_write_record,
    }
    handler = handlers.get(name)
    if not handler:
        return [TextContent(type="text", text=json.dumps({"success": False, "error": f"unknown tool: {name}"}))]
    try:
        result = await handler(arguments)
    except Exception as e:
        result = {"success": False, "error": f"{type(e).__name__}: {e}"}
    return [TextContent(type="text", text=json.dumps(result, ensure_ascii=False))]


async def main():
    async with stdio_server() as (read_stream, write_stream):
        await server.run(read_stream, write_stream, server.create_initialization_options())


if __name__ == "__main__":
    asyncio.run(main())
