---
name: codex-image-gen
description: "将 ai-art 已经沉淀的美术需求和提示词文件交给 Codex CLI 执行实际生图。Claude 主对话本身没有图像生成能力，需要通过 codex exec 把出图任务外包给 Codex（它内置 imagegen 系统 skill 与图像生成模型）。触发场景：用户提到'实现美术素材'、'按提示词出图'、'把美术需求出图'、'生图'、'绘制美术'、'生成美术素材'、'处理美术资源'等，且前置 art/requirements.md + art/prompts.md 已存在。前置工作（提示词与需求生成、风格初始化）走 ai-art SKILL，本 SKILL 只负责最后的执行落盘环节。"
---

# Codex Image Gen — 调用 Codex CLI 执行实际生图

## 一、定位与边界

**做的**：读取已有的 `art/requirements.md` + `art/prompts.md`，逐项调 `codex exec` 调用 Codex 的 imagegen 系统 skill 生成 PNG/JPG 文件，落到 `art/raw/` 并写 `生成记录.md`、更新需求文件头部状态。

**不做的**（→ 走 [ai-art](../ai-art/SKILL.md)）：
- 美术风格初始化、art bible 撰写
- 美术需求与提示词的从零撰写
- 图片反推提示词（image-to-prompt）

**调用关系**：
```
ai-art（沉淀 requirements.md + prompts.md）
       ↓
codex-image-gen（本 SKILL — 调 Codex 实际出图）
       ↓
art/raw/*.png + 生成记录.md
```

## 二、前置条件（不满足直接停手）

1. ✅ `codex --version` 可用（CLI 已安装、已登录）
2. ✅ `openspec/changes/<change-name>/art/requirements.md` 与 `art/prompts.md` 都存在
3. ✅ 这两个文件头部**未**出现 `美术素材状态: 已处理` 或 `美术素材状态: 已归档`（若已存在先询问用户是否重做）
4. ✅ **若要走 L2**：`tools/ImageCut_Tool/image_cut.py` 存在（`ls` 检查）+ `.venv/Scripts/python -c "from PIL import Image"` 可跑通。**注意**：`tools/` 整体在 .gitignore 内，新机器需按 [setup.md](../../../setup.md) 重建；若缺失但只跑 L1/L0 可继续

任一不满足：停手并告诉用户具体阻塞点，**禁止**伪造文件、禁止假装已生成。L2 缺工具时可降级 L1 继续，但要在日志里说清楚原因。

## 三、执行流程（三档批量策略 — 节省 Codex 额度）

### 3.0 三档总览

| 档 | 触发 | 单次 codex exec 输出 | 节省点 |
|---|---|---|---|
| **L1 · 进程内并行** | 任意单图需求 → 自动合批 | 1 张 codex exec 命令 → 多张独立 PNG | 节省 N-1 次 system prompt + 进程开销 |
| **L2 · 合并画布** | 单张目标 ≤ 256×256 | 1 张大画布 PNG → 切割工具拆为 N 张小 PNG | 节省 N-1 次 imagegen 调用本身（最省） |
| **L0 · 单图模式**（保留） | 任意单张大图 / debug 单图 | 1 张 PNG | 无节省，仅 fallback / 单张 turnaround 用 |

**关键边界**：
- **单次 codex exec 内 L1 上限 12 张**（默认 8 张），超过自动分批
- **L2 触发阈值 ≤256×256**；画布固定 1024×1024 透明，资源间留 ≥32px padding
- **失败重试上限 1 次**：批次内失败项收集进队列 → 全批跑完后整批重试一次 → 仍失败标 failed
- **不重复**：处理前必查 `art/requirements.md` 头部状态字段

### 3.1 定位 change

按优先级：
1. 执行 `openspec status` 找 active change（同一时刻应只有 1 个合法）
2. 从用户上下文（最近提到的 change-name）推测
3. 询问用户「目标 change 是哪个？」

### 3.2 解析 prompts.md + 三档归类

对每张图提取：
- `资源名`（也作为文件名基础）
- `提示词`（英文优先，imagegen 对英文响应更好）
- `尺寸`：**优先**按 [美术资源规范.md](../../美术资源规范.md) §二 对应视觉类别的 Max 尺寸（如 UI 图标 256×256 / 角色全身 1024×2048 / 场景背景 1920×1080）。**目标是「生产即合规」**，让 imagegen 直接吐出符合规范的尺寸，避免事后 image-compression 兜底。仅当 prompts.md 明确指定其它尺寸或规范无对应类别时，才回退默认值（1024×1024 / 横 1536×1024 / 竖 1024×1536）
- `透明背景`（是 / 否）
- `负面提示词`（如有）
- `候选数量`（默认 1；多候选用 `资源名_v01.png` / `_v02.png`）

