"""L2 sheet 后处理：chroma_key 去键色 + image_cut 切图。

dispatch_l2 内部调用：codex 生成 1024×1024 键色背景合图后,
本地跑 chroma_key.py 把键色变 alpha=0,再跑 image_cut.py 按 bbox 切 N 张。

外部工具路径（chroma_key.py / image_cut.py / python 解释器）支持 env 覆盖,
不强行假设 MCP 在某仓库 layout 下。env 缺省时退回到 parents[2] 推断,
便于本仓库 in-place 跑。
"""
from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
from pathlib import Path


def _resolve_tool(env_key: str, fallback: Path) -> Path:
    p = os.environ.get(env_key)
    return Path(p) if p else fallback


_INFERRED_ROOT = Path(__file__).resolve().parents[2]
IMAGE_CUT_PY = _resolve_tool("CODEX_ART_GEN_IMAGE_CUT_PY", _INFERRED_ROOT / "tools" / "ImageCut_Tool" / "image_cut.py")
CHROMA_KEY_PY = _resolve_tool("CODEX_ART_GEN_CHROMA_KEY_PY", _INFERRED_ROOT / "tools" / "chroma_key_tool" / "chroma_key.py")
VENV_PYTHON = _resolve_tool("CODEX_ART_GEN_PYTHON", _INFERRED_ROOT / ".venv" / "Scripts" / "python.exe")


def remove_chroma_key(
    src_png: Path,
    dst_png: Path,
    key_color: str = "#00ff00",
    threshold: float = 80,
    soft_edge: float = 30,
    despill: float = 0.5,
) -> dict:
    """跑 chroma_key.py 把纯色背景转 alpha=0。返回 manifest dict。"""
    if not src_png.exists():
        return {"success": False, "error": f"src not found: {src_png}"}
    dst_png.parent.mkdir(parents=True, exist_ok=True)
    python_exe = str(VENV_PYTHON) if VENV_PYTHON.exists() else sys.executable
    cmd = [
        python_exe, str(CHROMA_KEY_PY), str(src_png),
        "--key", key_color,
        "--threshold", str(threshold),
        "--soft-edge", str(soft_edge),
        "--despill", str(despill),
        "-o", str(dst_png.parent),
        "--overwrite",
    ]
    try:
        result = subprocess.run(
            cmd,
            stdin=subprocess.DEVNULL,
            capture_output=True,
            text=True,
            timeout=60,
            encoding="utf-8",
        )
    except subprocess.TimeoutExpired:
        return {"success": False, "error": "chroma_key.py timeout 60s"}
    if result.returncode != 0:
        return {"success": False, "error": f"chroma_key.py exit {result.returncode}: {result.stderr[:300]}"}
    # chroma_key.py 输出到 -o dir + 同名文件，但我们要 dst_png 的特定名字
    real_out = dst_png.parent / src_png.name
    if real_out != dst_png and real_out.exists():
        real_out.rename(dst_png)
    if not dst_png.exists():
        return {"success": False, "error": f"chroma_key output missing: {dst_png}"}
    return {"success": True, "output": str(dst_png), "stderr": result.stderr.strip()[:300]}


def cut_sheet(
    sheet_png: Path,
    out_dir: Path,
    alpha_threshold: int = 16,
    min_area: int = 80,
    padding: int = 2,
) -> dict:
    """跑 image_cut.py 切图，返回它的 JSON manifest。"""
    if not sheet_png.exists():
        return {"success": False, "error": f"sheet not found: {sheet_png}"}
    out_dir.mkdir(parents=True, exist_ok=True)
    python_exe = str(VENV_PYTHON) if VENV_PYTHON.exists() else sys.executable
    cmd = [
        python_exe, str(IMAGE_CUT_PY), str(sheet_png),
        "-o", str(out_dir),
        "--alpha", str(alpha_threshold),
        "--min-area", str(min_area),
        "--padding", str(padding),
        "--json", "--overwrite",
    ]
    try:
        result = subprocess.run(
            cmd,
            stdin=subprocess.DEVNULL,
            capture_output=True,
            text=True,
            timeout=120,
            encoding="utf-8",
        )
    except subprocess.TimeoutExpired:
        return {"success": False, "error": "image_cut.py timeout 120s"}
    if result.returncode != 0:
        return {"success": False, "error": f"image_cut.py exit {result.returncode}: {result.stderr[:300]}"}

    # image_cut --json 实际输出 out_dir/manifest_all.json（list[dict]）
    manifest_path = out_dir / "manifest_all.json"
    if not manifest_path.exists():
        jsons = [p for p in out_dir.glob("*.json") if p.name != "chroma_key_manifest.json"]
        if not jsons:
            return {"success": False, "error": "no manifest produced"}
        manifest_path = jsons[0]
    try:
        raw = json.loads(manifest_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as e:
        return {"success": False, "error": f"manifest not JSON: {e}"}
    # image_cut 输出是 list[dict]，每个对应一张输入图；我们只切 1 张
    manifest = raw[0] if isinstance(raw, list) and raw else raw
    return {"success": True, "manifest": manifest, "manifest_path": str(manifest_path)}


def rename_by_layout_order(
    manifest: dict,
    layout_order: list[str],
    items_meta: list[dict],
    art_raw_root: Path,
) -> list[dict]:
    """按 grid row 桶 + x 坐标排序，回填 layout_order 命名，移到对应 raw/<category>/ 目录。

    返回 [{name, file, status: ok|failed, ...}]
    """
    sprites = manifest.get("sprites", [])
    size = manifest.get("size", {})
    canvas_h = size.get("height", 1024)
    grid_rows = manifest.get("grid_rows") or 4  # L2 模板默认 4×4
    row_h = canvas_h / grid_rows

    def sort_key(sp):
        bb = sp["bbox"]
        cx = bb["x"] + bb["width"] // 2
        cy = bb["y"] + bb["height"] // 2
        return (int(cy / row_h), cx)

    sprites_sorted = sorted(sprites, key=sort_key)
    name_to_item = {it["name"]: it for it in items_meta}
    results: list[dict] = []

    for i, sp in enumerate(sprites_sorted):
        if i >= len(layout_order):
            break
        target_name = layout_order[i]
        item = name_to_item.get(target_name)
        if item is None:
            results.append({"name": target_name, "status": "failed", "error": "name not in items_meta"})
            continue
        src = Path(sp.get("file") or sp.get("path"))  # image_cut.py 输出字段名是 "file"
        if not src.exists():
            results.append({"name": target_name, "status": "failed", "error": f"sprite missing: {src}"})
            continue
        dest_rel = item["file"]  # raw/<cat>/<name>.png
        dest_abs = art_raw_root / dest_rel
        dest_abs.parent.mkdir(parents=True, exist_ok=True)
        try:
            shutil.move(str(src), str(dest_abs))
            sz = dest_abs.stat().st_size
            results.append({
                "name": target_name,
                "file": dest_rel,
                "size_bytes": sz,
                "status": "ok" if sz > 1024 else "failed",
            })
        except Exception as e:
            results.append({"name": target_name, "status": "failed", "error": str(e)})

    # 没匹配上的 layout name 也记一笔
    matched = {r["name"] for r in results}
    for name in layout_order:
        if name not in matched:
            results.append({"name": name, "status": "failed", "error": "no sprite slot"})
    return results
