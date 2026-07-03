---
name: ai-art
description: "AI 绘图提示词生成与美术素材实现工具集。当用户需要生成 AI 绘图提示词、描述图片需求、从参考图反推提示词，或可能想根据 openspec change 下 art/ 子目录里的需求和提示词直接生成图片时使用。覆盖类型：技能/Buff 图标（ICON）、角色立绘（CHARACTER）、场景图（SCENE）、UI 元素（UI）、以及从图片反推提示词。触发场景：用户提到'美术风格'、'初始化美术风格'、'绘图提示词'、'画一个'、'图标设计'、'角色立绘'、'场景图'、'参考图分析'、'反推提示词'、'实现和处理美术素材'、'把美术需求出图'、'按提示词生成素材'、'处理美术资源'等。"
---

# AI Art — AI 绘图提示词生成

## 子技能列表

| 子技能 | 适用场景 | 参考文件 |
|--------|---------|---------|
| **drawing-prompt-generator** | 总入口/路由器：根据绘图需求自动判断类型并调用子模块 | `references/drawing-prompt-generator.md` |
| **drawing-prompt-CHARACTER** | 角色立绘提示词：根据角色职业、外貌、性格生成立绘/头像提示词 | `references/drawing-prompt-CHARACTER.md` |
| **drawing-prompt-ICON** | 技能/Buff 图标提示词：根据技能机制和效果描述生成图标提示词 | `references/drawing-prompt-ICON.md` |
| **drawing-prompt-SCENE** | 场景图提示词：根据场景类型、地域风格、氛围生成背景提示词 | `references/drawing-prompt-SCENE.md` |
| **drawing-prompt-UI** | UI 元素提示词：根据 UI 组件类型生成按钮、面板、边框等提示词 | `references/drawing-prompt-UI.md` |
| **drawing-prompt-COMMON** | 通用素材提示词：子弹、道具、树木、岩石、特效、载具等不属于上述类型的 2D 素材 | `references/drawing-prompt-COMMON.md` |
| **image-to-prompt-generator** | 从参考图片逆向分析，反推出 AI 绘图提示词（输出 JSON） | `references/image-to-prompt-generator.md` |

## 使用流程

1. 先读取 `references/drawing-prompt-generator.md`（总入口），了解分类判断逻辑
2. 根据用户描述的绘图需求类型，读取对应子模块的参考文件
3. 如果用户提供了参考图片要反推提示词，读取 `references/image-to-prompt-generator.md`

## 选择指南

- 用户要**生成提示词**（描述需求） → 先读总入口，再按类型读子模块
- 用户提供**参考图片**要反推 → image-to-prompt-generator
- 用户明确输入**"实现和处理美术素材"** → 执行下方"美术素材实现流程"（输出到 `openspec/changes/<change-name>/art/raw/`）
- 用户表达相近意图（如"把美术需求出图"、"按提示词生成素材"、"处理美术资源"） → 先询问是否按"实现和处理美术素材"流程执行
- 不确定类型 → 读总入口，它会帮你判断
- **需要从现成风格库 / 行业模板里挑视觉样式**（用户提到"换个风格试试"、"有哪些风格可选"、"参考某行业的提示词模板"、"按 GPT-Image2 风格出图"等）→ 切到 [`gpt-image-2-style-library`](../gpt-image-2-style-library/SKILL.md)，它内置 awesome-gpt-image-2 风格库 JSON + 行业模板分类，本 SKILL 不提供这一层资源

## 美术素材实现流程

当用户明确输入 **"实现和处理美术素材"**，或对相近意图确认要执行该流程后：

