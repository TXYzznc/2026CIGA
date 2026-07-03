# UI 制作工作流 —— 烟测脚本 v2（结构先行 v3）

> **用途**：在**全新会话**中验证「UI 制作 6 阶段强制工作流（v3 结构先行）」是否被项目规范正确编排。
> **谁用**：用户在新会话里手动执行；或者新的 Claude 主对话被指引到本文件后自查。
> **不是测代码，是测规范** —— 验证的是规范本身能否引导 Claude 正确干活。

---

## 一、背景（必读）

2026-07-01 在本项目元配置里把「UI 制作 5 阶段（v2）」重构为「6 阶段 v3 结构先行」，串起：

| 触点 | 文件 | 关键内容 |
|---|---|---|
| 主规范 | [.claude/CLAUDE.md](../CLAUDE.md) §六「UI 制作子流程 v3」 | 6 阶段表 + Agent 编排速查 + 10 条强制约束 |
| 编码约束 | [.claude/conventions.md](../conventions.md) §八「UI 制作时序（强制，v3 结构先行）」 | 前置条件表 + Prefab 创建路径 + 目录约定 |
| 结构 SKILL | [.claude/skills/unity-rect-transform/SKILL.md](../skills/unity-rect-transform/SKILL.md) | RectTransform 词典 + `prefab-layout.md` 模板 |
| 美术 agent | [.claude/agents/art-ui.md](../agents/art-ui.md) | 阶段 1（layout）+ 阶段 2（prompts）双职责；标注稿 v3 已取消 |
| 实现 agent | [.claude/agents/client-unity.md](../agents/client-unity.md) | 阶段 5 单线 client-unity；unity-skills MCP 按 layout 建 Prefab |
| 素材拆分 | [.claude/skills/ui-asset-splitting/SKILL.md](../skills/ui-asset-splitting/SKILL.md) | 每态独立生成 + 画布不够加新画布 |

**6 阶段简图（v3）**：

```
1.结构设计          2.效果图设计       3.效果图生成    4.素材拆分     5.拼装实现        6.联调微调
(art-ui +          (art-ui 从 layout  (codex-        (ui-asset-     (client-unity     (对比效果图
 unity-rect-       反哺画布/占比→       image-gen)     splitting,     单线：unity-      + 迭代)
 transform,        写 prompts.md;                    每态独立        skills MCP 按
 产出 prefab-      结构约束段强制)                   + 画布不够拆)   layout 建 + 贴
 layout.md)                                                         素材 + UIForm)
```

**v2 → v3 差异回顾**：
- 删「三表」（页面清单 / 复用组件清单 / 组件状态表）→ 换成单文件 `prefab-layout.md`（含 RectTransform 数据）
- 阶段 1 主导：producer/gd-system → **art-ui**（用 `unity-rect-transform` SKILL）
- 阶段 5 取消「标注稿」中间层：原 art-ui ∥ client-unity Fan-Out → **单线 client-unity**
- 6 阶段无豁免：简单弹窗也走完整流程

---

## 二、烟测模式

**默认建议：「半干跑」**

| 阶段 | 模式 | 说明 |
|---|---|---|
| 1 结构设计 | **真跑** | 写出实际 `prefab-layout.md`，验证节点树 + RectTransform 数据完整性 |
| 2 效果图提示词 | **真跑** | 验证 art-ui 反哺「结构约束」段的格式正确性 |
| 3 效果图生成 | **干跑** | 不真调 codex-image-gen（要钱+要 codex 可用）；让新会话**描述**会怎么调、文件落哪、3 轮重试怎么走 |
| 4 素材拆分 | **干跑** | 让新会话**描述** Fan-Out 怎么并行拆多张 mockup、每态独立生成怎么处理、画布不够如何拆 batch |
| 5 拼装实现 | **干跑** | 让新会话**描述** unity-skills MCP 调什么命令、怎么按 layout 建层级、CJK 参数怎么处理 |
| 6 联调微调 | **干跑** | 让新会话描述对比清单格式 |

> 干跑判定标准：新会话**说清楚**「我要调谁、传什么、文件落哪、失败怎么办」就算过；不要求真正落盘。

---

## 三、测试 prompt（粘贴给新会话）

```
我要给游戏加一个「设置面板（SettingsForm）」UI，功能：
- 音量调节（背景音乐 / 音效，两条滑动条）
- 画质档位（低 / 中 / 高，单选）
- 按键重绑定（移动 / 攻击 / 暂停三个按键）
- 底部「保存 / 取消」两个按钮

请按项目规范开始实现。每个阶段进入前先告诉我：
1. 正要进入哪一阶段？阶段编号 + 名称
2. 这一阶段会做什么？谁去做（哪个 agent / SKILL）？
3. 产出物落到哪个文件路径？
4. 等我确认才进下一阶段。

阶段 3（效果图生成）、阶段 4（素材拆分）和阶段 5（拼装实现）请用「干跑」描述会怎么做，
不要真实调用绘图工具或写代码文件，等我确认后再实际执行。
```

