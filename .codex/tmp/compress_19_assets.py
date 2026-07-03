"""One-shot compression for 19-visual-polish assets (status icons + reference images).

- Status icons (64x64 pixel art, transparent): NEAREST downscale + palette PNG
- Reference illustrations (512x256 / 512x512): LANCZOS downscale + palette PNG
"""
import os
import sys
from PIL import Image

# (src_path, target_w, target_h, mode, budget_kb)
# mode: 'pixel' = NEAREST (status icons), 'illu' = LANCZOS (reference illustrations)
JOBS = [
    ("d:/unity/UnityProject/GameDesinger/Assets/Resources/Sprite/UI/StatusIcon/burn.png", 64, 64, "pixel", 20),
    ("d:/unity/UnityProject/GameDesinger/Assets/Resources/Sprite/UI/StatusIcon/poison.png", 64, 64, "pixel", 20),
    ("d:/unity/UnityProject/GameDesinger/Assets/Resources/Sprite/UI/StatusIcon/stun.png", 64, 64, "pixel", 20),
    ("d:/unity/UnityProject/GameDesinger/openspec/changes/19-visual-polish/art/raw/crit_text_ref.png", 512, 256, "illu", 200),
    ("d:/unity/UnityProject/GameDesinger/openspec/changes/19-visual-polish/art/raw/hitspark_ref.png", 512, 512, "illu", 200),
]


def kb(path):
    return os.path.getsize(path) / 1024.0


def save_optimized_rgba(img, dst):
    """Save as optimized RGBA PNG (lossless)."""
    img.save(dst, "PNG", optimize=True, compress_level=9)


def save_palette_with_alpha(img, dst, colors=256):
    """Quantize RGBA -> PNG-8 with transparency. Preserves a single fully-transparent index.

    Pillow's quantize() supports method=2 (MAXCOVERAGE) but RGBA quantize uses method=Image.Quantize.FASTOCTREE
    For decent quality + alpha, use convert('P', ...) via quantize on the alpha-composited image,
    or directly img.quantize(colors=colors, method=Image.Quantize.FASTOCTREE) which supports RGBA in modern Pillow.
    """
    # FASTOCTREE supports RGBA and keeps alpha. method=2 (MAXCOVERAGE) does not.
    quantized = img.quantize(colors=colors, method=Image.Quantize.FASTOCTREE)
    quantized.save(dst, "PNG", optimize=True, compress_level=9)


def process(src, tw, th, mode, budget_kb):
    before = kb(src)
    img = Image.open(src).convert("RGBA")
    w, h = img.size
    if mode == "pixel":
        resized = img.resize((tw, th), Image.NEAREST)
    else:
        resized = img.resize((tw, th), Image.LANCZOS)

    # Strategy: try lossless RGBA first; if over budget, fall back to palette quantize.
    # For 64x64 pixel icons, RGBA optimize usually already fits in 20KB.
    save_optimized_rgba(resized, src)
    size_after = kb(src)
    strategy = "RGBA-optimize"

    if size_after > budget_kb:
        # Fall back to palette PNG-8 (256 colors).
        save_palette_with_alpha(resized, src, colors=256)
        size_after = kb(src)
        strategy = "PNG8-256"

        # Still over budget? reduce colors further.
        if size_after > budget_kb:
            for c in (128, 64, 32):
                save_palette_with_alpha(resized, src, colors=c)
                size_after = kb(src)
                strategy = f"PNG8-{c}"
                if size_after <= budget_kb:
                    break

    return {
        "src": src,
        "from_kb": round(before, 1),
        "to_kb": round(size_after, 1),
        "from_res": f"{w}x{h}",
        "to_res": f"{tw}x{th}",
        "budget_kb": budget_kb,
        "strategy": strategy,
        "ok": size_after <= budget_kb,
    }


def main():
    results = []
    for src, tw, th, mode, budget in JOBS:
        if not os.path.exists(src):
            results.append({"src": src, "error": "NOT_FOUND"})
            continue
        r = process(src, tw, th, mode, budget)
        results.append(r)

    print("=" * 80)
    for r in results:
        if "error" in r:
            print(f"[FAIL] {r['src']}: {r['error']}")
            continue
        mark = "OK" if r["ok"] else "FAIL"
        print(f"[{mark}] {os.path.basename(r['src'])}: {r['from_kb']}KB({r['from_res']}) -> {r['to_kb']}KB({r['to_res']}) budget={r['budget_kb']}KB strategy={r['strategy']}")
    print("=" * 80)

    failures = [r for r in results if r.get("error") or not r.get("ok")]
    sys.exit(1 if failures else 0)


if __name__ == "__main__":
    main()