**自动归类**（按尺寸 + 透明背景）：

```python
# 伪代码
for item in items:
    if item.size <= (256, 256) and item.transparent == True:
        bucket_L2.append(item)
    else:
        bucket_L1.append(item)

# 再分批
L1_batches = chunks(bucket_L1, size=8)   # ≤12 上限
L2_batches = chunks(bucket_L2, size=64)  # 1024/128=8 → 8×8=64 上限
```

多模块需求按 prompts.md / requirements.md 里 `<子模块名>` 字段分组后再各自归类。

### 3.3 构造输出路径

- 单模块：`openspec/changes/<change>/art/raw/<filename>.png`
- 多模块：`openspec/changes/<change>/art/raw/<子模块名>/<filename>.png`
- **L2 临时画布**：`openspec/changes/<change>/art/raw/_merged/batch_<N>.png`（保留，不删，方便回查）

文件名优先用 prompts.md 里指定的；否则用 `序号-资源名.png`。**严禁** `ChatGPT Image ...png` 之类的默认名。

### 3.4 L1 命令模板（进程内并行多张独立画作）

单次 codex exec 一次给 ≤12 张独立画作的清单，imagegen 并行处理：

```bash
codex exec -s workspace-write "请使用 imagegen 系统 skill 批量生成以下 N 张独立图片，可并行处理。

# 待生成清单
[
  {
    \"index\": 1,
    \"file\": \"openspec/changes/01-foo/art/raw/sword.png\",
    \"size\": \"1024x1024\",
    \"transparent\": false,
    \"prompt\": \"A glowing steel longsword, ornate hilt, fantasy style\",
    \"negative\": \"blurry, watermark, text, signature\"
  },
  {
    \"index\": 2,
    \"file\": \"openspec/changes/01-foo/art/raw/shield.png\",
    \"size\": \"1024x1024\",
    \"transparent\": false,
    \"prompt\": \"A round wooden shield with iron rim\",
    \"negative\": \"blurry, watermark, text\"
  }
  // ...
]

# 执行要求
- 按 index 顺序逐项生成（imagegen 可并发，但要逐项落盘）
- 每张失败请独立标记失败原因，**继续下一张**，不要因单张失败中断
- 透明背景项必须真透明（PNG alpha 通道有效）

# 完成标准 — 必须返回严格 JSON
[
  { \"index\": 1, \"file\": \"...\", \"size_bytes\": 123456, \"status\": \"ok\" },
  { \"index\": 2, \"file\": \"...\", \"size_bytes\": 0, \"status\": \"failed\", \"error\": \"quota exceeded\" }
]
不要输出 JSON 以外的 markdown / 解释 / 重试 —— 失败由调用方决定是否重试。"
```

### 3.5 L2 命令模板（合并画布 + 后切割）

将 N 个小资源合并到一张 1024×1024 透明画布，imagegen 一次生成，**省下 N-1 次调用**：

```bash
codex exec -s workspace-write "请使用 imagegen 系统 skill 生成 1 张透明背景画布，画布上独立绘制 N 个小型美术资源。

# 画布规格
- 尺寸：1024x1024
- 背景：完全透明
- 落盘：openspec/changes/01-foo/art/raw/_merged/batch_1.png

# 待绘制资源清单（N 项）
1. sword_icon — A small glowing longsword icon, top-down, fantasy
2. shield_icon — A small round shield icon, top-down
3. staff_icon — A small wooden staff icon, top-down
// ...

# 关键布局要求（必须严格遵守）
- 每个资源大约占 200×200 像素区域（最终 256×256 切割框）
- 资源间至少留 32 像素全透明 padding（alpha=0）
- 不要画连接线 / 装饰边框 / 共享背景
- 不要在资源里写文字 / 数字标签
- 按从左上到右下顺序排列（这是为了后续切割回填资源名）

# 完成标准 — 必须返回严格 JSON
{
  \"canvas\": \"openspec/changes/01-foo/art/raw/_merged/batch_1.png\",
  \"size_bytes\": 123456,
  \"status\": \"ok\",
  \"grid_rows\": 3,
  \"grid_cols\": 3,
  \"layout_order\": [\"sword_icon\", \"shield_icon\", \"staff_icon\", ...]
}
失败请说明原因，不要重试。"
```