> **⚠️ UI 类型前置约束**：UI 类型素材（按钮 / 面板 / HUD / 弹窗 / 标签页 / 输入框 / 进度条 / 标题条 等）走 **结构先行流程 v3**（见 [CLAUDE.md §六「UI 制作子流程」](../../CLAUDE.md)）。本 SKILL 只承担 UI 流程的**阶段 2（写效果图 prompts.md）+ 阶段 3（生 mockups）**两步；**阶段 1 的 `prefab-layout.md` 必须由 `art-ui` 用 `unity-rect-transform` SKILL 先产出**，本 SKILL 不再要求"三表"。若接到 UI 出图指令时 `art/prefab-layout.md` 缺失 → 阻塞并交回主对话（不能自己起草结构）。
>
> CHARACTER / ICON / SCENE / COMMON 四种非 UI 类型不受此约束，直接进 Step 1。

### Step 1：定位 change 与读取需求

1. 定位当前 openspec change：
   - 优先：执行 `openspec status` 找 active change（同一时刻只有 1 个合法）
   - 次选：从用户上下文（最近提到的 change-name）推测
   - 最次：询问用户「目标 change 是哪个？」
2. 读取 `openspec/changes/<change-name>/art/requirements.md` 和 `art/prompts.md`。多模块需求按 prompts.md / requirements.md 中的 `<子模块名>` 字段定位。
3. 处理前检查 `art/requirements.md` 与 `art/prompts.md` 文件头部是否已有 `美术素材状态: 已处理` 或 `美术素材状态: 已归档`。若已存在，先提示用户该需求可能已处理过，并询问是否重新生成。**UI 类型还要检查 `art/prefab-layout.md` 是否已由 art-ui 产出且用户已确认；layout 缺失 → 阻塞交回主对话，不得进入 Step 4。**
4. 解析每个资源的：
   - 资源名/文件名
   - 类型（角色、敌人、子弹、UI、背景、道具等）
   - **尺寸 / 格式 / 文件大小预算**：必须按 [美术资源规范.md](../../美术资源规范.md) §二 对应视觉类别确定（如 UI 小图标 → 256×256 PNG ≤ 50KB；角色全身立绘 → 1024×2048 PNG ≤ 500KB；场景背景 → 1920×1080 JPEG ≤ 800KB）。**生产即合规**：把规范上的 Max 尺寸直接作为出图工具的目标分辨率参数，让产出的图绝大多数本身就达标，避免事后再走 image-compression 兜底。
   - 透明背景、风格一致性、负面提示词
   - 是否需要多个候选图
5. 如果当前是 GPT 系列模型且可用绘图模型/图像生成工具，必须调用绘图模型逐项生成图片。
6. 输出目录固定为：
   - 单模块：`openspec/changes/<change-name>/art/raw/`
   - 多模块：`openspec/changes/<change-name>/art/raw/<子模块名>/`
7. 图片保存命名：
   - 优先使用需求或提示词里的明确文件名
   - 否则使用 `序号-资源名.png`
   - 多候选使用 `资源名_v01.png`、`资源名_v02.png`
   - 禁止保留 `ChatGPT Image ...png` 等绘图工具默认文件名
8. 同 `art/raw/` 目录写入 `生成记录.md`，记录每张图的资源名、来源提示词摘要、输出文件名、生成状态、后续处理建议。
9. 生成完成后，在对应 `art/requirements.md` 和 `art/prompts.md` 文件头部写入或更新：`美术素材状态: 已处理`、`处理日期: YYYY-MM-DD`、`输出目录: art/raw/`、`生成记录: art/raw/生成记录.md`。
10. 如果无法调用绘图模型，或工具不能把生成结果保存到本地路径，必须明确告诉用户阻塞点，不得伪造文件或声称已保存。

## 美术风格初始化流程

`references/drawing-prompt-{CHARACTER,ICON,SCENE,UI,COMMON}.md` 中的"核心设计理念"等风格相关内容默认**留空（待初始化）**，因为美术风格因项目而异。当用户表达"初始化美术风格"、"确定/设置项目美术风格"等意图时，执行以下流程：

### 0. 前置：读取项目已有设计文档

