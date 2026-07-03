"""纯本地测试 L1 chroma_key 替换流程,不烧 codex token。

模拟 codex 输出:用 PIL 造一张 1024x1024 绿底+蓝色矩形的 RGB PNG,
然后直接调 server.py 内的 chroma_key 替换段(_run_one_l1_batch 后处理逻辑)。

验证:
- 替换后 file_abs 是 RGBA
- 四角 alpha = 0(绿底被键出)
- 中心区域 alpha > 0(蓝色矩形保留)
"""
from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image, ImageDraw

MCP_DIR = Path(__file__).resolve().parents[1] / "codex-art-gen-mcp"
sys.path.insert(0, str(MCP_DIR))

from sheet_cutter import remove_chroma_key  # noqa: E402
import os  # noqa: E402

OUT_ROOT = Path(__file__).resolve().parents[2] / ".smoke-test-out"
OUT_ROOT.mkdir(parents=True, exist_ok=True)


def make_fake_codex_output(p: Path):
    """造一张 1024x1024 RGB 绿底 + 蓝色矩形的图,模拟 codex gpt-image-2 输出。"""
    img = Image.new("RGB", (1024, 1024), (0, 255, 0))  # 纯绿底
    d = ImageDraw.Draw(img)
    d.rectangle([(384, 384), (640, 640)], fill=(20, 80, 255))  # 蓝色 256x256 矩形
    img.save(p, format="PNG")


def main():
    file_abs = OUT_ROOT / "local_test_chroma.png"
    make_fake_codex_output(file_abs)
    print(f"[local-test] created fake codex output: {file_abs}")
    print(f"[local-test] original mode: {Image.open(file_abs).mode}")

    # 跑 server.py 的 chroma_key 替换段(逐字复制 _run_one_l1_batch 的核心逻辑)
    tmp_file = file_abs.with_name(file_abs.stem + "_keyed.png")
    ck_res = remove_chroma_key(
        src_png=file_abs,
        dst_png=tmp_file,
        key_color="#00ff00",
        threshold=80,
        soft_edge=30,
        despill=0.5,
    )
    print(f"[local-test] chroma_key result: {ck_res}")
    if not ck_res["success"]:
        print("[local-test] FAIL: chroma_key step failed")
        sys.exit(1)

    # 关键:此刻 file_abs 已被 helper rename 走,只有 tmp_file 存在
    if file_abs.exists():
        print(f"[local-test] WARN: file_abs unexpectedly still exists")
    if not tmp_file.exists():
        print(f"[local-test] FAIL: tmp_file should exist but doesn't: {tmp_file}")
        sys.exit(2)

    os.replace(str(tmp_file), str(file_abs))
    print(f"[local-test] os.replace OK, file_abs now exists: {file_abs.exists()}")

    # 验证最终结果
    img = Image.open(file_abs)
    print(f"[local-test] final mode: {img.mode}, size: {img.size}")
    if img.mode != "RGBA":
        print(f"[local-test] FAIL: expected RGBA, got {img.mode}")
        sys.exit(3)

    alpha = img.split()[-1]
    w, h = img.size
    corners = [(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)]
    a = [alpha.getpixel(c) for c in corners]
    print(f"[local-test] corner_alpha: {a}")
    if any(v > 32 for v in a):
        print("[local-test] FAIL: corners not transparent")
        sys.exit(4)

    center_alpha = alpha.getpixel((w // 2, h // 2))
    print(f"[local-test] center_alpha: {center_alpha}")
    if center_alpha < 200:
        print("[local-test] FAIL: center should be near opaque")
        sys.exit(5)

    print("[local-test] PASS — chroma_key replace flow works")


if __name__ == "__main__":
    main()
