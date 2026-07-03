---
name: font-selection-cjk
description: 中日韩字体选型与字形覆盖率检测。触发：中文字体、日文字体、韩文字体、CJK、字形覆盖率、GB18030、思源、Noto、TMP 字体、字符集。
tags: font, cjk, unity, tmp, coverage
---

# CJK 字体选型与覆盖率

## 何时使用
- 项目本地化需中/日/韩多语言文本展示。
- 出现「豆腐块」（□）即字形缺失，需要核查字体覆盖。
- 选择 Unity TextMeshPro 主字体 / Fallback 链。
- 评估开源字体的商用授权与体积。

## 核心规则
- **永远不要混用「字符集」与「编码」**：GB2312 是字符集（6763 个汉字），UTF-8 是编码方式。字体覆盖按 Unicode codepoint 衡量。
- **中文最低目标**：GB2312（一二级 6763 字）覆盖游戏 UI 99%；如含古籍/生僻人名，需 GB18036（27533 字）或 CJK Unified Ideographs Ext-A/B。
- **日文必备三件套**：Hiragana (U+3040–309F) + Katakana (U+30A0–30FF) + 常用汉字 2136 字（常用漢字表，覆盖 JIS Level 1）。注意「直」「次」等汉字字形与中文不同 — Han Unification 同 codepoint，需选日文字形版本。
- **韩文核心**：完成型 Hangul Syllables (U+AC00–D7A3) 共 11172 字；现代韩文几乎只用完成型，组合型 Jamo 仅古文/输入法。
- **同 codepoint 不同字形**：思源黑体分 SC/TC/JP/KR 四套 region，按目标语言选择，不要用 SC 显示日文。

## 推荐字体（开源 / 可商用）

| 字体 | 覆盖 | 授权 | 体积（Regular） | 适用 |
|------|------|------|-----------------|------|
| Noto Sans CJK SC/TC/JP/KR | CJK Unified + Ext-A | OFL 1.1 | ~10 MB / region | 通用 UI，黑体 |
| Source Han Sans (思源黑体) | 同上（实为同字体） | OFL 1.1 | ~10 MB | 同 Noto |
| Source Han Serif (思源宋体) | 同上 | OFL 1.1 | ~14 MB | 标题、剧情 |
| LXGW WenKai (霞鹜文楷) | GB18030 | OFL 1.1 | ~20 MB | 古风/楷体调性 |
| 文泉驿微米黑 | GB18030 | GPL+字体例外 | ~5 MB | 体积敏感 |
| Cubic 11（方型本黑） | CJK 常用 | OFL 1.1 | ~1.5 MB | 11px 像素字体 |
| Inter + Noto Sans | 拉丁 + CJK | OFL 1.1 | 视组合 | 拉丁主、CJK fallback |

> **OFL 关键条款**：可商用、可嵌入、可子集化，但 *修改后衍生字体不得单独售卖*，名称不可保留原名。

## 关键流程/模式

### 模式 A：用 fc-list 检查系统字体覆盖
```bash
# Linux/Mac：列出所有含中文的字体
fc-list :lang=zh
fc-list :lang=ja
fc-list :lang=ko

# 查询某字体是否覆盖某 Unicode 区段
fc-match -s "Noto Sans CJK SC" | head
```

### 模式 B：FontTools 检测字形覆盖率
```python
# pip install fonttools
from fontTools.ttLib import TTFont

def coverage(font_path, text):
    font = TTFont(font_path)
    cmap = font.getBestCmap()  # {codepoint: glyphName}
    missing = [c for c in set(text) if ord(c) not in cmap]
    rate = 1 - len(missing) / len(set(text))
    return rate, missing

with open("game_text.txt", encoding="utf-8") as f:
    text = f.read()
rate, miss = coverage("NotoSansCJKsc-Regular.otf", text)
print(f"覆盖率 {rate*100:.2f}% 缺失 {len(miss)} 字: {''.join(miss[:50])}")
```

### 模式 C：Unity TextMeshPro 字体生成
1. **Window > TextMeshPro > Font Asset Creator**。
2. Source Font File 选 .ttf/.otf。
3. **Sampling Point Size**：Auto Sizing 或 32–64（SDF 抗模糊容忍度高）。
4. **Atlas Resolution**：2048×2048（CJK 全字符可能溢出，用多 Atlas 或 Dynamic）。
5. **Character Set**：
   - `ASCII` — 拉丁基础
   - `Unicode Range (Hex)` — 自定义，如 `4E00-9FFF,3040-309F,30A0-30FF`
   - `Characters from File` — 推荐，喂入实际游戏文本去重后的字符集
6. **Atlas Population Mode**：
   - `Static` — 出包前烘焙，运行时零开销，无法新增字符
   - `Dynamic` — 运行时按需生成，适合 UGC/聊天，但需保留 .ttf 在包内（体积变大）
7. **Fallback List**：主字体英文用 Inter，CJK 兜底用 Noto Sans CJK。

## 常见坑
- **「中文显示成日文字形」**：用了 Noto Sans CJK JP 而非 SC。Source Han / Noto CJK 是 4 个独立文件，按 region 选。
- **TMP Dynamic SDF 卡顿**：每帧大量新字符生成会卡。解决：预热常用字符集 `tmpFont.TryAddCharacters(commonChars)`。
- **生僻字「燚」「𤭢」缺失**：Unified Ideographs Ext-B/C/D/E 在 SMP 平面（U+20000+），需字体支持。Noto Sans CJK 含 Ext-A，不含 Ext-B+；如需用 Adobe Sans Han 或自行合并。
- **OFL 改名陷阱**：子集化保留原名 OK，但 *修改字形* 必须改 family name 才能商用。
- **iOS 系统字体 PingFang 不可分发**：仅可调用系统 API，不可打包进 ipa。

## 代码/命令示例
```bash
# 一键查覆盖率（FontTools 命令行）
pyftinspect MyFont.ttf | grep -A 2 "cmap"

# 列出字体所有 codepoint（导出文本）
ttx -t cmap MyFont.ttf  # 生成 MyFont.ttx 含 cmap 表
```

```csharp
// Unity TMP 运行时检查字符是否在 atlas
TMP_FontAsset fa = tmpText.font;
if (!fa.HasCharacter('燚')) {
    Debug.LogWarning("字符缺失，将走 fallback");
}
```
