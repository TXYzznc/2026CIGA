"""prompts.md → items 数组（通用 lib, 不耦合任何项目业务）。

prompts.md 里的 ```json``` 代码块包含 STYLE_BASE / NEG_BASE 占位符,
本 parser 用调用方传入的字符串展开。
"""
from __future__ import annotations

import json
import re
from pathlib import Path


def category_of(file_path: str) -> str:
    """raw/<category>/<name>.png → 'category'。

    要求 file 字段是 3 段相对路径; 否则退化用第 1 段或 'misc'。
    """
    parts = Path(file_path).parts
    if len(parts) >= 2 and parts[0] == "raw":
        return parts[1]
    return parts[0] if parts else "misc"


def parse_prompts_md(
    prompts_md: Path,
    *,
    style_base: str = "",
    neg_base: str = "",
    non_transparent_prefixes: tuple[str, ...] = (),
) -> list[dict]:
    """提取所有 ```json``` 块, 展开 {style_base, neg_base} 占位符, 规范化 items。

    Args:
        prompts_md: prompts.md 路径
        style_base: 用来替换 prompt 里 'STYLE_BASE' 字面量
        neg_base: 用来替换 negative 里 'NEG_BASE' 字面量
        non_transparent_prefixes: 命中前缀的 item 强制 transparent=False
                                  (用于贴图素材如 env_floor / env_wall)
    """
    txt = prompts_md.read_text(encoding="utf-8")
    blocks = re.findall(r"```json\s*(\[.*?\])\s*```", txt, re.S)
    items: list[dict] = []
    for block in blocks:
        try:
            parsed = json.loads(block)
        except json.JSONDecodeError:
            continue
        if not isinstance(parsed, list):
            parsed = [parsed]
        items.extend(parsed)

    normalized: list[dict] = []
    for idx, it in enumerate(items, start=1):
        file_rel = it.get("file", "").strip()
        name = Path(file_rel).stem
        category = category_of(file_rel)

        prompt = it.get("prompt", "").replace("STYLE_BASE", style_base)
        neg = it.get("negative", "").replace("NEG_BASE", neg_base)

        transparent = bool(it.get("transparent", True))
        if any(name.startswith(p) for p in non_transparent_prefixes):
            transparent = False

        normalized.append({
            "index": idx,
            "name": name,
            "category": category,
            "file": file_rel,
            "size": it.get("size", "1024x1024"),
            "transparent": transparent,
            "prompt": prompt,
            "negative": neg,
        })
    return normalized


def write_items_json(items: list[dict], out_path: Path) -> None:
    out_path.write_text(json.dumps(items, ensure_ascii=False, indent=2), encoding="utf-8")
