"""v21 项目专用展开入口。

把 openspec/changes/<change_name>/art/prompts.md 展开成可直接传给 MCP
dispatch_l1 / dispatch_l2 的 envelope。

所有 v21 业务（风格指南 / 分类 / 命名规则）都在本文件,不污染 MCP。

用法：
    from codex_art_gen_helper.expand_v21 import build_envelopes
    l1, l2 = build_envelopes()
    # 传给 MCP: mcp.dispatch_l1(batches=l1) / mcp.dispatch_l2(sheets=l2)
"""
from __future__ import annotations

from pathlib import Path

from batch_parser import parse_prompts_md
from bucketizer import (
    chunk_l1,
    group_l2_by_category,
    skip_done,
    split_l1_l2,
)

REPO_ROOT = Path(__file__).resolve().parents[2]

# ─── v21 业务常量 ────────────────────────────────────────────────────

V21_STYLE_BASE = (
    "A 1024x1024 Hades-style 2.5D game asset, painterly thick brush strokes, "
    "vibrant saturated colors, strong silhouette with deep shadow outline, "
    "dramatic high-contrast lighting from upper-left at 45 degrees, "
    "rim light highlighting the subject, magical aura where applicable."
)
V21_NEG_BASE = (
    "no text, no letters, no numbers, no watermark, no signature, no logo, "
    "blurry, low contrast, flat vector, washed out, multiple subjects, "
    "frame border decoration"
)

V21_NON_TRANSPARENT_PREFIXES = ("env_floor", "env_wall")
V21_L1_CATS = {"character", "boss", "npc", "env"}
V21_L2_CATS = {"weapon", "skill", "affix", "paint", "recipe", "consumable", "hud", "ui"}

V21_L1_PER_BATCH = 6
V21_L2_PER_SHEET = 16


def build_envelopes(
    change_name: str = "06-v21-implementation",
    *,
    l1_per_batch: int = V21_L1_PER_BATCH,
    l2_per_sheet: int = V21_L2_PER_SHEET,
    skip_already_done: bool = True,
) -> tuple[list[dict], list[dict]]:
    """读 v21 prompts.md → 展开 → 分桶 → 产出 MCP 直传 envelope。

    Returns:
        (l1_batches, l2_sheets) 直接传给 mcp.dispatch_l1(batches=l1_batches) / mcp.dispatch_l2(sheets=l2_sheets)。
    """
    art_dir = (REPO_ROOT / "openspec" / "changes" / change_name / "art").resolve()
    prompts_md = art_dir / "prompts.md"
    if not prompts_md.exists():
        raise FileNotFoundError(f"prompts.md not found: {prompts_md}")

    items = parse_prompts_md(
        prompts_md,
        style_base=V21_STYLE_BASE,
        neg_base=V21_NEG_BASE,
        non_transparent_prefixes=V21_NON_TRANSPARENT_PREFIXES,
    )

    if skip_already_done:
        items = skip_done(items, art_dir)

    l1_items, l2_items = split_l1_l2(items, l1_cats=V21_L1_CATS, l2_cats=V21_L2_CATS)
    l1_chunks = chunk_l1(l1_items, per_batch=l1_per_batch)
    l2_groups = group_l2_by_category(l2_items, per_sheet=l2_per_sheet)

    art_raw = art_dir / "raw"
    l1_batches = [_to_l1_batch(chunk, i, art_dir, art_raw) for i, chunk in enumerate(l1_chunks, 1)]
    l2_sheets = [_to_l2_sheet(group, i, art_dir, art_raw) for i, group in enumerate(l2_groups, 1)]
    return l1_batches, l2_sheets


def _to_l1_batch(items: list[dict], seq: int, art_dir: Path, art_raw: Path) -> dict:
    return {
        "batch_id": f"v21-l1-{seq:03d}",
        "writable_roots": [str(art_raw)],
        "items": [
            {
                "index": it["index"],
                "name": it["name"],
                "file": str((art_dir / it["file"]).resolve()),
                "size": it.get("size", "1024x1024"),
                "transparent": bool(it.get("transparent", True)),
                "prompt": it["prompt"],
                "negative": it.get("negative", ""),
            }
            for it in items
        ],
    }


def _to_l2_sheet(items: list[dict], seq: int, art_dir: Path, art_raw: Path) -> dict:
    n = len(items)
    grid_rows = grid_cols = int(n ** 0.5)
    while grid_rows * grid_cols < n:
        grid_cols += 1
    category = items[0].get("category", "misc")
    canvas_name = f"v21_{category}_sheet_{seq:03d}.png"
    canvas_path = art_raw / "_merged" / canvas_name
    return {
        "sheet_id": f"v21-l2-{seq:03d}",
        "canvas": str(canvas_path.resolve()),
        "writable_roots": [str(art_raw)],
        "grid_rows": grid_rows,
        "grid_cols": grid_cols,
        "chroma_key": "#00ff00",
        "items": [
            {
                "index": it["index"],
                "name": it["name"],
                "target_file": str((art_dir / it["file"]).resolve()),
                "prompt": it["prompt"],
                "negative": it.get("negative", ""),
            }
            for it in items
        ],
    }


if __name__ == "__main__":
    import json
    l1, l2 = build_envelopes()
    print(f"L1 batches: {len(l1)}  L2 sheets: {len(l2)}")
    if l1:
        print(f"  L1[0] items: {len(l1[0]['items'])}  first={l1[0]['items'][0]['name']}")
    if l2:
        print(f"  L2[0] items: {len(l2[0]['items'])}  first={l2[0]['items'][0]['name']}")
