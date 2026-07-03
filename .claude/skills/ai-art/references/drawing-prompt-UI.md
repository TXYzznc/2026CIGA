
# UI 元素提示词生成器

## UI 出图前置：结构先行（强制）

> **v3 变更（2026-07-01）**：从「先定三表」改为「先定 `prefab-layout.md`」。三表容易变成 checklist 摆设，而 layout 表既是效果图长宽依据、又是拼 Prefab 依据，一份文档同时喂养 3、5、6 三个阶段。

### 前置门槛（本文件只负责阶段 2、3）

- **阶段 1**：art-ui 使用 `unity-rect-transform` SKILL 产出 `openspec/changes/<NN-name>/art/prefab-layout.md`（含 RectTransform 数据），用户已确认。
- **阶段 2（本文件）**：读取 `prefab-layout.md` 中的画布尺寸 + 各组件占比，把「结构约束」写进 `prompts.md` 的效果图提示词。
- **阶段 3**：`codex-image-gen` 依 `prompts.md` 出图到 `art/mockups/`。

**若 `art/prefab-layout.md` 缺失或用户未确认 → 立即阻塞，交回主对话**，让主对话按 [CLAUDE.md §六 UI 制作子流程 v3](../../../CLAUDE.md) 从阶段 1 开始重新走。**禁止**在没有 layout 的情况下起草任何三表或直接开始写提示词。

### 结构长宽反哺（强制格式）

`prompts.md` 中每个页面的效果图提示词**必须**在开头包含 `结构约束` 段落，字段直接从 `prefab-layout.md` 提取：

```markdown
## <PageName>Form 效果图提示词

### 结构约束（来自 prefab-layout.md，禁止手改）
- **画布**：1920×1080（横屏）
- **Title**：top-center，宽 800 高 120，距顶 60px，占画布顶部 ~12% 高度
- **ButtonGroup**：middle-center，容器 400×320，内含 3 个 320×80 按钮堆叠，间距 20px
- **Background**：stretch-all，1920×1080 铺满
- **其他节点**：<按 layout 节点树逐条列出，含 anchor / sizeDelta / 大致占比>

### 视觉风格
- **主题**：<主题描述>
- **配色**：<主色/辅色/强调色 HEX>
- **字体**：<字体族 / 字号层级>
- **氛围**：<厚涂 / 扁平 / 未来 / 复古 等>

### 效果图额外约束（不影响结构）
- Aspect ratio: 1920x1080
- 保持组件位置与结构约束一致；不允许自行调整版式
- <正面词 / 负面词>
```

**目的**：让效果图的画布长宽、组件占比与最终 Prefab 完全一致 → 阶段 6 联调时不会出现「效果图 16:9 但 Prefab 4:3」这种硬伤，也避免效果图里的按钮位置与 layout 打架。

### 提取规则（art-ui 阶段 2 写 prompts.md 时执行）

1. **画布** → 直接抄 layout 的「全局约定 · 画布基准分辨率」
2. **各节点** → 按 layout 中节点树顺序，逐条抽取 anchor / sizeDelta / anchoredPosition，转成人类语言（`sizeDelta: (800, 120)` → `宽 800 高 120`；`anchoredPosition: (0, -60)` → `距顶 60px`）
3. **占比** → 用 sizeDelta / 画布尺寸算大致百分比，帮助模型理解视觉权重（如 `120/1080 ≈ 11%` 高度）
4. **状态** → layout 里每个节点若有 `states: [normal, pressed, disabled]`，在 prompts.md 里给该节点单独一条状态说明；每态在阶段 3 独立出一张图（不要一张图画多态）
5. **画布不够就加新画布** → 若一张 1920×1080 mockup 装不下当前页所有元素（如商店有大量商品列表），拆成 `<PageName>_part1.png` / `<PageName>_part2.png`，每张画布独立 1920×1080

### 状态变体处理

`prefab-layout.md` 的「状态清单」章节列出所有需要状态变体的节点。阶段 2 写 prompts.md 时：

- **每态一条提示词条目**：`按钮 StartBtn (normal 态)` / `按钮 StartBtn (pressed 态)` / `按钮 StartBtn (disabled 态)` 三条独立
- **禁止**把三态放同一张图里让模型生 3 个变体（模型内部一致性差，拆分素材时也难切）
- 阶段 3 分别调 `codex-image-gen` 生 3 张，各自命名 `StartBtn_normal.png` / `StartBtn_pressed.png` / `StartBtn_disabled.png`

---

## 核心设计理念

### 项目美术风格 —「黑暗史诗手绘肉鸽」

- **质感**：厚涂手绘 + 史诗感（**不是扁平 SVG**），有油彩笔触、有金属/木质质感
- **核心组件**：金属边框（外）+ 木质 / 兽皮 / 羊皮纸内衬 + 厚涂主体
- **装饰繁简**：中等装饰，边框四角点缀末日纹样
- **边框按主题切换**：进 AI 区边框带机械齿轮、外星区带生体触手、病毒区带真菌肿块、默认中性带金属铆钉

### UI 元素分类与色彩体系

