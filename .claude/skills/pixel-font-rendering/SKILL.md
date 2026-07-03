---
name: pixel-font-rendering
description: 像素字体渲染与点阵字体配置。触发：像素字体、pixel font、bitmap font、点阵字体、BMFont、像素渲染、TextMeshPro 点阵、复古字体。
tags: font, pixel, bitmap, bmfont, unity, retro
---

# 像素字体渲染

## 何时使用
- 像素艺术风游戏需要点阵感清晰的文字。
- 文字显示「糊」「锯齿不对」「自动抗锯齿了」。
- 复古/8-bit/16-bit 风格 UI、对话框、伤害数字。
- 需要严格 1:1 像素映射的低分辨率渲染。

## 核心规则
- **两条流派**：
  - **Bitmap Font**（.fnt + .png）：天然像素，每字符一组像素。**只能等比整数缩放**。
  - **Vector Pixel Font**（TTF，如 Press Start 2P / Silver / Cubic 11）：轮廓字体但设计成像素网格对齐。**可任意尺寸但需禁用抗锯齿**。
- **像素 1:1 三要素**：① Camera 正交 + 整数 PPU；② 字体渲染尺寸 = 设计尺寸 × N（N ∈ ℤ⁺）；③ Filter Mode = Point。
- **永远禁用抗锯齿**：Texture Import 关闭 Generate Mipmaps；TMP 用 `Bitmap` Shader（无 SDF 模糊）。
- **TTF 像素字体的 Point Size = 设计像素高**：Cubic 11 设计为 11px，TMP Sampling Point Size 必须 11/22/33（整数倍），否则字形错位。

## 推荐字体

| 字体 | 设计像素 | 覆盖 | 授权 | 风格 |
|------|---------|------|------|------|
| Press Start 2P | 8 | ASCII + Latin Ext | OFL | 经典街机 |
| Silver | 11 | ASCII | OFL | 圆角现代像素 |
| Fusion Pixel | 12 | CJK 全覆盖 | OFL | 通用中文像素 |
| Cubic 11（方型本黑） | 11 | CJK 常用 | OFL | 中文方块像素，最知名 |
| PixelOperator | 8/16 | Latin + Cyrillic | CC0 | 终端风 |
| Boutique Bitmap 9x9 | 9 | CJK 常用 | 免费 | 中日韩 9px |
| Galmuri | 11/14 | 韩文 + CJK | OFL | 韩文像素 |

## 关键流程/模式

### 模式 A：Bitmap Font 流派（BMFont）
1. **生成工具**：
   - **AngelCode BMFont**（Windows，免费，老牌）
   - **Hiero**（跨平台 Java，LibGDX 工具）
   - **TexturePacker**（商用，但支持 .fnt 导出）
   - **SnowB BMFont**（在线，免费）
2. **配置要点**（BMFont 软件内）：
   - Font Settings：勾选 Match char height，Render from TrueType outline 选 **No smoothing**
   - Export：File format `Text`（.fnt），Texture format `png`
   - Padding：A/B/C/D 各 0，Spacing H/V 各 1（防纹理渗透）
3. **Unity 导入**：
   - .png：Texture Type `Sprite (2D and UI)`，**Filter Mode = Point**，**Compression = None**
   - .fnt：用 TMP 的 BMFont importer 或第三方插件转 SDF/Bitmap Asset

### 模式 B：Vector Pixel Font + TMP Bitmap 流派
**推荐做法**，可缩放、易本地化。
1. Font Asset Creator：
   - Sampling Point Size = 字体设计像素值（如 Cubic 11 用 **11**，绝不能填 12/16）
   - Padding = 0（重要！默认 5 会糊）
   - **Render Mode = `RASTER`**（不是 SDF！）
   - Atlas Resolution 按字符量定，Cubic 11 全字 4096×4096 可塞下
2. 使用时：
   - TMP Text 字号填 11、22、33、44（设计像素 × N）
   - Material Shader 切到 `TextMeshPro/Bitmap` 或 `TextMeshPro/Bitmap (Mobile)`
   - 关闭 `Extra Settings → Outline / Underlay`

### 模式 C：1:1 屏幕像素校准
```csharp
// 像素完美 Camera 设置
Camera cam = Camera.main;
cam.orthographic = true;
// 设计分辨率 240×160，目标设备 1080p
// 缩放倍数 = 1080 / 160 = 6.75 → 取 6（整数）
int designH = 160;
int scale = Screen.height / designH;     // 整数
cam.orthographicSize = designH * 0.5f / 100f;   // PPU = 100
// 渲染目标用 RenderTexture (240×160) → 再 Point Filter 放大到屏幕
```

### 模式 D：Texture/Atlas Import 设置
- Texture Type：Sprite (2D and UI) 或 Default
- **Filter Mode：Point (no filter)**
- **Compression：None**
- Wrap Mode：Clamp
- Generate Mip Maps：Off
- Max Size：≥ atlas 实际尺寸（不要被压小）

## 常见坑
- **「字模糊一片」**：Sampling Point Size 错（如 Cubic 11 填了 12）或 Render Mode 用了 SDF。修：改 11 + RASTER。
- **「整数缩放还是糊」**：Filter Mode 没改 Point，或 Canvas Scaler 用了 Scale With Screen Size 导致非整数缩放。修：UI 用固定参考分辨率 + Constant Pixel Size。
- **「中文像素字超大体积」**：Cubic 11 全字 atlas 4096×4096，约 5 MB png。修：子集化 → Font Asset Creator 用 Characters from File。
- **「半透明边缘」**：Padding > 0 会让 RASTER 模式带 alpha 渐变。修：Padding = 0。
- **「TMP 默认 Shader 还是糊」**：默认 Distance Field Shader 自带模糊。修：手动换 `TextMeshPro/Bitmap`。
- **「旧 UGUI Text 像素字效果更好？」**：UGUI 用 dynamic font 直接位图渲染，没 SDF 模糊。如不需要 TMP 富文本，UGUI Text 反而简单。
- **整数倍只对设计像素而言**：Press Start 2P 设计 8px，TMP 字号必须 8/16/24/32…，写 10 就糊。

## 代码/命令示例
```csharp
// 运行时强制 TMP 字体走 Bitmap Shader
using TMPro;
TextMeshProUGUI t = GetComponent<TextMeshProUGUI>();
t.font.material.shader = Shader.Find("TextMeshPro/Bitmap");
t.fontSize = 22;  // Cubic 11 的 2x
t.enableWordWrapping = false;  // 像素字常关自动换行
```

```csharp
// 检测当前显示是否整数倍像素映射
float ratio = (float)Screen.height / designHeight;
if (Mathf.Abs(ratio - Mathf.Round(ratio)) > 0.001f)
    Debug.LogWarning($"非整数倍缩放 {ratio:F3}，像素字会糊");
```

```bash
# BMFont 命令行（v1.14+）
bmfont64.exe -c font.bmfc -o output.fnt
# .bmfc 是配置文件，可批量生成多字号 atlas
```
