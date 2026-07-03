---
name: font-subsetting
description: 字体子集化与包体优化。触发：字体子集、subsetting、woff2、SDF、MSDF、TextMeshPro Atlas、字体压缩、包体优化、pyftsubset。
tags: font, subset, optimization, woff2, sdf, tmp
---

# 字体子集化与包体优化

## 何时使用
- 字体文件 > 5 MB，影响首包/首屏加载。
- CJK 字体动辄 10–30 MB，需按实际使用字符裁剪。
- 移动端 H5 / 小游戏强约束首包 < 4 MB。
- Unity TMP Atlas 占用过大或 SDF 模糊。

## 核心规则
- **裁剪原则**：先按实际用字（CSV/JSON 文本提取）+ 通用补集（ASCII、标点、常用 2500）。
- **格式选择**：Web 用 woff2（Brotli 压缩，比 ttf 小 30–50%）；Unity/原生 用 ttf/otf（引擎不识别 woff2）。
- **保留 hinting**：移动端低分辨率屏，去 hinting 会让字渲糊。pyftsubset 默认保留。
- **不要重复子集**：再次子集会因 cmap 重建丢字 — 始终从原始母字体开始。
- **SDF vs MSDF**：SDF 单通道更小（适合小号 UI）；MSDF 多通道保细节（适合大号标题、像素描边）。
- **Atlas 单图 ≤ 2048×2048**：超过用多 Atlas 或动态。

## 关键流程/模式

### 模式 A：从游戏文本 CSV 提取字符集
```python
# extract_charset.py
import csv, sys, pathlib

def extract(csv_files, out_file, extra_chars=""):
    chars = set(extra_chars)
    # ASCII 可见 + 中英文标点
    chars |= set(chr(c) for c in range(0x20, 0x7F))
    chars |= set("，。、；：！？""''（）《》【】…—")

    for fp in csv_files:
        with open(fp, encoding="utf-8") as f:
            for row in csv.reader(f):
                for cell in row:
                    chars |= set(cell)

    chars.discard("\n"); chars.discard("\t")
    pathlib.Path(out_file).write_text("".join(sorted(chars)), encoding="utf-8")
    print(f"导出 {len(chars)} 个字符到 {out_file}")

if __name__ == "__main__":
    extract(sys.argv[1:-1], sys.argv[-1])
# 用法: python extract_charset.py text/*.csv out/chars.txt
```

### 模式 B：pyftsubset 命令模板
```bash
# 安装：pip install fonttools brotli zopfli

# 1. 按字符文件子集（推荐：游戏项目）
pyftsubset NotoSansCJKsc-Regular.otf \
  --text-file=chars.txt \
  --output-file=NotoSC-game.ttf \
  --layout-features='*' \
  --no-hinting=False \
  --desubroutinize \
  --name-IDs='*' \
  --notdef-outline

# 2. 按 Unicode 区段子集（Web 字体常见）
pyftsubset Inter-Regular.woff2 \
  --unicodes=U+0000-00FF,U+2000-206F \
  --flavor=woff2 \
  --output-file=Inter-latin.woff2

# 3. 极限压缩 woff2（首屏字体）
pyftsubset SourceHanSansSC-Regular.otf \
  --text-file=chars.txt \
  --flavor=woff2 \
  --with-zopfli \
  --output-file=SHS-tiny.woff2
```

**关键参数速查**：
| 参数 | 作用 |
|------|------|
| `--text-file=` | 字符列表文件（UTF-8） |
| `--unicodes=` | Unicode 范围，如 `U+4E00-9FFF` |
| `--flavor=woff2` | 输出 woff2（Web） |
| `--layout-features='*'` | 保留所有 OpenType 特性（连字/小写等） |
| `--desubroutinize` | 展开 CFF 子程序，体积换性能 |
| `--drop-tables=DSIG` | 丢数字签名表，减体积 |
| `--no-hinting` | 丢 hinting（**移动端不推荐**） |
| `--with-zopfli` | 极限压缩（慢但小 5–10%） |

### 模式 C：Unity TextMeshPro SDF 选择
- **Static SDF Atlas**：出包前烘焙固定字符集到一张图。零运行时开销，包体可控，无法显示未烘焙字符。**适合**：UI 标签、按钮、固定文案。
- **Dynamic SDF Atlas**：运行时从 .ttf 按需渲染到 Atlas。**适合**：聊天、UGC、本地化变量名。代价：需带 .ttf 进包（CJK 即 10 MB+），首次显示有 spike。
- **Dynamic OS**：从系统字体动态生成。Android/iOS 可用，但风险是不同设备字形不一致。仅 fallback 用。

### 模式 D：Fallback Chain 设计
```
主字体 (Inter, Static, ASCII+标点)           ~ 300 KB
  ↓ fallback
次字体 (Noto SC, Static, 游戏文本子集)        ~ 1.5 MB
  ↓ fallback
末字体 (Noto SC Dynamic, 完整 .ttf)           ~ 10 MB (运行时按需)
```
TMP Inspector 的 Fallback List 按此顺序填入。

## 常见坑
- **子集后丢「‼ ⁉ ™」**：text-file 漏写补充标点。修：用 `--unicodes=U+2000-206F,U+2100-214F` 兜底符号区。
- **woff2 在 Unity 报错**：Unity 不识别 woff2，仅 ttf/otf 可生成 TMP Asset。
- **SDF 描边糊掉**：Sampling Point Size 太小（< 24）。修：提升到 64，渲染时再缩小。
- **MSDF 编辑器导出失败**：TMP 默认 SDF。MSDF 需 `MSDFGen` 外部工具或 TMP Essentials Beta。
- **pyftsubset 后 Adobe Illustrator 打不开**：保留 `--name-IDs='*'` 和 `--legacy-cmap`。
- **子集后字体名仍是「Source Han Sans」**：商用须改 family name，pyftsubset 不改名，可用 fontTools 改 `name` 表 nameID 1/4/6。

## 代码/命令示例
```bash
# 一键流水线（bash）
python extract_charset.py text/*.csv build/chars.txt
pyftsubset raw/SourceHanSans-Regular.otf \
  --text-file=build/chars.txt \
  --output-file=Assets/Fonts/SHS-game.ttf \
  --drop-tables=DSIG,LTSH,VDMX \
  --desubroutinize
ls -lh Assets/Fonts/SHS-game.ttf  # 期望从 10MB → 1-2MB
```

```python
# 改字体 family name（避免 OFL 商用违规）
from fontTools.ttLib import TTFont
f = TTFont("SHS-game.ttf")
for r in f["name"].names:
    if r.nameID in (1, 4, 6, 16):
        r.string = "MyGameFont".encode(r.getEncoding())
f.save("MyGameFont.ttf")
```
