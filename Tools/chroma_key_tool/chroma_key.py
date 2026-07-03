#!/usr/bin/env python
"""Chroma-key 抠图工具。

把纯色背景（默认绿色 #00ff00）的 PNG 转成真透明 alpha PNG。
接口与 tools/ImageCut_Tool/image_cut.py 对齐：支持 CLI 与 --json 输出。

工作原理：
1. 像素颜色与 key 色的欧氏距离 < threshold → alpha = 0（完全透明）
2. 距离在 [threshold, threshold+soft_edge] 范围 → alpha 线性渐变（软边缘抗锯齿）
3. 远超 threshold → alpha 保持 255

为了避免主体里偶然含有 key 色（如绿色主体里的小绿点），用**连通域**思想：
只把"贴着画面边缘"或"从透明区域可达"的 key 色像素去掉。但简单实现先不做这步——
codex 生成的合图中，key 色是大片背景，主体内部即便有同色也不会过度损害。
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image


DEFAULT_KEY_COLOR = (0, 255, 0)   # 纯绿
DEFAULT_THRESHOLD = 80            # 颜色距离阈值（0-441，欧氏距离 sqrt(3*255^2)≈441）
DEFAULT_SOFT_EDGE = 30            # 软边缘渐变宽度
DEFAULT_DESPILL = 0.5             # 去溢色强度（0-1，把残留绿色减弱）


def parse_color(s: str) -> tuple[int, int, int]:
    """支持 '#00ff00' / '0,255,0' / 'green' 三种格式。"""
    s = s.strip().lower()
    if s.startswith("#"):
        s = s[1:]
        return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16))
    if "," in s:
        r, g, b = (int(x) for x in s.split(","))
        return (r, g, b)
    named = {"green": (0, 255, 0), "magenta": (255, 0, 255), "cyan": (0, 255, 255)}
    if s in named:
        return named[s]
    raise ValueError(f"unsupported color format: {s}")


def remove_chroma_key(
    img: Image.Image,
    key_color: tuple[int, int, int] = DEFAULT_KEY_COLOR,
    threshold: float = DEFAULT_THRESHOLD,
    soft_edge: float = DEFAULT_SOFT_EDGE,
    despill: float = DEFAULT_DESPILL,
) -> Image.Image:
    """核心算法：返回 RGBA 透明图。"""
    img = img.convert("RGBA")
    arr = np.array(img).astype(np.float32)
    rgb = arr[:, :, :3]
    key = np.array(key_color, dtype=np.float32)

    # 1. 欧氏距离
    dist = np.sqrt(np.sum((rgb - key) ** 2, axis=2))

    # 2. alpha 线性插值
    #   dist <= threshold        → alpha=0
    #   dist >= threshold+soft   → alpha=255
    #   中间                      → 线性渐变
    soft_end = threshold + soft_edge
    if soft_edge > 0:
        new_alpha = np.clip((dist - threshold) / soft_edge, 0.0, 1.0) * 255.0
    else:
        new_alpha = np.where(dist > threshold, 255.0, 0.0)

    # 3. 去溢色（despill）：边缘像素的绿色分量按 despill 强度衰减
    if despill > 0 and key_color == (0, 255, 0):
        # 仅对边缘（new_alpha 在 1-254 之间）做去绿
        edge_mask = (new_alpha > 0) & (new_alpha < 255)
        # 用 (R+B)/2 替代过强的 G
        rb_avg = (rgb[:, :, 0] + rgb[:, :, 2]) / 2.0
        excess_green = np.maximum(0, rgb[:, :, 1] - rb_avg)
        rgb[:, :, 1] = np.where(
            edge_mask,
            rgb[:, :, 1] - excess_green * despill,
            rgb[:, :, 1],
        )

    out = np.zeros_like(arr)
    out[:, :, :3] = np.clip(rgb, 0, 255)
    out[:, :, 3] = new_alpha
    return Image.fromarray(out.astype(np.uint8), mode="RGBA")


def process_one(
    src: Path,
    dst: Path | None,
    key_color: tuple[int, int, int],
    threshold: float,
    soft_edge: float,
    despill: float,
) -> dict:
    """处理单张图，返回 manifest dict。"""
    img = Image.open(src)
    out = remove_chroma_key(img, key_color, threshold, soft_edge, despill)
    if dst is None:
        dst = src.with_name(src.stem + "_alpha" + src.suffix)
    dst.parent.mkdir(parents=True, exist_ok=True)
    out.save(dst, format="PNG", optimize=True)

    arr = np.array(out)
    a = arr[:, :, 3]
    total = a.size
    alpha_zero = int(np.sum(a == 0))
    alpha_full = int(np.sum(a == 255))
    return {
        "source": str(src),
        "output": str(dst),
        "size": {"width": out.width, "height": out.height},
        "key_color": list(key_color),
        "threshold": threshold,
        "soft_edge": soft_edge,
        "despill": despill,
        "alpha_zero_pixels": alpha_zero,
        "alpha_full_pixels": alpha_full,
        "alpha_zero_ratio": round(alpha_zero / total, 4),
        "status": "ok",
    }


def main():
    p = argparse.ArgumentParser(
        description="Remove a flat chroma-key background from PNGs and write true alpha PNGs."
    )
    p.add_argument("inputs", nargs="+", help="PNG file(s) or folder(s).")
    p.add_argument("-o", "--output", default=None,
                   help="Output folder (default: same dir, suffix _alpha).")
    p.add_argument("--key", default="#00ff00",
                   help="Chroma key color (#hex / r,g,b / green/magenta/cyan). Default: #00ff00")
    p.add_argument("--threshold", type=float, default=DEFAULT_THRESHOLD,
                   help=f"Color distance threshold (0-441). Default: {DEFAULT_THRESHOLD}")
    p.add_argument("--soft-edge", type=float, default=DEFAULT_SOFT_EDGE,
                   help=f"Soft edge width for anti-aliasing. Default: {DEFAULT_SOFT_EDGE}")
    p.add_argument("--despill", type=float, default=DEFAULT_DESPILL,
                   help=f"Despill strength 0-1 (reduce residual key color at edges). Default: {DEFAULT_DESPILL}")
    p.add_argument("--json", action="store_true", help="Write manifest JSON to output dir.")
    p.add_argument("--overwrite", action="store_true",
                   help="Overwrite existing output files (default: skip).")
    args = p.parse_args()

    key_color = parse_color(args.key)

    # 收集输入文件
    input_files: list[Path] = []
    for inp in args.inputs:
        path = Path(inp)
        if path.is_file() and path.suffix.lower() == ".png":
            input_files.append(path)
        elif path.is_dir():
            input_files.extend(sorted(path.glob("*.png")))

    out_dir = Path(args.output) if args.output else None
    manifest_items: list[dict] = []
    for src in input_files:
        if out_dir:
            dst = out_dir / src.name
        else:
            dst = src.with_name(src.stem + "_alpha.png")
        if dst.exists() and not args.overwrite:
            print(f"[skip] {src.name} -> {dst} (exists)", file=sys.stderr)
            continue
        try:
            info = process_one(src, dst, key_color, args.threshold, args.soft_edge, args.despill)
            print(
                f"[ok] {src.name} -> {dst.name}  "
                f"alpha=0 {info['alpha_zero_ratio']*100:.1f}%",
                file=sys.stderr,
            )
            manifest_items.append(info)
        except Exception as e:
            print(f"[fail] {src.name}: {e}", file=sys.stderr)
            manifest_items.append({"source": str(src), "status": "failed", "error": str(e)})

    if args.json:
        manifest_path = (out_dir or input_files[0].parent) / "chroma_key_manifest.json"
        manifest_path.write_text(
            json.dumps({"items": manifest_items}, ensure_ascii=False, indent=2),
            encoding="utf-8",
        )
        print(f"[manifest] {manifest_path}", file=sys.stderr)


if __name__ == "__main__":
    main()
