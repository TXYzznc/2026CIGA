"""L1 / L2 分桶（通用 lib, 类别集合由调用方传入）。

L1 = 独立大图, 每批 N 张 → 1 次 codex exec 内 N 次 image_gen
L2 = 小素材合图, 每批 ≤ N 张 → 1 次 codex exec 生 1 张 sheet, 本地切图
"""
from __future__ import annotations

import json
from pathlib import Path

L1_PER_BATCH_DEFAULT = 6
L2_PER_SHEET_DEFAULT = 16


def split_l1_l2(
    items: list[dict],
    *,
    l1_cats: set[str],
    l2_cats: set[str],
) -> tuple[list[dict], list[dict]]:
    """按 category 字段分 L1 / L2。

    匹配规则：
    - 精确匹配 l1_cats / l2_cats
    - 'env_floor' / 'env_light' 这类子类别按下划线前缀（'env'）再查 l1_cats
    - 未知类别默认进 L1（保险:独立画质好,合图易粘连）
    """
    l1, l2 = [], []
    for it in items:
        cat = it["category"]
        cat_top = cat.split("_")[0] if "_" in cat else cat
        if cat in l1_cats or cat_top in l1_cats:
            l1.append(it)
        elif cat in l2_cats or cat_top in l2_cats:
            l2.append(it)
        else:
            l1.append(it)
    return l1, l2


def skip_done(items: list[dict], art_dir: Path) -> list[dict]:
    """已 ok（item.file 存在且 > 1KB）的自动 skip。

    `art_dir` 用作 item.file 的解析根。
    """
    out = []
    for it in items:
        full = art_dir / it["file"]
        if full.exists() and full.stat().st_size > 1024:
            continue
        out.append(it)
    return out


def chunk_l1(items: list[dict], per_batch: int = L1_PER_BATCH_DEFAULT) -> list[list[dict]]:
    return [items[i:i+per_batch] for i in range(0, len(items), per_batch)]


def group_l2_by_category(items: list[dict], per_sheet: int = L2_PER_SHEET_DEFAULT) -> list[list[dict]]:
    """L2 按 category 聚集后切片 per_sheet/sheet。同类聚一起方便美术风格统一。"""
    by_cat: dict[str, list[dict]] = {}
    for it in items:
        by_cat.setdefault(it["category"], []).append(it)
    sheets = []
    for cat, lst in by_cat.items():
        for i in range(0, len(lst), per_sheet):
            sheets.append(lst[i:i+per_sheet])
    return sheets


def write_batch_json(batch: list[dict], out_path: Path, mode: str) -> None:
    """调试用：dump 单批 JSON 到磁盘。"""
    payload = {"mode": mode, "items": batch}
    out_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
