#!/usr/bin/env python
"""Command-line interface for ImageCompression_Tool.

Wraps ``compressor.batch_compress`` so AI agents (and humans) can compress
PNG/JPEG/WEBP/TGA art assets without launching the Tkinter GUI in ``app.py``.

Quick reference
---------------
Compress one file (writes to ``<input_dir>/compressed/``)::

    .venv/Scripts/python tools/ImageCompression_Tool/cli.py path/to/img.png

Compress a folder recursively, in-place (DANGEROUS — overwrites originals)::

    .venv/Scripts/python tools/ImageCompression_Tool/cli.py path/to/dir --recursive --in-place

Apply a preset from ``.claude/美术资源规范.md`` and emit machine-readable summary::

    .venv/Scripts/python tools/ImageCompression_Tool/cli.py path/to/dir --preset ui-icon --json
"""

from __future__ import annotations

import argparse
import json
import shutil
import sys
from pathlib import Path

# Allow running as ``python tools/ImageCompression_Tool/cli.py`` from repo root
sys.path.insert(0, str(Path(__file__).resolve().parent))

from compressor import batch_compress, compress_image, get_file_size_str, SUPPORTED_FORMATS  # noqa: E402


# Presets mirror the size/format budgets in `.claude/美术资源规范.md`.
# Keys MUST match the规范文档 §二 / §四 各类型 preset 名称，便于 AI 直接套用。
PRESETS: dict[str, dict] = {
    "ui-icon": {
        "max_width": 256, "max_height": 256,
        "output_format": "PNG",
        "png_quantize": True, "png_colors": 256,
        "quality": 90,
    },
    "ui-button": {
        "max_width": 512, "max_height": 256,
        "output_format": "PNG",
        "png_quantize": True, "png_colors": 256,
        "quality": 90,
    },
    "ui-panel": {
        "max_width": 1024, "max_height": 1024,
        "output_format": "PNG",
        "png_quantize": True, "png_colors": 256,
        "quality": 90,
    },
    "ui-bg": {
        "max_width": 1920, "max_height": 1080,
        "output_format": "PNG",
        "png_quantize": True, "png_colors": 256,
        "quality": 85,
    },
    "character-portrait": {
        "max_width": 1024, "max_height": 2048,
        "output_format": "PNG",
        "png_quantize": False,
        "quality": 92,
    },
    "character-avatar": {
        "max_width": 512, "max_height": 512,
        "output_format": "PNG",
        "png_quantize": True, "png_colors": 256,
        "quality": 90,
    },
    "item-icon": {
        "max_width": 256, "max_height": 256,
        "output_format": "PNG",
        "png_quantize": True, "png_colors": 256,
        "quality": 90,
    },
    "skill-icon": {
        "max_width": 256, "max_height": 256,
        "output_format": "PNG",
        "png_quantize": True, "png_colors": 256,
        "quality": 90,
    },
    "decal": {
        # 贴花 / 装饰图样：表面贴图，细节多，不量化保边缘
        "max_width": 1024, "max_height": 1024,
        "output_format": "PNG",
        "png_quantize": False,
        "quality": 92,
    },
    "environment-bg": {
        "max_width": 1920, "max_height": 1080,
        "output_format": "JPEG",
        "quality": 85,
    },
    "effect-texture": {
        "max_width": 512, "max_height": 512,
        "output_format": "PNG",
        "png_quantize": True, "png_colors": 256,
        "quality": 85,
    },
    "screenshot": {
        "max_width": 1920, "max_height": 1080,
        "output_format": "JPEG",
        "quality": 80,
    },
}

# Backward-compat aliases. Old callers may still pass --preset tattoo; map to decal.
PRESET_ALIASES: dict[str, str] = {
    "tattoo": "decal",
}


def collect_inputs(paths, recursive: bool) -> list[Path]:
    images: list[Path] = []
    for raw in paths:
        p = Path(raw).expanduser()
        if p.is_file() and p.suffix.lower() in SUPPORTED_FORMATS:
            images.append(p)
        elif p.is_dir():
            globber = p.rglob if recursive else p.glob
            images.extend(sorted(
                f for f in globber("*")
                if f.is_file() and f.suffix.lower() in SUPPORTED_FORMATS
            ))
        else:
            print(f"[WARN] 跳过不支持的输入: {p}", file=sys.stderr)
    # de-dup while preserving order
    seen, out = set(), []
    for p in images:
        sp = str(p)
        if sp not in seen:
            out.append(p)
            seen.add(sp)
    return out


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Compress PNG/JPEG/WEBP/TGA images. Wraps compressor.batch_compress.",
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("inputs", nargs="+", help="Image files or folders.")
    parser.add_argument("-o", "--output", default=None,
                        help="Output directory. Default: '<first_input_dir>/compressed'.")
    parser.add_argument("--recursive", action="store_true", help="Scan folders recursively.")
    parser.add_argument("--in-place", action="store_true",
                        help="DANGER: overwrite originals. Incompatible with -o / --suffix.")
    parser.add_argument("--preset", choices=sorted(set(PRESETS) | set(PRESET_ALIASES)),
                        help="Apply preset from .claude/art-asset-spec (overrides --max-*/--format).")
    parser.add_argument("--quality", type=int, default=80, help="JPEG/WEBP quality 1-100.")
    parser.add_argument("--max-width", type=int, default=0, help="Resize width <= N. 0 = no limit.")
    parser.add_argument("--max-height", type=int, default=0, help="Resize height <= N. 0 = no limit.")
    parser.add_argument("--format", dest="output_format", default="same",
                        choices=["same", "JPEG", "PNG", "WEBP", "TGA", "BMP"],
                        help="Output format. 'same' keeps source format.")
    parser.add_argument("--png-lossless", action="store_true",
                        help="PNG: optimize + max compress level (lossless).")
    parser.add_argument("--png-quantize", action="store_true",
                        help="PNG: lossy palette quantize (uses pngquant if available).")
    parser.add_argument("--png-colors", type=int, default=256,
                        help="PNG quantize colors 2-256.")
    parser.add_argument("--keep-exif", action="store_true", help="JPEG: keep EXIF metadata.")
    parser.add_argument("--suffix", default="",
                        help="Append suffix to filename (e.g. _compressed). Empty = keep stem.")
    parser.add_argument("--json", dest="json_out", action="store_true",
                        help="Print machine-readable JSON summary at end (for AI consumption).")
    parser.add_argument("--quiet", action="store_true", help="Suppress per-file progress lines.")
    return parser