**关于 grid_rows / grid_cols**：必须由 Claude 在构造提示词时就指定（如 9 个资源 → 让 Codex 画 3×3），Codex 在 JSON 里原样回报。Claude 后续 bucketize 切割结果时用这俩参数。

### 3.6 L2 切割与重命名

L2 画布生成后，调本地工具切割（**不消耗 Codex 额度**）：

```bash
.venv/Scripts/python tools/ImageCut_Tool/image_cut.py \
  openspec/changes/<change>/art/raw/_merged/batch_1.png \
  -o openspec/changes/<change>/art/raw/_merged/batch_1_cut \
  --alpha 16 --min-area 80 --padding 2 --debug --json
```

输出：
- `batch_1/<sheet>_001.png` ~ `_NNN.png`：按 alpha 连通域切出的独立资源
- `batch_1/<sheet>_debug.png`：带编号 bounding box 的预览图
- `batch_1/<sheet>_manifest.json`：含每张子图的 bbox 坐标 `(x, y, w, h)`

**资源名映射**（关键：必须做 row bucketize，不能直接按 (y, x) 排序）：

```python
# 直接按 (y, x) 排序会出 bug：同一行 icon 的 y 中心可能差几像素（imagegen 自由布局），
# 导致跨行错位。正确做法：先按 y 中心除以 (canvas_h / ROWS) 分行，再每行内按 x 排序。
CANVAS_H = manifest["size"]["height"]
ROWS = <Codex 返回的 grid_rows，如 3x3 = 3>
ROW_H = CANVAS_H / ROWS

def row_then_x(s):
    bb = s["bbox"]
    cx = bb["x"] + bb["width"] // 2
    cy = bb["y"] + bb["height"] // 2
    return (int(cy / ROW_H), cx)

sprites_sorted = sorted(manifest["sprites"], key=row_then_x)
# sprites_sorted[i] 对应 layout_order[i]
```

**约定**：L2 提示词必须让 Codex 在 JSON 汇总里**显式返回 `grid_rows` 和 `grid_cols`**（如 9 个资源 = `"grid_rows": 3, "grid_cols": 3`），否则 Claude 无法 bucketize。

**异常处理**：
- 若 ImageCut 切出的张数 ≠ 清单数 → Claude `Read` debug.png 视觉校正，必要时打断用户
- 若 layout_order 顺序与视觉排布不符（imagegen 没遵守"从左上到右下"）→ 视觉校正
- 切出的 PNG 实际尺寸可能 < 目标尺寸（imagegen 自由布局）→ 默认接受，不强行 resize

### 3.7 失败重试（L1 + L2 共用）

批次跑完后：

1. 收集本批所有 `status: failed` 项 → 重试队列
2. **整批重试 1 次**（同一 codex exec 命令，只塞失败项）
3. 仍失败 → 最终标 `failed`，写入生成记录，**不再重试**
4. **L2 批次整体失败**（如画布完全画错）→ 自动降级到 L1 单张模式重跑该批

### 3.8 验证落地

每次 codex exec 返回后：
1. `Bash` 检查每个目标文件存在且 size > 1KB
2. 解析 Codex 返回的 JSON 汇总，对照清单
3. L2 额外：`image_cut.py` 切割完成 + 张数对应

### 3.9 写 `生成记录.md`

文件位置：`openspec/changes/<change>/art/raw/生成记录.md`（多模块时每个子模块目录各有一份）

模板：

```markdown
# 生成记录

生成时间：YYYY-MM-DD HH:MM
执行模型：Codex CLI vX.X.X（imagegen system skill）
总数：N，成功：N，失败：N
分档：L1 ×N1（M1 批），L2 ×N2（M2 批），L0 ×N0

| # | 档 | 资源名 | 文件 | 尺寸 | 状态 | 提示词摘要 | 备注 |
|---|---|---|---|---|---|---|---|
| 1 | L1 | sword | sword.png | 1024x1024 | ok | A glowing steel longsword... | batch_1 |
| 2 | L2 | health_icon | health_icon.png | 256x256 | ok | A red heart icon... | _merged/batch_2/_001 |
| 3 | L1 | shield | shield.png | 1024x1024 | failed | A round wooden shield... | quota exceeded（重试后仍失败）|
```

