---
name: game-art
description: 游戏美术总原则与跨子专家协调入口。覆盖视觉风格选型、美术资源管线、动画工作流、色彩理论。触发：美术风格、art style、美术管线、art pipeline、视觉方向。❌ 不适用：具体执行（UI/3D/动画/特效），请用 art-ui / art-3d / art-anim / art-vfx。
allowed-tools: Read, Glob, Grep
tags: game-art, art-style-selection, asset-pipeline, animation-workflow, color-theory
tags_cn: 游戏美术, 美术风格选择, 资源管线, 动画工作流, 色彩理论
---

# 游戏美术原则

> 游戏视觉设计思路——风格选择、资源管线与美术指导

---

## 1. 美术风格选择

### 决策树

```
What feeling should the game evoke?
│
├── Nostalgic / Retro
│   ├── Limited palette? → Pixel Art
│   └── Hand-drawn feel? → Vector / Flash style
│
├── Realistic / Immersive
│   ├── High budget? → PBR 3D
│   └── Stylized realism? → Hand-painted textures
│
├── Approachable / Casual
│   ├── Clean shapes? → Flat / Minimalist
│   └── Soft feel? → Gradient / Soft shadows
│
└── Unique / Experimental
    └── Define custom style guide
```

### 风格对比矩阵

| 风格 | 制作速度 | 入门门槛 | 可扩展性 | 适用场景 |
|-------|------------------|-------------|-------------|----------|
| **Pixel Art** | 中等 | 中等 | 难以招聘相关人才 | 独立游戏、复古游戏 |
| **Vector/Flat** | 快速 | 低 | 容易 | 移动游戏、休闲游戏 |
| **Hand-painted** | 缓慢 | 高 | 中等 | 奇幻游戏、风格化游戏 |
| **PBR 3D** | 缓慢 | 高 | AAA级管线 | 写实类游戏 |
| **Low-poly** | 快速 | 中等 | 容易 | 独立3D游戏 |
| **Cel-shaded** | 中等 | 中等 | 中等 | 动漫、卡通类游戏 |

---

## 2. 资源管线决策

### 2D资源管线

| 阶段 | 工具选项 | 输出结果 |
|-------|--------------|--------|
| **概念设计** | 纸笔、Procreate、Photoshop | 参考图册 |
| **创作** | Aseprite、Photoshop、Krita | 独立精灵图 |
| **图集制作** | TexturePacker、Aseprite | 精灵图集 |
| **动画制作** | Spine、DragonBones、逐帧动画 | 动画数据 |
| **集成** | 引擎导入 | 游戏可用资源 |

### 3D资源管线

| 阶段 | 工具选项 | 输出结果 |
|-------|--------------|--------|
| **概念设计** | 2D美术、Blockout | 参考素材 |
| **建模** | Blender、Maya、3ds Max | 高模网格 |
| **拓扑重构** | Blender、ZBrush | 游戏可用网格 |
| **UV/纹理制作** | Substance Painter、Blender | 纹理贴图 |
| **绑定** | Blender、Maya | 骨骼绑定 |
| **动画制作** | Blender、Maya、Mixamo | 动画片段 |
| **导出** | FBX、glTF | 引擎可用资源 |

---

## 3. 色彩理论决策

### 调色板选择

| 目标 | 策略 | 示例 |
|------|----------|---------|
| **和谐感** | 互补色或类似色 | 自然题材游戏 |
| **对比度** | 高饱和度差异 | 动作类游戏 |
| **氛围** | 暖色调/冷色调 | 恐怖游戏、治愈系游戏 |
| **可读性** | 优先明度对比而非色相对比 | 游戏玩法清晰度 |

### 色彩原则

- **层级性：** 重要元素应突出
- **一致性：** 同一物体使用同一色系
- **关联性：** 颜色在不同背景下呈现效果不同
- **可访问性：** 不要仅依赖颜色传递信息

---

## 4. 动画原则

### 12条动画原则（游戏应用版）

| 原则 | 游戏应用场景 |
|-----------|------------------|
| **挤压与拉伸（Squash & Stretch）** | 跳跃轨迹、碰撞效果 |
| **预备动作（Anticipation）** | 攻击前的蓄力动作 |
| **舞台呈现（Staging）** | 清晰的轮廓 |
| **跟随动作（Follow-through）** | 移动后的头发、披风摆动 |
| **缓入缓出（Slow in/out）** | 过渡动画的缓动效果 |
| **弧线运动（Arcs）** | 自然的运动路径 |
| **次要动作（Secondary Action）** | 呼吸、眨眼 |
| **时间节奏（Timing）** | 帧数决定重量/速度 |
| **夸张表现（Exaggeration）** | 远距离仍清晰可辨 |
| **吸引力（Appeal）** | 令人难忘的设计 |

### 帧数参考指南

| 动作类型 | 典型帧数 | 感受 |
|-------------|----------------|------|
| Idle呼吸 | 4-8 | 微妙自然 |
| 行走循环 | 6-12 | 流畅 |
| 奔跑循环 | 4-8 | 充满活力 |
| 攻击动作 | 3-6 | 干脆利落 |
| 死亡动作 | 8-16 | 富有戏剧性 |

---

## 5. 分辨率与缩放决策

### 各平台2D分辨率

| 平台 | 基础分辨率 | 精灵缩放比例 |
|----------|-----------------|--------------|
| 移动平台 | 1080p | 64-128px角色 |
| 桌面平台 | 1080p-4K | 128-256px角色 |
| 像素美术 | 320x180 至 640x360 | 16-32px角色 |

### 一致性规则

选择一个基础单位并保持统一：
- 像素美术：以1倍尺寸制作，再放大（绝不缩小）
- 高清美术：定义DPI，保持比例
- 3D：1单位 = 1米（行业标准）

---

## 6. 资源管理

### 命名规范

```
[type]_[object]_[variant]_[state].[ext]

Examples:
spr_player_idle_01.png
tex_stone_wall_normal.png
mesh_tree_oak_lod2.fbx
```

### 文件夹结构原则

```
assets/
├── characters/
│   ├── player/
│   └── enemies/
├── environment/
│   ├── props/
│   └── tiles/
├── ui/
├── effects/
└── audio/
```

---

## 7. 反模式

| 不要做 | 应该做 |
|-------|-----|
| 随机混合美术风格 | 定义并遵循风格指南 |
| 仅以最终分辨率制作 | 以源分辨率创作 |
| 忽略轮廓可读性 | 在游戏实际距离下测试 |
| 过度细化背景 | 将细节聚焦在玩家关注区域 |
| 跳过色彩测试 | 在目标显示器上测试 |

---

> **谨记：** 美术为游戏玩法服务。如果对玩家没有帮助，那它只是装饰。