def merge_preset(options: dict, preset_name: str) -> dict:
    canonical = PRESET_ALIASES.get(preset_name, preset_name)
    preset = PRESETS[canonical]
    merged = dict(options)
    for k, v in preset.items():
        merged[k] = v
    return merged


def process_in_place(image: Path, options: dict) -> dict:
    """Compress in place via temp file + atomic replace (Pillow file-handle safe)."""
    tmp = image.with_suffix(image.suffix + ".tmp")
    res = compress_image(
        input_path=str(image),
        output_path=str(tmp),
        quality=options["quality"],
        max_width=options["max_width"],
        max_height=options["max_height"],
        keep_exif=options["keep_exif"],
        lossless=options.get("lossless", False),
        png_lossless=options["png_lossless"],
        output_format=options["output_format"],
        png_quantize=options["png_quantize"],
        png_colors=options["png_colors"],
    )
    if res["success"]:
        shutil.move(str(tmp), str(image))
        res["output_path"] = str(image)
    else:
        if tmp.exists():
            tmp.unlink()
    return res


def print_progress(current: int, total: int, res: dict) -> None:
    name = Path(res["input_path"]).name
    if res["success"]:
        info = (
            f"{get_file_size_str(res['input_size'])} → "
            f"{get_file_size_str(res['output_size'])}  "
            f"{res['ratio']:.1f}%"
        )
        sign = "✓"
    else:
        info = res.get("error", "")
        sign = "✗"
    print(f"[{current}/{total}] {sign} {name}  {info}")


def main() -> int:
    args = build_parser().parse_args()

    if args.in_place and (args.output or args.suffix):
        print("[ERROR] --in-place 与 -o / --suffix 互斥（会改变原路径语义）。", file=sys.stderr)
        return 2
    if args.preset and (args.max_width or args.max_height or args.output_format != "same"):
        # Not fatal — preset wins. Just tell the user.
        print(f"[INFO] --preset {args.preset} 会覆盖 --max-width / --max-height / --format。",
              file=sys.stderr)

    images = collect_inputs(args.inputs, args.recursive)
    if not images:
        print("[ERROR] 未找到任何支持的图片文件。", file=sys.stderr)
        return 1

    options = {
        "quality": args.quality,
        "max_width": args.max_width,
        "max_height": args.max_height,
        "keep_exif": args.keep_exif,
        "lossless": False,
        "png_lossless": args.png_lossless,
        "output_format": args.output_format,
        "png_quantize": args.png_quantize,
        "png_colors": args.png_colors,
        "suffix_tag": args.suffix,
    }
    if args.preset:
        options = merge_preset(options, args.preset)
        canonical = PRESET_ALIASES.get(args.preset, args.preset)
        if not args.quiet:
            preset_brief = {k: v for k, v in PRESETS[canonical].items()}
            label = args.preset if canonical == args.preset else f"{args.preset} (alias of {canonical})"
            print(f"[PRESET] {label} → {preset_brief}")

    results: list[dict] = []

    if args.in_place:
        total = len(images)
        for i, img in enumerate(images, start=1):
            res = process_in_place(img, options)
            results.append(res)
            if not args.quiet:
                print_progress(i, total, res)
    else:
        outdir = Path(args.output) if args.output else images[0].parent / "compressed"
        outdir.mkdir(parents=True, exist_ok=True)
        results = batch_compress(
            input_paths=[str(p) for p in images],
            output_dir=str(outdir),
            options=options,
            progress_callback=None if args.quiet else print_progress,
        )

    ok = sum(1 for r in results if r["success"])
    fail = len(results) - ok
    total_in = sum(r["input_size"] for r in results if r["success"])
    total_out = sum(r["output_size"] for r in results if r["success"])
    saved_pct = (1 - total_out / total_in) * 100 if total_in > 0 else 0.0

    if args.json_out:
        summary = {
            "total": len(results),
            "ok": ok,
            "fail": fail,
            "input_bytes": total_in,
            "output_bytes": total_out,
            "saved_pct": round(saved_pct, 2),
            "preset": args.preset,
            "files": [
                {
                    "input": r["input_path"],
                    "output": r["output_path"],
                    "input_size": r["input_size"],
                    "output_size": r["output_size"],
                    "ratio": round(r["ratio"], 2),
                    "success": r["success"],
                    "error": r["error"],
                }
                for r in results
            ],
        }
        print(json.dumps(summary, ensure_ascii=False, indent=2))
    else:
        print(f"\n[完成] {ok}/{len(results)} 成功，{fail} 失败")
        if total_in > 0:
            print(
                f"       {get_file_size_str(total_in)} → "
                f"{get_file_size_str(total_out)}  节省 {saved_pct:.1f}%"
            )

    return 0 if fail == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
