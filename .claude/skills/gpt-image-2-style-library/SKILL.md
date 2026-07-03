---
name: gpt-image-2-style-library
description: 从awesome-gpt-image-2风格库中选择GPT-Image2 / gpt-image-2视觉样式及行业提示词模板。当Agent需要借助仓库提供的模板、分类、风格标签、场景标签、注意事项及示例来创建、重写、分类或优化图像生成提示词时，可使用本技能。
tags: gpt-image2, prompt-engineering, image-generation, agent-skill, style-library
tags_cn: GPT-Image2, 提示词工程, 图像生成, Agent技能, 风格库
---

# GPT-Image2 风格库

使用本技能，可借助awesome-gpt-image-2风格库将用户的图像生成需求转化为可投入生产使用的GPT-Image2提示词。

## 示例输出

![城市生命系统图谱示例](assets/city-life-system-map.png)

示例请求：`用 gpt-image-2-style-library 技能生成城市生命系统图谱`

## 参考资料

- 在选择模板或样式前，请阅读`references/style-library.md`。
- 参考资料由仓库中的`data/style-library.json`生成。
- 当涉及模板名称、分类、封面或风格标签时，优先参考该资料而非依赖记忆。

## 工作流程

1. 检测用户使用的语言，并以该语言回复。
2. 确定用户的目标输出类型：产品、海报、UI、信息图、品牌、照片、插画、角色、场景、历史素材、文档或特殊任务。
3. 按以下顺序匹配请求：模板分类、视觉风格标签、场景标签，然后是最接近的示例案例。
4. 如果某一模板明显最匹配，则直接使用；若多个模板都合适，则提供2-3个选项并附上简短理由，请求用户选择。
5. 结合以下模块构建最终提示词：
   - 主题与任务
   - 构图与布局
   - 视觉风格与材质
   - 文字与标签要求
   - 宽高比与输出格式
   - 约束条件与负面细节
6. 包含所选模板名称及任何有用的示例案例ID。

## 输出默认规则

- 首先提供可复制的提示词。
- 约束条件需具体明确：确切文本、宽高比、清晰可读的标签、布局层级以及需避免的瑕疵。
- 对于中文请求，最终提示词默认使用中文，除非用户要求英文。
- 对于英文请求，最终提示词默认使用英文，除非用户要求中文。
- 当用户请求多个概念时，复用同一模板，仅调整主题、构图、配色及场景。

## 维护说明

当源仓库更新时，运行：

```bash
npm run generate:style-skill
```

要将本技能安装到本地Codex技能文件夹，运行：

```bash
npm run install:skill
```