启动流程前，**必须**先扫描并阅读项目已有的设计文档（`开发文档/`、`docs/`、`README.md`、`项目知识库（AI自行维护）/`、`openspec/changes/*/proposal.md` 等），提取以下信息作为问答的上下文与默认值：

- 游戏类型 / 视角 / 单局结构
- 已锁定的色彩 / 元素映射（如"颜色 ↔ 元素"对照表）
- 已表态的风格倾向 / 候选风格 / 参考游戏
- 平台与性能约束（PC / 移动端 / Solo 开发等）

**不要**在没读项目文档的情况下直接弹窗，否则选项会脱离项目实际。

### 1. 弹窗式分轮问答（核心规则）

**必须优先使用 `AskUserQuestion` 工具**做选项题，仅在需要描述性输入（如品牌色 HEX、参考图描述）时才回退到文字提问。

**每轮调用约束**：
- 一次 `AskUserQuestion` 最多 4 个问题，**只放强相关的问题在同一轮**（如「明度 + 饱和度」一轮，「角色比例 + 服饰细节」一轮）
- 每个问题 2-4 个选项（工具上限）
- 选项设计三原则：
  1. **推荐项放第一位**，label 末尾加 `(推荐)`，description 说明为什么推荐（要结合上一步读到的项目约束）
  2. 每个选项都要有 `description` 说明 trade-off / 适用场景
  3. 视觉对比类问题（风格大类、色板、布局）**优先用 `preview` 字段**放 ASCII 示意（色块/构图/字符画），单选题才能用 preview
- 用户选了「Other」或描述模糊时，**回退到一次普通文字追问**澄清

**推荐问答顺序**（从粗到细，逐轮收敛；某些轮次若项目无该需求可跳过）：

| 轮次 | 内容 | 工具用法 |
|---|---|---|
| **轮 1** | 整体风格大类（4 选 1，preview 放风格示意）+ 参考游戏（multiSelect） | 1 个单选 + 1 个多选 |
| **轮 2** | 明度倾向（3 选 1）+ 饱和度倾向（3 选 1），preview 放色板对比 | 2 个单选 |
| **轮 3**（若有 CHARACTER） | 角色比例 + 服饰/材质特征 | 2 个单选 |
| **轮 4**（若有 SCENE） | 场景光影氛围 + 典型场景类型 | 2 个单选 |
| **轮 5**（若有 UI） | UI 整体质感 + 装饰元素繁简度 | 2 个单选 |
| **轮 6**（若有 ICON / COMMON） | 图标质感 + COMMON 素材繁简度 | 2 个单选 |

**每轮答完后**：
1. 用一句话总结本轮锁定的决策（不要长篇复述）
2. 进入下一轮前**不需要**额外的"是否继续"弹窗，直接弹下一轮即可
3. 全部轮次结束后，再用一个**总结弹窗**（单选：「确认落地 / 调整某一轮 / 取消」）锁定最终决策

### 2. 更新参考文件

风格确定后，更新对应的 `references/drawing-prompt-{CHARACTER,ICON,SCENE,UI,COMMON}.md`：

- 将"核心设计理念"中的"待初始化"占位说明，替换为实际的风格定位描述
- 按类型补充对应的视觉语言表/分类表/色彩体系（CHARACTER 的职业视觉语言表、ICON 的效果-色彩映射表、SCENE 的场景类型分类表、UI 的元素分类表、COMMON 的常见素材类型推理示例）
- 将"提示词结构"模板中的 `[项目美术风格关键词]`、`[主题描述]` 等占位符替换为符合实际风格的具体描述
- 补充 1~2 个推理示例，供后续生成提示词时参考

### 3. 中途澄清也走弹窗

过程中如遇到风格描述模糊、信息冲突，或拿不准如何转化为具体视觉语言，**优先用 `AskUserQuestion` 弹窗追问**（给出 2-4 个具体的解决方向作为选项）；仅当问题完全开放、无法枚举选项时才用文字追问。不要自行猜测后直接写入参考文件。
