"""codex-art-gen-helper — 项目级 helper, 把 prompts.md/business 展开成 MCP envelope。

设计原则：
- MCP (tools/codex-art-gen-mcp/) 完全无业务, 只跑 codex + chroma_key + image_cut。
- 业务（风格指南 / 分类规则 / prompts.md 格式）放本包。
- 每个项目写自己的 `expand_<project>.py`, 复用 batch_parser + bucketizer 两个通用 lib。

公开 API：
- batch_parser.parse_prompts_md(prompts_md_path, style_base, neg_base, non_transparent_prefixes)
- bucketizer.split_l1_l2(items, l1_cats, l2_cats)
- bucketizer.chunk_l1(items, per_batch)
- bucketizer.group_l2_by_category(items, per_sheet)
- expand_v21.build_envelopes(change_name) → (l1_batches, l2_sheets)  # v21 项目专用
"""
