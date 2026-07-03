
# 通用素材提示词生成器

## 适用范围

用于**不属于角色立绘（CHARACTER）、技能图标（ICON）、场景背景（SCENE）、UI 元素（UI）** 的其他 2D 游戏素材，例如：

- 子弹/弹幕（炮弹、激光、能量球等）
- 场景物体/道具（箱子、门、机关、收集品、武器掉落物等）
- 自然元素（树木、岩石、草丛、云朵等）
- 独立特效素材（爆炸、烟雾、光效粒子等，作为单张 Sprite）
- 非角色单位（飞船、坦克、炮塔等载具/单位）

## 核心设计理念

### 项目美术风格 —「黑暗史诗手绘肉鸽」

适用于子弹/子弹特效、武器掉落物、宝箱、地图道具（颜料瓶、配方卷轴）、植物（藤蔓、苔藓、真菌）、岩石/废墟碎片、载具（断电机器人、外星运输舱）等。

- **细节程度**：中等细节（不画细胞肌理，但物体装饰金属件/材质区分可见）
- **质感**：与角色统一的厚涂手绘 + 1-2px 黑色描边
- **配色规则**：物体本体用环境同调（土褐 / 灰），仅功能性元素（元素色子弹、稀有道具光晕）走高饱和

### 通用素材分类与色彩

| 素材类型 | 细节级别 | 配色来源 | 描边 | 发光 | 示例 |
|---------|--------|---------|------|------|------|
| **玩家子弹** | 极简（球 + 尾迹） | 7 元素色（武器属性） | 无 | 强 | bullet_fire |
| **敌人子弹** | 极简（球） | 暗红 / 暗紫 | 1px | 中 | enemy_bullet |
| **武器掉落物** | 中等（轮廓 + 材质区分） | 灰金属 + 元素色辉光 | 1-2px | 弱 | weapon_axe |
| **宝箱（普通/稀有/传说）** | 中等（木纹 + 铁件） | 暗木 + 铁皮 + 稀有度光 | 1-2px | 视稀有度 | chest_normal |
| **颜料瓶** | 中等（玻璃瓶 + 液体） | 暗灰玻璃 + 元素液体 | 1-2px | 中 | pigment_red |
| **植物（苔藓/真菌）** | 中等（叶片轮廓） | 病毒区黄绿 / 中性褐 | 1px | 无 | moss_patch |
| **岩石/碎片** | 中等（块面 + 裂纹） | 灰褐 | 1-2px | 无 | rubble |
| **机器人残骸** | 中等（铁件 + 电缆） | 锈铁 + 电火花点光 | 1-2px | 弱（火花点） | mech_husk |

### 元素色板（与 CHARACTER §色彩体系-元素色板一致）

```
🔴 #ff3030  🟡 #ffe838  🟢 #38ff5c  🔵 #00d4ff  🟣 #b838ff  🟨 #ffd700  ⚪ #ffffff
```

### 设计原则

1. **统一描边宽度**：与角色一样 1-2px 黑色，让所有元素融为一体
2. **环境物用环境色，功能物用元素色**：道具/装备的稀有度/属性靠「颜色 + 光晕」传达
3. **子弹纯靠光晕**：子弹不需要细节，只需要「元素色 + 拖尾发光」
4. **可复用模板**：同一类素材（如所有宝箱、所有颜料瓶）共享形状模板，仅换贴图/颜色

### 项目美术风格关键词集合（提示词模板填充用）

> 下方 `[项目美术风格关键词]` 占位符使用：
```
dark epic painterly hand-drawn game prop/object, medium detail, heavy brushwork,
bold black outline 1-2px, low-saturation environment-matching base color,
glowing accent color for functional items (elemental pigments, rare loot),
reference style of Darkest Dungeon props and Hades pickups
```

> 下方 `[主题描述]` 占位符使用：
```
prop/object for post-apocalyptic roguelike battle royale, top-down 2.5D view,
isolated on transparent background, size 64x64 or 128x128 sprite
```

### 推理示例

**需求**：「火元素玩家子弹」
**推理**：火元素 → #ff3030 → 球 + 尾迹 → 强发光
**提示词关键字**：
```
dark epic painterly hand-drawn projectile, glowing red #ff3030 energy sphere core
with bright white inner highlight, soft elongated comet tail trailing behind,
strong outer glow halo, no outline (pure energy), 32x32 sprite, isolated on
transparent background, reference Hades projectile style
```

---

## 提示词结构

```
[分辨率] [物体类型] [外观/形状描述], [项目美术风格关键词]
Shape: [轮廓/形状描述]
Color palette: [色彩代码]
Details: [细节程度，如 minimalist / highly detailed]
[可选：Lighting/glow 等光效描述]
Isolated on transparent background
Game sprite for [用途，如 bullet in vertical shooter / decorative prop / environment object]
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

---

## 通用规范

- **尺寸**：明确标注像素尺寸（如 `32x32`、`64x64`），与项目内同类素材保持比例一致
- **构图**：主体居中、占画面主要面积，避免大面积留白
- **风格一致性**：同一项目内同类素材（如所有子弹、所有树木）应共享统一的色彩体系和细节程度
- **多状态/多变体**：如需要多个变体（如 3 种树木、不同颜色子弹），逐一说明差异点，复用同一基础描述