### 3.10 更新需求文件头部

在 `art/requirements.md` 与 `art/prompts.md` 文件**顶部**追加/更新（保留正文不动）：

```markdown
美术素材状态: 已处理
处理日期: YYYY-MM-DD
执行 SKILL: codex-image-gen
输出目录: art/raw/
生成记录: art/raw/生成记录.md
批量分档: L1 ×N1，L2 ×N2，L0 ×N0
```

若部分失败：状态写 `部分已处理`，并在记录里点名失败项，等用户决定是否重试。

## 四、复用提示

- 同 change 内所有图建议**一次性**走 SKILL，避免多次启动
- prompts.md 中相邻的小尺寸透明资源会被自动归到 L2，**强烈建议**把 icon 类提示词集中写
- L2 画布临时文件 `_merged/` 保留供回查，不入 git 时在 .gitignore 加 `art/raw/_merged/`

## 五、常见陷阱

| 陷阱 | 现象 | 处理 |
|---|---|---|
| Codex 未登录 | `codex doctor` 报 `no Codex credentials` | 停手，提示用户跑 `codex login` |
| 输出路径写绝对路径 | 文件落到 Codex 沙箱镜像里，真实工作树没有 | 一律用相对项目根的路径 |
| 提示词包含中文且图里出现乱码 | imagegen 对中文文字渲染不稳 | 提示词改英文；如果非要中文文字，在生成记录里标注"文字层需后期" |
| 假装成功 | Codex 返回错但流程继续标 ok | **禁止**——逐张验证文件存在 + size > 1KB |
| L1 单批塞太多 | 单次任务超时 / 输出 JSON 截断 | L1 单批默认 8、上限 12，超过自动分批 |
| L2 单批塞太多 | 画布过密 / 资源粘连 / 切割误判 | L2 单批 ≤64（按 128px/格），优先小批量 4×4=16 |
| L2 padding 不足 | ImageCut 把两个资源切成一个 | 提示词强调 ≥32px 透明 padding；切割时用 `--alpha 16 --open 1` 激进分离 |
| L2 imagegen 不按顺序排 | bbox 排序与 layout_order 错位 | Read debug.png 视觉校正；混乱严重时降级到 L1 |
| L2 切出数 ≠ 清单数 | 部分资源粘连或丢失 | 比对 manifest.json 与清单；差值 ≤2 时视觉校正，差值大时整批降级 L1 |
| 重复生成 | 没看头部 `美术素材状态` 字段 | 处理前先 grep 该字段；已是「已处理」就问用户 |
| 在 `art/raw/` 之外乱写文件 | 测试图、debug 图散落 | 严格只写 `art/raw/`（含 `_merged/` 子目录）与对应 `生成记录.md` |
| 失败死循环 | 同一张反复重试烧额度 | 重试上限 **1 次**整批；仍失败标 failed，让用户决定 |
| L2 工具链缺失（新机器） | `tools/ImageCut_Tool/` 在 .gitignore 内，git clone 后不存在 | 提示用户跑 [setup.md](../../../setup.md) 重建 tools/；或临时降级 L1（每张独立画作）继续 |

## 六、与 ai-art 的接力关系

如果用户说"实现和处理美术素材"且未先经过 ai-art 的需求/提示词撰写阶段：
- **先**让 ai-art 完成 `requirements.md` + `prompts.md` 撰写
- **再**调本 SKILL 实际出图

如果 ai-art 流程描述里写"调绘图模型逐项生成图片"那一步：
- 当前对话是 Claude → 用本 SKILL 替代该步
- 当前对话是带绘图工具的模型（GPT-Image 系列） → 按 ai-art 原流程直接调

## 七、踩坑实录（2026-06-26 实测 codex-art-gen MCP 时的教训）

> 本节记录写新工具/调用 codex 时翻过的车。**写消费 codex 的代码前必读**。

### 7.1 透明背景 — codex 内置 image_gen **不支持原生透明**

