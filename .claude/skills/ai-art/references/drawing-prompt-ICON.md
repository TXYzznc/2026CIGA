
# 技能图标提示词生成器

## 核心设计理念

### 项目美术风格 —「黑暗史诗手绘肉鸽」

ICON 用于技能槽位、Buff/Debuff 显示、纹身配方栏、纹身师商品列表等场景。所有 ICON 共享统一调性：

- **质感**：厚涂手绘主体 + 7 元素色发光描边 + 金属边框
- **金属边框**：兼任稀有度标识（铁 = 普通 / 铜 = 优 / 银 = 稀 / 金 = 史诗 / 紫黑 = 传说）
- **构图**：被动技能内向收敛，主动/大招外向爆发，纹身配方居中对称
- **背景**：图标内部留 60-70% 视觉主体 + 25-30% 简化能量场背景

### ICON 类型与色彩

| 类型 | 用途 | 边框 | 主色来源 | 示例 |
|------|------|------|--------|------|
| **纹身技能图标** | 已刻纹身槽位 | 金属边框（颜色 = 稀有度） | 7 元素色之一 + 发光 | tattoo_fire_chest |
| **纹身配方图标** | 配方列表/掉落物 | 木质卷轴边框 | 元素主色 + 暗色卷轴 | recipe_fire_burst |
| **Buff/Debuff 图标** | HUD 状态栏 | 细金边或无边 | Buff 同元素色 / Debuff 暗化 | buff_haste |
| **武器图标** | 装备槽 | 钢铁边框 | 灰色金属 + 武器特征色 | weapon_axe |
| **颜料/资源图标** | 库存 | 简单金边 | 元素色为主 | pigment_red |
| **大招图标** | 终极技能 | 紫黑传说边框+辉光 | 高饱和元素色 + 强烈光晕 | ultimate_inferno |

### 元素色板（与 CHARACTER §色彩体系-元素色板一致）

```
🔴 #ff3030  🟡 #ffe838  🟢 #38ff5c  🔵 #00d4ff  🟣 #b838ff  🟨 #ffd700  ⚪ #ffffff
```

### 稀有度边框色板

```
普通 #8a8a8a 铁     优 #cd7f32 铜     稀 #c0c0c0 银
史诗 #d4af37 金     传说 #4a1a4a 紫黑（带辉光）
```

### 设计原则

1. **0.5 秒识别**：玩家瞄一眼就能判断属性元素（颜色）和稀有度（边框颜色）
2. **构图体现机制**：单体技能内向、群体技能外向、被动收敛、大招爆发
3. **统一边框系列**：金属边框是同一套模板换色（铁/铜/银/金/紫黑），不要每个稀有度重新设计
4. **不要靠文字**：纯视觉就能表达「火 / 冻结 / 治疗」，避免在图标内塞文字

### 项目美术风格关键词集合（提示词模板填充用）

> 下方 `[项目美术风格关键词]` 占位符使用：
```
dark epic painterly hand-drawn icon, heavy brushwork main subject, glowing
[元素色] rim light, ornate metal frame border indicating rarity tier,
reference style of Diablo IV and Hades skill icons
```

> 下方 `[主题描述]` 占位符使用：
```
ability/buff icon for post-apocalyptic roguelike, elemental tattoo magic system,
icon at 256x256 resolution for in-game HUD use
```

### 推理示例

**需求**：「火元素胸口纹身技能图标，稀有度史诗」
**推理**：胸口纹身 → 居中对称构图 → 火元素红色 → 史诗 → 金边 → 厚涂手绘 + 发光
**提示词关键字**：
```
dark epic painterly hand-drawn icon, centered symmetrical composition of glowing
flame motif on a chest tattoo silhouette, heavy brushwork with high-saturation
#ff3030 red fire as main subject, glowing red rim light, ornate gold metal frame
border (#d4af37) indicating epic rarity, dark smoky background filling outer 30%,
256x256 resolution, reference Diablo IV skill icon style
```

---

## 提示词生成方法

生成图标提示词时，建议按以下步骤推理（根据具体需求分析，不依赖预设模板）：

1. **信息收集** — 技能名称、技能类型（被动/普攻/技能/大招）、所属职业或角色、效果描述、特殊机制
2. **职业/角色推理** — 根据职业或角色设定推断视觉语言和色彩倾向（需依据已初始化的项目美术风格）
3. **效果推理** — 从效果描述中提取关键词（如灼烧、冻结、击退等），转化为视觉元素和色彩
4. **背景推理** — 根据效果类型、职业氛围、技能规模（被动简洁 / 大招宏大）组合背景元素
5. **构图推理** — 根据技能类型确定构图方式（中心对称/爆发式/动感方向等）和光效方向
6. **提示词组织** — 按照下方模板组织最终提示词

---

## 提示词结构

```
[分辨率] [风格] [职业/角色特征] [主体描述]
Color palette: [色彩代码]
[光效描述]
Background: [背景描述]
[质感和细节]
No border, no frame, no rounded corners, full bleed image filling the entire canvas
Professional game art style, [主题描述]

请生成真正透明背景的 PNG 图片。

要求：
1. 背景必须是真实透明通道，也就是 PNG RGBA 的 alpha=0。
2. 不要白底、不要灰底、不要棋盘格、不要"模拟透明背景"。
3. 画面中只能保留主体图形本身，主体外所有区域必须完全透明。
4. 生成后请用程序检查图片是否为 RGBA，并验证四个角落像素 alpha=0。

这个提示词能保证AI绘制的图片是真正的PNG格式图片。
```
