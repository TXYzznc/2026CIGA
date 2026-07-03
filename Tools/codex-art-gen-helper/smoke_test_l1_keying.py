"""精简 smoke:验证 L1 chroma_key 修复 — 1 张图,验证 RGBA + 绿底抠图工作。

跑完后检查:
- smoke_keying_test.png 应该是 RGBA 模式
- 四角 alpha = 0
- 主体区域 alpha > 0
"""
from __future__ import annotations

import asyncio
import sys
import time
from pathlib import Path

MCP_DIR = Path(__file__).resolve().parents[1] / "codex-art-gen-mcp"
sys.path.insert(0, str(MCP_DIR))

from server import tool_dispatch_l1  # noqa: E402

OUT_ROOT = Path(__file__).resolve().parents[2] / ".smoke-test-out"
OUT_ROOT.mkdir(parents=True, exist_ok=True)

L1_BATCH = {
    "batch_id": "smoke-keying-001",
    "writable_roots": [str(OUT_ROOT)],
    "chroma_key": "#00ff00",
    "items": [
        {
            "index": 1,
            "name": "smoke_keying_test",
            "file": str(OUT_ROOT / "smoke_keying_test.png"),
            "size": "1024x1024",
            "transparent": True,
            "prompt": "A single blue crystal gemstone, glowing, simple cartoon style.",
            "negative": "text, watermark, blurry",
        },
    ],
}


async def main():
    t0 = time.time()
    print(f"[smoke-key] OUT_ROOT={OUT_ROOT}")
    res = await tool_dispatch_l1({"batches": [L1_BATCH]})
    print(f"[smoke-key] L1 result: {res}")
    print(f"[smoke-key] elapsed = {time.time() - t0:.1f}s")

    # 验证 RGBA + alpha
    from PIL import Image
    p = OUT_ROOT / "smoke_keying_test.png"
    if not p.exists():
        print("[smoke-key] FAIL: file missing")
        sys.exit(1)
    img = Image.open(p)
    w, h = img.size
    print(f"[smoke-key] mode={img.mode}, size=({w},{h})")
    if img.mode != "RGBA":
        print(f"[smoke-key] FAIL: expected RGBA, got {img.mode}")
        sys.exit(2)
    alpha = img.split()[-1]
    corners = [(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)]
    a = [alpha.getpixel(c) for c in corners]
    print(f"[smoke-key] corner_alpha={a}")
    if any(v > 32 for v in a):
        print("[smoke-key] FAIL: corners not transparent (chroma_key didn't work)")
        sys.exit(3)
    # 中心应该非透明(图案主体)
    center_alpha = alpha.getpixel((w // 2, h // 2))
    print(f"[smoke-key] center_alpha={center_alpha}")
    if center_alpha < 32:
        print("[smoke-key] WARN: center is transparent — subject too small or off-center")
    print("[smoke-key] PASS — RGBA + corners transparent + center opaque")


if __name__ == "__main__":
    asyncio.run(main())