- **症状**：让 codex `image_gen` 传 `transparent: true`，输出 PNG 是 `RGB` 模式（不是 RGBA），里面画了**仿 PS 棋盘格图案**冒充"透明"，alpha 全是 255。
- **根因**：codex 内置 `image_gen` 工具底层是 `gpt-image-2`，按 imagegen SKILL.md 原文 "`gpt-image-2` does not support `background=transparent`"。真透明只能走 `gpt-image-1.5 --background transparent`（需付费 `OPENAI_API_KEY`，独立于 ChatGPT 订阅）。
- **规避**：走 **chroma-key 工作流**（codex 推荐方案）：
  1. prompt 里要求 `#00ff00` 纯绿背景，**并明确"主体不能含纯绿"避免误抠**
  2. 本地用 `tools/chroma_key_tool/chroma_key.py` 去绿（threshold 80 / soft-edge 30 / despill 0.5 默认参数实测可用）
  3. 再做切图等下游步骤

### 7.2 prompt 工程 — 给完整 JSON 模板，模型会偷懒

- **症状**：第一次 dispatch_l2 用 `gpt-5.4-mini`，**0 次 image_gen 调用**（codex 直接抄 prompt 末尾的 JSON 模板返回，假装"ok"）。
- **根因**：mini 推理弱 + prompt 末尾给了完整 JSON 答案 → 走捷径 copy-paste。
- **规避**：
  - 末尾不要给完整答案模板，只给字段名 + 占位符
  - 关键 tool call 用强约束：「**必须实际调用 image_gen 工具…如果跳过将被视为严重错误…生成结果会被本地校验**」
  - 推理强度敏感的任务用 `gpt-5.5`，不用 `gpt-5.4-mini`

### 7.3 Windows codex.cmd shim — Python subprocess 不能直接 exec

- **症状**：`asyncio.create_subprocess_exec("codex", ...)` 报 `FileNotFoundError: [WinError 2]`。
- **根因**：npm 装的 codex 是 `.cmd` shim（`%APPDATA%\npm\codex.CMD`），Windows `subprocess_exec` 不解析 PATH 也不能直接执行 .cmd。
- **规避**：
  ```python
  import shutil
  CODEX_EXE = shutil.which("codex")
  USE_SHELL = CODEX_EXE.lower().endswith(".cmd")
  real_args = ["cmd.exe", "/c", CODEX_EXE, *args] if USE_SHELL else [CODEX_EXE, *args]
  # 用 create_subprocess_exec(real_args)，让 Python list2cmdline 自动转义参数
  ```
- **关键**：**不要用 `create_subprocess_shell` 手动拼 cmdline** —— TOML 数组 `["..."]` 的双引号会被 cmd.exe 吞掉，导致 `-c writable_roots=...` TOML 解析失败。

### 7.4 消费外部工具前必须先看接口（最常翻车）

- **症状**：sheet_cutter 报 `'list' object has no attribute 'get'` + `KeyError: 'path'`。
- **根因**：`image_cut.py --json` 输出 `manifest_all.json`（不是 `<stem>.json`），结构是 `list[dict]`（不是 dict），sprite 字段叫 `file`（不是 `path`）。**没跑过它的 --json 输出就写了消费代码**。
- **强制规避**：写消费外部工具的代码前，**必须先在终端跑一次实测**：
  ```bash
  .venv/Scripts/python tools/ImageCut_Tool/image_cut.py --help
  .venv/Scripts/python tools/ImageCut_Tool/image_cut.py sample.png -o /tmp/cut --json
  cat /tmp/cut/*.json | head -30
  ```
- **绝对不能凭印象写 manifest 解析代码。**

### 7.5 prompts.md `file` 字段路径规约 — 必须 `raw/<cat>/<name>.png` 三段

- **症状**：bucket 把每张图归到自己的"分类"（取了文件名当 category）。
- **根因**：`raw/button_normal.png` 只两段，category 解析成 `button_normal.png`；需要 `raw/ui/button_normal.png` 三段才能识别出 `ui` 类别。
- **规避**：prompts.md 写 file 字段时**必须三段**，批处理工具不会替你修正路径分类。

### 7.6 MCP 通用工具不该硬编码业务

- **症状**：`bucketizer.py` 写死 `L2_CATEGORIES = {"weapon",...,"ui"}`；`batch_parser.py` 写死项目 `STYLE_BASE`。每个新项目要 fork MCP 改硬编码。
- **根因**：把项目业务逻辑塞进通用工具，违背 MCP 抽象。
- **规避**：MCP 工具输入完全参数化（`l1_categories=[]` / `l2_categories=[]` / `style_base=""` / `neg_base=""`），由调用方注入项目业务。