| 元素类型 | 用途 | 构成 | 主色 | 备注 |
|---------|------|------|------|------|
| **HUD 血条** | 屏顶血量 | 厚涂深红填充 + 金属边框 | 红 #c8302e + 金 #d4af37 | 可 9-slice |
| **HUD 技能槽** | 屏底 4 个技能 | 金属圆角槽 + 厚涂图标 | 暗褐 #3a2418 + 金边 | 槽内嵌 ICON |
| **缩圈倒计时** | 屏顶 | 卷轴底 + 厚描数字 | 羊皮纸 #d4b896 + 暗红字 | 史诗感 |
| **暂停菜单面板** | 全屏 | 大幅羊皮纸 + 四角金属装饰 | 羊皮纸 + 暗红血字 | 史诗 |
| **纹身师工作室面板** | 多面板布局 | 木质底 + 铜钉 + 人体图鉴 | 暗木 #4a2818 + 铜 #b87333 | 重头戏 |
| **死亡宝箱面板** | 拾取交互 | 金属边框 + 物品格子 | 暗灰 + 物品稀有度光 | 紧凑 |
| **构筑预览（远观）** | 看其他玩家纹身 | 描边轮廓 + 发光纹身位置 | 透明底 + 纹身元素色 | 不挡视野 |

### 主题边框三套（同一形状不同元素）

| 主题 | 边框装饰 | 颜色调整 |
|------|--------|---------|
| **AI 区** | 四角机械齿轮、铜管贯穿 | 金边偏冷青 #4a8aae |
| **外星区** | 四角生体触手、紫色裂纹 | 金边偏紫 #8a4a8e |
| **病毒区** | 四角真菌肿块、孢子飞散 | 金边偏黄绿 #8aae4a |
| **默认/中性** | 四角金属铆钉、磨损划痕 | 标准金 #d4af37 |

### 设计原则

1. **信息密度优先 + 装饰只在四角**：BR 节奏下玩家要快速读 HUD，不能被装饰干扰
2. **9-slice 友好**：所有面板/按钮的边框设计为四角 + 四边可拉伸
3. **移动端可读**：最小可视尺寸下，文字 + 图标 + 边框都还能看清
4. **史诗感来自材质而非繁复**：厚涂笔触、金属反光、木纹质感 → 史诗感；不需要堆装饰
5. **状态完整**：按钮三态 Normal / Hover / Pressed 必须明显区分

### 项目美术风格关键词集合（提示词模板填充用）

> 下方 `[项目美术风格关键词]` / `[项目 UI 美术风格关键词]` 占位符使用：
```
dark epic painterly hand-drawn UI element, heavy brushwork, ornate metal frame
border with parchment or wood inset, four-corner decoration only,
9-slice tileable edges, reference style of Darkest Dungeon UI and Diablo IV UI
```

> 下方 `[主题描述]` 占位符使用：
```
UI for post-apocalyptic roguelike battle royale, epic dark aesthetic,
mobile-friendly minimum readable size, optional theme variant
(AI / alien / virus / neutral)
```

### 推理示例

**需求**：「暂停菜单面板，默认（中性）主题」
**推理**：暂停面板 → 全屏羊皮纸 → 四角金属装饰 → 厚涂史诗感
**提示词关键字**：
```
dark epic painterly hand-drawn UI panel, large parchment background with worn
leather texture, ornate metal frame border with four-corner decoration of rivets
and weathered scratches, standard gold trim #d4af37, 9-slice tileable edges,
heavy brushwork, dark red blood-letter title bar at top, designed for pause menu,
reference Darkest Dungeon UI style, 1920x1080
```

---

## 提示词结构

```
[分辨率] [UI 元素类型] [功能描述]
Style: [项目 UI 美术风格关键词]
Material: [材质描述：木质/石质/金属/水晶/扁平/玻璃拟态等]
Color palette: [色彩代码]
Decorative elements: [装饰元素描述]
State: [正常/高亮/按下/禁用 — 需要哪种状态]
Transparent background / [或具体背景色]
No border on the outside, internal decorative borders only
Clean edges suitable for Unity UI sprite slicing
Professional game UI art style, [主题描述]

请生成真正透明背景的 PNG 图片。

要求：
1. 背景必须是真实透明通道，也就是 PNG RGBA 的 alpha=0。
2. 不要白底、不要灰底、不要棋盘格、不要"模拟透明背景"。
3. 画面中只能保留主体图形本身，主体外所有区域必须完全透明。
4. 生成后请用程序检查图片是否为 RGBA，并验证四个角落像素 alpha=0。

这个提示词能保证AI绘制的图片是真正的PNG格式图片。
```

---

## 关键规范

- **透明背景**：UI 素材通常需要 `transparent background (PNG)`，方便 Unity 中直接使用
- **九宫格适配**：面板/按钮描述中加 `designed for 9-slice sprite (corners and edges tileable)`
- **尺寸清晰**：注明具体像素尺寸，如 `200x60 pixel button`
- **状态完整**：重要按钮最好说明需要 Normal/Hover/Pressed 三种状态
