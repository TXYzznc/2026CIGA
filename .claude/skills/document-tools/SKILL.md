---
name: document-tools
description: "文档与文件处理工具集。当用户需要创建、读取、编辑、转换任何文档格式时使用。覆盖格式：Word(.docx)、PDF、PowerPoint(.pptx)、Excel(.xlsx/.csv)、以及通过 markitdown 支持的 HTML/图片/音频/JSON/XML/ZIP/EPUB 等。触发场景包括：提及任何文档格式名称、要求生成专业文档、转换文件格式、提取文件内容、复制文档格式模板等。即使用户只是说'帮我做个文档'或'读取这个表格'也应触发。"
---

# Document Tools — 文档与文件处理

## 子技能列表

| 子技能 | 适用场景 | 参考文件 |
|--------|---------|---------|
| **docx** | 创建/读取/编辑 Word 文档（.docx），生成带目录、标题、页码的专业文档 | `references/docx.md` |
| **docx-format-replicator** | 从现有 Word 文档提取格式，用相同格式生成新文档（模板复制） | `references/docx-format-replicator.md` |
| **pdf** | 读取/提取 PDF 内容，合并/拆分 PDF，添加水印，创建新 PDF，填写 PDF 表单 | `references/pdf.md` |
| **pptx** | 创建/读取/编辑 PowerPoint 演示文稿，提取幻灯片内容 | `references/pptx.md` |
| **xlsx** | 打开/读取/编辑/创建 Excel 电子表格（.xlsx/.xlsm/.csv/.tsv） | `references/xlsx.md` |
| **markitdown** | 使用 markitdown CLI 将文件转为 Markdown（支持 PDF/Word/Excel/PPT/HTML/图片/音频/CSV/JSON/XML/ZIP/EPUB） | `references/markitdown.md` |

## 使用流程

1. 根据用户需求和目标文件格式，匹配最合适的子技能
2. 读取对应的 `references/*.md` 获取该子技能的详细操作指令
3. 按照指令执行任务

## 选择指南

- 需要**创建/编辑** Office 文档 → 对应格式的子技能（docx/pptx/xlsx）
- 需要**复制文档格式**生成新文档 → docx-format-replicator
- 需要**提取文件内容为纯文本** → markitdown（最通用，格式支持最广）
- 需要 **PDF 操作**（合并/拆分/水印） → pdf
- 用户只说"转成 markdown" → markitdown