### 7.7 路径变量命名 confusion

- **症状**：`rename_by_layout_order(art_raw_root=art_raw)` 路径重复 `raw/ui/raw/ui/...`。
- **根因**：参数叫 `art_raw_root`，但 item.file 已含 `raw/` 前缀，根目录应传 `art_dir` 不是 `art/raw`。变量名跟实际语义不一致。
- **规避**：路径拼接函数加最简单的 unit test：
  ```python
  def test_rename_path():
      assert (Path("/proj/art") / "raw/ui/button.png") == Path("/proj/art/raw/ui/button.png")
  ```

### 7.8 MCP 测试别绕过 stdio 协议层

- **症状**：用 `python -c "import server; tool_dispatch_l2(...)"` 直接调函数验证逻辑，绕过了 MCP stdio。
- **根因**：图快走 Python import，但 MCP server 进程根本没启动，相当于"测了引擎没测车壳"。
- **规避**：MCP 改动后**必须**让 Claude Code 重启加载 MCP，用 `mcp__<server>__<tool>` 工具走真实 stdio 协议测一次。

### 7.9 MCP stdio 下 subprocess 必须 `stdin=DEVNULL`（不然死锁）

- **症状**：`tools/codex-art-gen-mcp/sheet_cutter.py` 调 `chroma_key.py` 子进程在 Python `import server` 旁路测试下 1-2 秒完成；同样代码在 **stdio MCP 真测**下 hang 到 60s timeout 报 "chroma_key.py timeout 60s"。
- **根因**：MCP stdio 模式下，server.py 父进程的 stdin 是 MCP 客户端协议管道。`subprocess.run(cmd, capture_output=True)` 不显式设 `stdin`，子进程**继承父进程的 stdin**，于是 chroma_key.py 子进程→父进程→MCP 客户端形成环形等待，Windows + asyncio 下 100% 复现死锁；import 旁路下 stdin 是 tty，所以测不出。
- **规避**：MCP server 里**所有** `subprocess.run` / `subprocess.Popen` 必须显式 `stdin=subprocess.DEVNULL`。`asyncio.create_subprocess_exec` 同理。**只在 MCP server 里要这样**，普通 CLI 脚本不用。
- **教训**：7.8 说"import 旁路 ≈ stdio 真测"是错的，stdio 模式有 subprocess 继承陷阱，import 旁路完全测不到。

### 7.10 server.py 别强依赖 codex 返回 JSON，纯磁盘核验最稳

- **症状**：dispatch_l2 在 stdio 真测下，**codex 6,653 tokens 直接返回 `{"size_bytes":123456,"status":"ok",...}`，画布根本没出**——codex 把 prompt 末尾的"返回格式"示例 JSON 当成答案原样吐回了，0 次 image_gen 调用。L1 同样模板曾经成功只是 gpt-5.5 随机性。
- **根因**：7.2 已经警告过 prompt 不要给完整答案模板，但 server.py 当时仍**强依赖 `codex_result["result"]` 的 list/dict 结构来对齐 items**——codex 复读 → list 结构刚好"合法" → server.py 接受 → 假装成功。
- **规避**（**两步连改**才能根治）：
  1. **删 prompt 里的"返回格式"段**：不给字段名 + 不给占位数值。改成 `"任务由调用方通过磁盘文件是否存在 + 体积 > 1KB 判定"`，断 codex 复读路径。
  2. **server.py 改纯磁盘核验**：不用 `codex_result["result"]`；遍历**原始** items，对每个 `item.file` 自己 `Path.exists()` + `stat().st_size > 1024`，再走 chroma_key。L2 同理：codex 完成后立即 `canvas_path.exists()` 早期检查，给清晰错误"codex 未真出图"。
- **副作用**：`size_bytes` 永远是 server.py 本地 `stat()` 算出来的真值，不可能被伪造。出图失败也不会因为 codex 嘴硬"status:ok"而被误判成功。
- **本次 v21 验证**：14 L1 + 8 L2 = 22 张一把过，0 失败。

---

## 八、Definition of Done

- [ ] 每张需求条目都在 `art/raw/`（或子模块目录）有对应 PNG，size > 1KB
- [ ] `生成记录.md` 完整覆盖所有条目（ok / failed 都要写）
- [ ] `requirements.md` 与 `prompts.md` 头部已更新状态字段
- [ ] 失败项已明确告知用户，未隐瞒
