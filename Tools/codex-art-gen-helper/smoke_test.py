"""MCP 解耦烟测：1 L1 batch(2 张) + 1 L2 sheet(4 张), 完全不依赖 v21 业务。

跑通即证明 codex-art-gen MCP 真的解耦了。

注意:本脚本**直接 import server.py 的 handler 函数**, 不走 MCP stdio。
要走 stdio 真测请重启 Claude Code 用 mcp__codex-art-gen__* 调用。
"""
from __future__ import annotations

import asyncio
import sys
import time
from pathlib import Path

MCP_DIR = Path(__file__).resolve().parents[1] / "codex-art-gen-mcp"
sys.path.insert(0, str(MCP_DIR))

from server import tool_dispatch_l1, tool_dispatch_l2, tool_write_record  # noqa: E402

OUT_ROOT = Path(__file__).resolve().parents[2] / ".smoke-test-out"
OUT_ROOT.mkdir(parents=True, exist_ok=True)

L1_BATCH = {
    "batch_id": "smoke-l1-001",
    "writable_roots": [str(OUT_ROOT)],
    "items": [
        {
            "index": 1,
            "name": "smoke_apple",
            "file": str(OUT_ROOT / "smoke_apple.png"),
            "size": "1024x1024",
            "transparent": True,
            "prompt": "A single red apple icon on a plain background, simple digital illustration, centered composition.",
            "negative": "text, watermark, blurry",
        },
        {
            "index": 2,
            "name": "smoke_banana",
            "file": str(OUT_ROOT / "smoke_banana.png"),
            "size": "1024x1024",
            "transparent": True,
            "prompt": "A single yellow banana icon on a plain background, simple digital illustration, centered composition.",
            "negative": "text, watermark, blurry",
        },
    ],
}

L2_SHEET = {
    "sheet_id": "smoke-l2-001",
    "canvas": str(OUT_ROOT / "smoke_sheet.png"),
    "writable_roots": [str(OUT_ROOT)],
    "grid_rows": 2,
    "grid_cols": 2,
    "chroma_key": "#00ff00",
    "items": [
        {"index": 1, "name": "smoke_potion_red",  "target_file": str(OUT_ROOT / "smoke_potion_red.png"),  "prompt": "small red potion bottle icon, simple cartoon style", "negative": "text"},
        {"index": 2, "name": "smoke_potion_blue", "target_file": str(OUT_ROOT / "smoke_potion_blue.png"), "prompt": "small blue potion bottle icon, simple cartoon style", "negative": "text"},
        {"index": 3, "name": "smoke_potion_yellow","target_file": str(OUT_ROOT / "smoke_potion_yellow.png"),"prompt":"small yellow potion bottle icon, simple cartoon style","negative":"text"},
        {"index": 4, "name": "smoke_potion_purple","target_file": str(OUT_ROOT / "smoke_potion_purple.png"),"prompt":"small purple potion bottle icon, simple cartoon style","negative":"text"},
    ],
}


async def main():
    t0 = time.time()
    print(f"[smoke] OUT_ROOT={OUT_ROOT}")
    print("[smoke] dispatch_l1 (2 张独立)...")
    l1_res = await tool_dispatch_l1({"batches": [L1_BATCH]})
    print(f"[smoke] L1 result: {l1_res}")

    print("[smoke] dispatch_l2 (4 张合图)...")
    l2_res = await tool_dispatch_l2({"sheets": [L2_SHEET]})
    print(f"[smoke] L2 result: {l2_res}")

    record_path = OUT_ROOT / "smoke_record.md"
    print(f"[smoke] write_record → {record_path}")
    all_results = l1_res.get("results", []) + l2_res.get("results", [])
    rec_res = await tool_write_record({"record_path": str(record_path), "results": all_results})
    print(f"[smoke] record: {rec_res}")

    print(f"[smoke] total elapsed = {time.time() - t0:.1f}s")


if __name__ == "__main__":
    asyncio.run(main())