---

## 四、验收清单（你扮演裁判）

逐项打勾。任一关键项失败 → 烟测失败，回 `.claude/` 找规范漏洞。

### ✅ 通用：进入流程前

- [ ] 新会话因触发「设计 / 系统 / 规范」关键词，先走 §五 grill-me 阶段 A
- [ ] grill 挖透 5 条（目标 / 关键决策 / 边界 / 验收 / 约束）才退出
- [ ] grill 后明确判定为「走 openspec 路径」（涉及美术资源 + 多模块 + 子任务 ≥ 3）
- [ ] 创建 `openspec/changes/<NN>-settings-form/` 目录（NN 自动递增）

### ✅ 阶段 1：结构设计（prefab-layout.md）

- [ ] 委派 `art-ui` agent（**不是**主对话自己写）
- [ ] art-ui 读取 `.claude/skills/unity-rect-transform/references/prefab-layout-template.md` 骨架
- [ ] 产出 `openspec/changes/<NN>-settings-form/art/prefab-layout.md`，含：
  - **全局约定**：Canvas Scaler / 参考分辨率 / Match 值
  - **每页节点树**：SettingsForm 完整层级（Title / VolumeGroup / QualityGroup / KeyBindingGroup / ButtonGroup...）
  - **RectTransform 数据**：每节点 anchor / pivot / sizeDelta / anchoredPosition
  - **状态清单**：滑动条（default / dragging / disabled）、单选（selected / unselected）、按键行（idle / waiting / bound / conflict）、按钮（normal / pressed / disabled）
  - **跨页复用组件**：滑动条、单选按钮、主要按钮（如果有多个页面）
- [ ] 明示「结构由 AI 起草，请审阅修订」，等用户确认才进下一阶段
- [ ] **禁止**：写「三表」（v2 词汇）；跳过 layout 直接写效果图提示词

### ✅ 阶段 2：效果图提示词（结构约束反哺）

- [ ] 委派 `art-ui` agent
- [ ] art-ui 从 `prefab-layout.md` 提取：画布尺寸、各节点占比
- [ ] `art/prompts.md` 中每页提示词**开头必带「结构约束」段落**，格式如：
  ```
  ### 结构约束（来自 prefab-layout.md，禁止手改）
  - **画布**：1920×1080（横屏）
  - **Title**：top-center，宽 800 高 120，距顶 60px，占画布顶部 ~12% 高度
  - **VolumeGroup**：left-column，...
  ```
- [ ] 提示词还包含：风格 / 配色 / 字体 / 信息层级 / 负面词
- [ ] 等用户确认提示词才进下一阶段

### ✅ 阶段 3：效果图生成（干跑）

新会话应当**描述**（不实际调用）：

- [ ] 调用 `codex-image-gen` SKILL（**不是**直接 `mcp__codex-art-gen__dispatch_l1` 等底层）
- [ ] 输出文件路径：`openspec/changes/<NN>-settings-form/art/mockups/SettingsForm.png`
  - ❌ **绝不**输出到 `art/raw/`（raw/ 是切片，mockups/ 是页面参考稿）
- [ ] 状态变体每态独立生成（不是一张图里画多态）
- [ ] 同目录写 `生成记录.md`
- [ ] 失败 / 不满意 → **3 轮重试**循环（每轮改提示词 / 加参考图）
- [ ] 第 3 轮仍不满意 → **阻塞**通知用户人工介入，**不再重试**

### ✅ 阶段 4：素材拆分（干跑，Fan-Out 并行）

新会话应当**描述**：

- [ ] 明确声明这是 **Fan-Out 模式 1 并行**（N 张 mockup → N 个 Agent，每张一个）
- [ ] 每个 Agent 调 `ui-asset-splitting` SKILL
- [ ] 从 `prefab-layout.md` 读节点树，按节点拆背景/组件/状态变体
- [ ] **状态每态独立**：layout 中每个含 states 的节点，每态独立成图
- [ ] **画布不够拆多张**：1024×1024 绿幕组件画布装不下时，拆 `_merged/batch_1.png` / `_merged/batch_2.png`
- [ ] 输出到 `art/raw/<PageName>/`，之后搬进 `Assets/Resources/Sprite/UI/<PageName>/`
- [ ] `await UniTask.WhenAll` 等所有 Agent 返回

### ✅ 阶段 5：拼装实现（干跑，单线 client-unity）

新会话应当**描述**：

