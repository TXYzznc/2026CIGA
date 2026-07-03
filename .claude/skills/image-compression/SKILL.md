---
name: image-compression
description: 通用图片压缩工具——用 tools/ImageCompression_Tool/cli.py 压缩 PNG/JPEG/WEBP/TGA。是 AI 的"兜底之手"：仅在图片已经存在且超规（外部下载 / 历史遗留 / 出图工具偶发超规）时调用，不参与正常生产流水线。触发：压缩图片、压缩贴图、缩图、减小图片体积、图片体积超标、JPG 转 PNG、批量压图、图片入库前兜底。
---

# image-compression — 通用图片压缩工具（兜底用）

## 一、定位与边界

**做的**：调用 `tools/ImageCompression_Tool/cli.py` 把单张/批量/整个目录的 PNG / JPEG / WEBP / TGA 等图片**按指定尺寸 / 格式 / 质量参数压缩**。本工具是**通用图片处理工具**，与任何具体项目规范解耦——你可以传任意 `--max-width`/`--max-height`/`--format`/`--quality`，也可以套内置 preset（preset 只是常用尺寸的便利预设）。

**仅作兜底使用的场景**：
- 出图工具偶发超规（如某模型只能产 1024 但目标是 256）
- 外部下载素材 / 历史遗留素材入库前
- 老项目对存量素材的批量整改
- 用户截图 / 参考图过大要贴对话

**不要把本 SKILL 当作"生产流水线的固定一环"**。本项目的美术生产侧约束（尺寸 / 格式 / 文件大小预算）由 [.claude/美术资源规范.md](../../美术资源规范.md) 在**前置**（ai-art 写提示词、codex-image-gen 调出图工具）阶段直接落地，生产即合规——绝大多数图根本走不到本 SKILL。

**不做的**：
- 出图 / 重绘 → [ai-art](../ai-art/SKILL.md) + [codex-image-gen](../codex-image-gen/SKILL.md)
- 透明背景抠绿幕 → [chroma_key_tool](../../../tools/chroma_key_tool/chroma_key.py)（独立调用）
- 把合并画布切成独立素材 → [ui-asset-splitting](../ui-asset-splitting/SKILL.md)
- Unity 导入设置（Texture Type / mipmap 等） → `Assets/Editor/UISpriteImportProcessor.cs` 自动处理
- 3D 贴图 PBR 流程 → [texture-art](../texture-art/SKILL.md)

## 二、何时触发（全部都是「图已经存在 + 已经超规」的兜底场景）

| 场景 | 建议 preset / 参数 |
|---|---|
| 出图工具偶发超规（如某模型只能产 1024 但目标是 256） | 对应类别 preset，或 `--max-width 256 --max-height 256` |
| 外部下载素材入库前 | 按视觉用途套对应 preset |
| 历史遗留素材定期审计发现超标 | 按视觉用途套对应 preset |
| 用户提供截图/参考图过大要贴对话 | `screenshot` |
| 想把无 alpha 的 PNG 换成更省体积的 JPEG | `--format JPEG --quality 85` |

> **不要在以下场景调用本 SKILL**：写 ai-art 提示词时（应直接按规范让出图工具产合规尺寸）/ codex-image-gen 出图时（同上）/ ui-asset-splitting 拆分时（拆出的素材本身已落规范）。这些都是**前置**约束阶段，本 SKILL 是**后置**兜底，错位调用 = 流水线设计倒挂。

## 三、前置条件

1. ✅ `.venv` 已就绪、`Pillow` 已装（`requirements.txt` 已在 setup.md 里）
2. ✅ `tools/ImageCompression_Tool/cli.py` 存在
3. ✅ 待压缩文件已确认（**不是源工程的最终交付物之前**——避免压完才发现要原图）
4. ⚠️ `--in-place` 之前**必须**有 git 工作区是干净的（万一压坏可以 `git checkout`）；不干净就走默认 `compressed/` 子目录路径，验收无误再手动替换

## 四、执行流程

