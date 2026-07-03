
# 角色立绘提示词生成器

## 核心设计理念

### 项目美术风格 —「黑暗史诗手绘肉鸽」

- **整体风格**：手绘厚涂 + 粗描边 + 局部发光描边的混合质感（painterly hand-drawn + bold outlined + glowing rim light），偏向 *Darkest Dungeon × Hades × Battle Chasers* 的综合调性
- **角色比例**：4-5 头身写实人体比例（不是 Q 版），俯视角 2.5D 下能展示 6 个纹身部位
- **色彩特征**：环境低饱和黄昏调（土褐、深棕、墨绿）；纹身/技能效果高饱和发光（参 §色彩体系-元素色板）
- **服饰**：破烂布装 + 大面积皮肤露出（露肩、胸口、腹部、大腿）；机械/生化改造痕迹可见（金属义体接缝、生化纹路、缝合线）
- **质感**：粗手绘黑色描边 1-2px，内部厚涂笔触；纹身边缘霓虹光描边（参考 Hades 祝福图标的发光手法）

### 角色类型与视觉语言

| 角色类型 | 视觉语言 | 服饰倾向 | 改造痕迹 | 示例 |
|---------|---------|--------|---------|------|
| **标准实验体（玩家）** | 4.5头身、运动员体型 | 破烂束腰布装+绷带+腰间皮带 | 后背/肩膀有缝合线，金属接口 | player_01 |
| **机械改造体** | 同上但直角线条更多 | 半甲+布裙 | 一只手/腿是金属义体 | mech_subject |
| **植物变异体** | 同上+苔藓质感 | 藤蔓+皮甲 | 皮肤生有藤蔓/真菌 | plant_subject |
| **异形改造体** | 异形比例（不对称） | 紧身肌纤维服 | 触手/复眼/角 | alien_subject |
| **纹身师 NPC** | 5头身瘦长 | 长袍+围裙+多个小腰包 | 全身覆盖完成态纹身 | npc_tattooist |
| **商人 NPC** | 4头身矮胖 | 多层斗篷 | 无 | npc_merchant |

### 色彩体系

**环境色（角色背景中性色，去饱和黄昏调）**：
```
深棕底     #3a2418
黄昏中明   #6b4423
黄褐高光   #a87c4f
土黄点缀   #d4b896
```

**纹身/技能元素色（7 色高饱和发光，与 03-世界观文档锁定）**：
```
🔴 火 Fire        #ff3030    病毒变异/异变能量
🟡 雷 Lightning   #ffe838    AI 故障/机械能量
🟢 毒 Poison      #38ff5c    病毒变异/生化污染
🔵 冰 Ice         #00d4ff    外星造物/异界寒能
🟣 异变 Void      #b838ff    外星入侵/维度扭曲
🟨 神圣 Holy      #ffd700    神秘/古文明残留
⚪ 纯能 Pure      #ffffff    中性/实验体本能
```

### 设计原则

1. **远观可读 build**：6 个纹身部位（头/胸/背/左臂/右臂/左腿/右腿）在俯视角下都能清晰展示且发光，让玩家「远观判断对手 build」的 USP 成立
2. **写实人体 + 厚涂卡通处理**：身体比例真实，但渲染方式是厚涂手绘（不是 PBR、不是 cell-shading）
3. **改造痕迹永远可见**：实验体身份必须从视觉上立刻识别，不能画成普通人
4. **环境/角色色彩反差**：环境土褐低饱和让角色身上的纹身能量光成为视觉焦点

### 项目美术风格关键词集合（提示词模板填充用）

> 下方提示词结构中遇到 `[项目美术风格关键词]` 占位符时，使用以下字符串替换：

```
dark epic painterly hand-drawn, heavy brushwork, bold black outlined 1-2px,
4-5 head tall realistic proportions, top-down 2.5D perspective,
low-saturation dusk palette environment, glowing rim light on tattoos and energy,
reference style of Darkest Dungeon, Hades, Battle Chasers Nightwar
```

> 下方 `[主题描述]` 占位符位置，使用：
```
post-apocalyptic experimental subject in multi-disaster world, body covered with
elemental tattoos as ability build, mechanical/biological body modifications visible
```

### 推理示例

**需求**：「生成标准实验体玩家立绘」
**推理**：标准实验体 → 4.5 头身 → 运动员体型 → 破烂束腰布装+绷带 → 后背缝合线+金属接口 → 厚涂手绘描边 → 露出 6 个纹身部位
**提示词关键字**：
```
dark epic painterly hand-drawn, 4-5 head tall realistic proportions, athletic build
experimental human subject, torn cloth wraps and bandages, exposed shoulders chest
and thighs revealing tattoo placement areas, surgical stitches on back and metallic
implant ports, bold black outline 1-2px, heavy brushwork, dusk warm brown palette
environment, glowing rim light on body, reference Darkest Dungeon and Hades art style,
top-down 2.5D pose
```

---

## 提示词结构

```
[分辨率] [画面类型] [角色职业/身份] [外貌描述]
Costume: [服饰描述]
Expression: [表情/神态]
Color palette: [主色调代码]
Lighting: [光效描述]
Background: [背景描述（立绘通常简洁）]
Art style: [项目美术风格关键词], high detail character illustration
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

- **立绘**：半身或全身，背景简洁（渐变或虚化场景），人物占画面 70% 以上
- **头像**：胸部以上，特写表情，背景极简
- **姿势**：有力量感的站姿或动态姿势，避免平淡的正面站立
- **细节**：武器/法器在画面中清晰可见，体现职业特征
