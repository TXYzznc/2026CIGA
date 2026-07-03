
# 场景图提示词生成器

## 核心设计理念

### 项目美术风格 —「黑暗史诗手绘肉鸽」

- **整体风格**：手绘厚涂背景，俯视角 2.5D 视角
- **光影**：平面阐释光为主（不走动态实时光照），关键物体加局部点光源（火堆、机械火花、异形腔孔发光）
- **色调**：环境低饱和黄昏调（土褐 / 深棕 / 灰绿），仅区域主题色 + 点光源高饱和

### 末日主题场景 × 双重区别系统

不同末日成因区域用 **「色调 + 几何语言」双重维度** 区分，玩家 0.5 秒识别当前区域：

| 区域 | 色调 | 几何语言 | 典型元素 | 主光源 |
|------|------|--------|---------|------|
| **AI 叛乱区** | 冷青 #2a5c7c + 钢灰 | 机械直角、网格、管道 | 废弃工厂、断电路、爆裂电缆、机械骨架 | 闪烁电火花、霓虹应急灯 |
| **外星入侵区** | 紫色 #6c3a7a + 暗洋红 | 有机曲线、不对称、跳跃比例 | 异形巢穴、扭曲空间、晶体生长、肉壁 | 紫色生体发光、异界裂缝 |
| **病毒变异区** | 黄绿 #6b8a3c + 暗黄 | 不规则肿块、流体、孢子 | 真菌森林、感染兽尸、毒液沼泽、变异植物 | 黄绿色磷光、毒液冒泡 |
| **中性废墟** | 黄褐 #6b4423 | 残破建筑直角 | 路边石、断墙、火堆、车骸 | 火堆橙黄、月光 |
| **纹身师工作室** | 深紫红 #4a2a3a + 烛光金 | 厚墙壁 + 魔法陈设 | 桌椅、墨水瓶、刻刀、人体图鉴 | 烛光暖黄、神秘符文蓝 |

### 设计原则

1. **平面光节省性能**：场景烘焙好的平面阴影 + 动态点光源（火堆、技能），不走 PBR
2. **几何即语言**：玩家看到「直角钢架 = AI 区」无需文字说明
3. **环境去饱和 + 关键物体高饱和**：让宝箱、纹身师、火堆、技能效果在场景中一眼凸显
4. **天空通常不可见**（俯视角），不要花时间画天空盒

### 项目美术风格关键词集合（提示词模板填充用）

> 下方 `[项目美术风格关键词]` 占位符使用（`[区域色调]` 处填写当前区域色板）：
```
dark epic painterly hand-drawn scene background, top-down 2.5D perspective,
heavy brushwork ground and walls, flat ambient lighting with localized point light
sources (campfire / machinery sparks / alien glow), low-saturation [区域色调] palette,
reference style of Darkest Dungeon environments and Hades chamber backgrounds
```

> 下方 `[主题描述]` 占位符使用：
```
post-apocalyptic ruins of multi-disaster world (AI rebellion / alien invasion /
virus mutation), no sky visible (top-down view), 1920x1080 or 2048x2048 tile
```

### 推理示例

**需求**：「AI 叛乱区废弃工厂场景」
**推理**：AI 区 → 冷青色调 → 机械直角 → 废弃工厂 → 闪烁电火花点光源 → 厚涂手绘
**提示词关键字**：
```
dark epic painterly hand-drawn scene background, top-down 2.5D perspective view of
an abandoned AI rebellion factory, heavy brushwork rusted steel grates and broken
pipes, low-saturation cold cyan-grey palette (#2a5c7c), flat ambient lighting with
localized point lights from sparking torn cables and flickering emergency neon,
mechanical right-angle geometry dominates, scattered robot skeletons, no sky visible,
2048x2048, reference Darkest Dungeon environment style
```

---

## 提示词结构

```
[分辨率] [场景类型] [地域/世界特征]
Key elements: [主要场景元素列表]
Color palette: [色彩代码]
Lighting: [光效/时间描述]
Atmosphere: [氛围关键词]
Background layers: [远景/中景/近景描述]
No border, no frame, no rounded corners, full bleed image filling the entire canvas
Professional game art style, [主题描述]
```

> **注意**：SCENE 生成的图片是铺满整个画布的背景画面，**不附加透明背景 PNG 要求**（不存在"主体外透明区域"的概念，强加该要求会与 `Background layers` 结构冲突）。其余类型（ICON/CHARACTER/UI）需附加，详见 `drawing-prompt-generator.md`。