### 4.1 单张压缩（默认 `compressed/` 子目录）

```bash
.venv/Scripts/python tools/ImageCompression_Tool/cli.py \
    Assets/Resources/Sprite/Environments/Tavern.png --preset environment-bg
```

输出 → `Assets/Resources/Sprite/Environments/compressed/Tavern.png`，**不会**覆盖原图。

### 4.2 整个目录递归 + 原地覆盖（危险）

```bash
# 先确认 git 工作区干净
git status -- Assets/Resources/Sprite/Items

.venv/Scripts/python tools/ImageCompression_Tool/cli.py \
    Assets/Resources/Sprite/Items --recursive --in-place --preset item-icon --json
```

JSON 输出可直接被下一步消费（如写进生成记录、做汇总报告）。

### 4.3 临时一次性压缩（不套 preset）

```bash
.venv/Scripts/python tools/ImageCompression_Tool/cli.py path/to/foo.png \
    --max-width 512 --max-height 512 --format PNG --png-quantize
```

### 4.4 验收（强制）

压完后**必须**：

1. 用 `Read` 工具或 `PIL.Image.open` 打开**至少一张**输出图，确认尺寸、模式（带不带 alpha）、视觉无明显坏点
2. 解析 `--json` 输出的 `saved_pct` 与每张 `output_size`，确认满足 [美术资源规范.md](../../美术资源规范.md) §二 的文件大小上限；超限项要记录并报告用户（**不要默默通过**）
3. 若是 `--in-place`：用 `git diff --stat` 看哪些被改了，跟预期清单对齐

## 五、JSON 输出契约（AI 消费）

```json
{
  "total": 8,
  "ok": 8,
  "fail": 0,
  "input_bytes": 1227915,
  "output_bytes": 1143102,
  "saved_pct": 6.91,
  "preset": "decal",
  "files": [
    {
      "input": "Assets/Resources/Sprite/Decal/Pattern/Sample.png",
      "output": "Assets/Resources/Sprite/Decal/Pattern/compressed/Sample.png",
      "input_size": 78441,
      "output_size": 76654,
      "ratio": 2.28,
      "success": true,
      "error": ""
    }
  ]
}
```

退出码：`0` 全成功 / `1` 任一失败 / `2` 参数错误。

## 六、踩坑

1. **PNG quantize 后视觉色阶肉眼可见的劣化** → 该类素材本身颜色多（如真实风格场景图），换成无量化（去掉 `--png-quantize`）或换 JPEG（无 alpha 场景）
2. **JPEG 透明被填白** → JPEG 不支持 alpha，源图带透明又选 `--format JPEG` 时透明会被 RGB 白底替换；想保留透明用 PNG 或 WEBP
3. **WEBP method=6 较慢** → 批量上千张时考虑加 `--quiet` 节省 stdout 时间
4. **codex 直出常见 1254×1254 / 1672×941 等非 POT 尺寸** → 套 preset 会自动缩到目标尺寸（如 256/512/1024），不需要手动 resize
5. **`--in-place` 把图压坏没法恢复** → 必须先 `git status` 确认工作区干净；否则走默认 `compressed/` 子目录路径
6. **导入到 Resources 后 Unity 自动 reimport 才生效** → 压完拷进 `Assets/Resources/Sprite/` 下后由 `UISpriteImportProcessor` 自动设导入参数，需要让 Unity 重新聚焦/编译触发；可用 `unity-skills` MCP 主动 reimport

## 七、Definition of Done

- [ ] 所有目标文件都已落到指定输出目录（或原地覆盖完成）
- [ ] JSON 输出的 `fail == 0`
- [ ] 每张输出文件大小 ≤ [美术资源规范.md](../../美术资源规范.md) §二 对应类别上限
- [ ] 已抽查 1-2 张输出视觉无明显坏点（色阶、透明、边缘）
- [ ] 若 `--in-place` 改动到 git 跟踪文件，已在 commit message / 任务回复中明确列出改动清单与压缩前后体积对比