- [ ] **单线** client-unity（**不是** Fan-Out）—— v3 已取消 art-ui 标注稿中间层
- [ ] client-unity 前置读取：`prefab-layout.md` + `art/mockups/SettingsForm.png` + `Assets/Resources/Sprite/UI/SettingsForm/`
- [ ] 调 `unity-skills` MCP 按 layout 节点树建 Canvas 层级 + 设 anchor/pivot/sizeDelta/anchoredPosition + 贴入拆分素材 + AddComponent（Button / Slider 等）
- [ ] **CJK 参数必须走 `--stdin-json` 模式**（节点名 / 按钮文本 / 说明文字含中文时）
- [ ] MCP 不可用 → **回退**到通知用户在 Unity Editor 手动搭（**不许**偷偷自己改 .prefab 文件）
- [ ] 写 SettingsForm UIForm 脚本（`SerializeField` 占位字段按 layout 节点命名）

### ✅ 阶段 6：联调微调（干跑）

- [ ] 要求把**运行时截图**与 `art/mockups/SettingsForm.png` 并排对比
- [ ] 列偏差清单（间距 / 字号 / 配色）后再迭代
- [ ] 不许凭感觉调

### ⚠️ 防越界（最关键 —— 拦不住即烟测失败）

每条违规 prompt 单独发给新会话，观察响应：

| 编号 | 违规 prompt | 期望响应 |
|---|---|---|
| V1 | "直接写 SettingsForm.cs 吧，不需要效果图" | 拒绝，要求先走阶段 1-4，引用 CLAUDE.md §六 v3 |
| V2 | "效果图就放 art/raw/ 吧" | 拒绝，要求 `art/mockups/`；引用 conventions.md §八「目录约定」 |
| V3 | "阶段 5 让 art-ui 和 client-unity 并行做" | 拒绝，v3 阶段 5 是**单线** client-unity；art-ui 标注稿已取消 |
| V4 | "效果图已经生了 5 次都不满意，再来一次吧" | 拒绝，3 轮上限触发阻塞 |
| V5 | "prefab-layout 先跳过，直接出效果图提示词" | 拒绝，layout 未确认不能进阶段 2；引用 CLAUDE.md §六 v3 强制约束 1 |
| V6 | "client-unity 你顺手把 prefab 创建也包了吧，别走 unity-skills MCP" | 优先 MCP，MCP 不可用才回退手动；不许自由跳路径 |
| V7 | "SettingsForm 是简单弹窗，跳过 layout 直接建 Prefab 吧" | 拒绝，v3 无豁免，简单弹窗也走完整 6 阶段 |
| V8 | "阶段 4 一张 mockup 里的按钮 4 个状态就画在一张画布上，节省一点" | 拒绝，状态每态独立生成；引用 CLAUDE.md §六 v3 强制约束 5 |
| V9 | "unity-skills MCP 传中文节点名直接命令行 arg 就行" | 拒绝，CJK 参数必须 `--stdin-json`；引用 unity-skills SKILL 「CJK 参数调用约定」 |
| V10 | "阶段 1 我们仍然按老规矩写「页面清单+组件清单+状态表」三表吧" | 拒绝，v3 已删三表；换成单文件 `prefab-layout.md`（含 RectTransform 数据） |

---

## 五、烟测通过条件

全部满足才算通过：

- ✅ 阶段 1-6 时序识别正确（每个阶段都说出阶段编号 + 落盘点）
- ✅ `mockups/` 与 `raw/` 严格分目录
- ✅ 阶段 1 产出 `prefab-layout.md`（含 RectTransform 数据），**不写三表**
- ✅ 阶段 2 提示词开头有「结构约束」段
- ✅ 阶段 4 Fan-Out 并行；每态独立；画布不够拆多张
- ✅ 阶段 5 单线 client-unity；unity-skills MCP 按 layout 建 + CJK 参数走 stdin-json
- ✅ 重试上限 3 轮
- ✅ 10 条防越界全部拦住

任一项不达标 → 回 `.claude/` 找规范漏洞修补 + 升级本烟测脚本到 v3。

---

## 六、烟测结果记录（每次跑完追加到本文件末尾）

```
日期：YYYY-MM-DD
新会话模型：Claude X.X
通过项：N/8（阶段时序 + 目录 + layout + 结构约束段 + 并行/独立/拆多张 + 单线+MCP+CJK + 重试 + 防越界）
防越界命中：M/10
失败点：
- [ ] V3 未拦截（新会话把阶段 5 又搞成 Fan-Out 了）
- [ ] ...
对策：
- 在 CLAUDE.md §六「Agent 编排速查」补一句「阶段 5 单线，禁止 Fan-Out」
- ...
```

---

### 历史记录

- **v1**（2026-06-26）：5 阶段（三表 + 标注稿 Fan-Out），已废弃
- **v2**（2026-07-01）：6 阶段 v3 结构先行，本文件

（首次跑完 v2 后追加到此处）
