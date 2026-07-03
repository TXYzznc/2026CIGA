---
name: ui-asset-splitting
description: 把用户已确认的 UI 效果图（mockup）拆分成可用于搭 Prefab 的独立美术素材——背景图 1 张 + 各组件/图标/状态变体若干张。触发：效果图已确认、拆素材、切图、扣图、UI 素材拆分、mockup 拆分、组件状态图。
---

# UI 美术效果图拆分

## 一、定位与边界

**做的**：输入一张**已确认**的 UI 效果图（`art/mockups/<FormName>.png`），分析其中包含哪些独立美术单元（背景 + N 个图标/按钮/进度条等组件，含必要的状态变体），调 Codex 重新生成这些独立素材（不是从原图抠图，是基于原图描述重新生成同风格的透明背景小图），本地抠图+切图后落到 `art/raw/<FormName>/`，最终搬进 `Assets/Resources/Sprite/UI/<FormName>/`。

**不做的**：
- 效果图本身的生成（→ [codex-image-gen](../codex-image-gen/SKILL.md)）
- 效果图的需求/提示词撰写（→ [ai-art](../ai-art/SKILL.md)）
- Prefab 搭建、UIForm 脚本编写（→ client-unity）

**调用关系**：
```
codex-image-gen（出 mockup，用户确认）
       ↓
ui-asset-splitting（本 SKILL — 拆成可用素材）
       ↓
art/raw/<FormName>/*.png → 搬进 Assets/Resources/Sprite/UI/<FormName>/
       ↓
client-unity 搭 Prefab（直接用这些 Sprite）
```

## ⚠️ 一条铁律（最容易违反 — SettingsForm 2026-07-01 实测踩坑）

**禁止从 mockup 直接裁矩形当素材。** 组件素材必须是**透明背景**的独立小图，合法来源只有两条：

1. **Codex 绿幕重生成**（默认 — 带描边/高光/质感的视觉组件：图标、圆环、handle、面板边框）：§3.2/3.3 在 `#00ff00` 绿幕画布重画 → chroma_key 去绿 → image_cut 切割
2. **程序化 PIL 生成**（仅限**纯色/纯几何极简元素**：纯色圆角矩形按钮底、纯色滑块轨道段、纯色分割条）：直接画透明底 PNG

**判定该走哪条**：元素有描边/渐变/高光/质感 → 走 Codex 绿幕；元素是单色几何（矩形/圆/线，无质感）→ 可程序化 PIL。**两条路的产出四角 alpha 都必须为 0。**

**为什么禁止裁 mockup**：mockup 里的组件画在深色面板底上（如 `#1A1C2E`），裁一块矩形下来 = 带面板底色的**不透明补丁**，贴到 UI 上就是一个个深色方块盖住面板。SettingsForm v2 第一次拆分正是犯此错：CloseIcon_X / RadioCircle_Normal / RadioCircle_Selected / SliderTrack / SliderFill 5 张全带 `#1A1C2E` 不透明底（PIL 采样 alpha 全 255）。

**普通文字不进素材**：所有文案（标题/标签/数值/按钮文字/键名）在 Prefab 里走 **TMP_Text 独立节点**，素材只做纯色/纹理底。只有**特殊艺术字**（logo、标题美术字、带特效的字形）才作为图片素材拆分。

## 二、前置条件

1. ✅ 目标 `art/mockups/<FormName>.png` 已存在，且 `art/prompts.md` 头部状态字段标记该图**已处理且用户已确认**（未确认先停手，回去问用户）
2. ✅ `openspec/changes/<change>/art/prefab-layout.md` 存在且用户已确认（v3 结构先行流程强制前置，缺失即阻塞交回主对话；layout 是**唯一**的拆分清单来源——每个节点 = 1 张素材，节点的 `states: [...]` 决定状态变体数量）
3. ✅ `codex --version` 可用
4. ✅ `tools/chroma_key_tool/chroma_key.py` 与 `tools/ImageCut_Tool/image_cut.py` 存在（`ls` 检查）；缺失则提示用户按 setup.md 重建 tools/，不可伪造产物
5. ✅ `Assets/Editor/UISpriteImportProcessor.cs` 存在（自动导入设置工具，2026-06-28 随本 SKILL 一起建立）——若不存在说明项目未同步该工具，需先补上再继续，否则素材搬进 Resources 后纹理类型不对，Prefab 用不了

## 三、执行流程

### 3.1 分析 mockup，列出拆分清单

`Read` 目标 mockup 图 + `openspec/changes/<change>/art/prefab-layout.md`，按 layout 节点树逐项列出：

- **背景**：1 张（面板底板/全屏插画背景，去除所有文字与可交互元素后的"干净"场景图）
- **组件**：layout 节点树中每个含 `Image` component 的节点 → 1 张素材（按钮、图标、进度条底、卡片底板……）
- **状态变体**：layout 中每个含 `states: [normal, pressed, disabled, ...]` 的节点 → 每态 1 张，**每态独立生成，禁止拼在同一张画布里同节点多态并列**（阶段 3 已按每态独立提示词生成 mockup 变体图或直接生独立素材图）

**画布够不够的处理原则**：
- **一张 1024×1024 绿幕画布装得下** → 单次 L2 生全部组件+状态变体（默认路径）
- **装不下（组件数 > 12，或含大尺寸面板边框挤走其他组件）** → **拆多张绿幕画布**（`_merged/batch_1.png` / `_merged/batch_2.png`），每张仍走 L2；不要退化成逐个 L1
- **状态变体独立画布**：如按钮有 normal/pressed/disabled 3 态，可以放同一张画布不同格子（layout_order 里体现），但一格只放 1 态，禁止一格里画 3 变体

写一份拆分清单（资源名 + 类型 + 尺寸 + 是否透明 + 状态变体列表 + 分配到哪张 batch），可以是临时清单不必落盘，直接进入 3.2。

### 3.2 构造拆分提示词

**背景**：不是从原图"抠"出来，是让 Codex 基于原图描述**重新生成一张不含 UI 文字/控件的纯场景背景图**，保持同风格、同构图、同光影。

**组件/图标**：**合并成一张画布**的提示词——告诉 Codex 在一张 1024×1024 **纯绿幕背景**（`#00ff00`）画布上，按网格布局把这个 Form 的全部组件（含状态变体）都画出来，每个之间留 ≥32px 纯绿 padding，强调主体本身不能含纯绿色。透明诉求走 chroma-key 流程（codex 内置 image_gen 不支持原生透明，参考 [codex-image-gen SKILL §7.1](../codex-image-gen/SKILL.md)）。同一组件的不同状态作为画布里独立的格子列出（如 `button_save_normal` / `button_save_disabled`），在 `layout_order` 里体现。

### 3.3 调用 Codex 生成

- **背景图**：走 L0/L1（单图，不透明，尺寸与原 mockup 一致或按面板实际尺寸），单独一次调用
- **组件/图标**：**默认一律走 L2（合并画布）**——UI 组件不需要单独高精度出图，一张 1024×1024 绿幕画布通常能装下一个 Form 的全部组件+状态变体（面板边框、按钮、图标、进度条……）。**不要**因为某个组件（如面板边框）尺寸看起来更大就单独拆出去走 L1——除非单张画布实在装不下（组件数 > 12 或单组件细节复杂到挤不下其他组件），才分两批画布，仍然是 L2，不要退化成逐个 L1
- 画布规格、`grid_rows`/`grid_cols` 约定、layout_order 排序方式，完全复用 [codex-image-gen SKILL §3.5/§3.6](../codex-image-gen/SKILL.md)（L2 命令模板 + row-then-x 排序算法），**唯一区别**：本 SKILL 的 L2 画布背景固定为绿幕 `#00ff00`（不是透明请求），因为要走 chroma-key 流程
- 输出路径：`openspec/changes/<change>/art/raw/<FormName>/`（背景）+ `art/raw/<FormName>/_merged/batch_1.png`（合并画布，保留不删）
- 文件名：`<FormName>_bg.png`（背景）、`<FormName>_<component>_<state>.png`（切割重命名后的组件，无状态变体的省略 `_<state>`）

### 3.4 本地后处理（不消耗 Codex 额度，顺序固定：先抠图，再切图）

```bash
# 第 1 步：对整张合并画布先做绿幕去背景（不是切完再逐张抠）
.venv/Scripts/python tools/chroma_key_tool/chroma_key.py <merged_batch.png> -o <merged_batch_alpha.png> --threshold 80 --soft-edge 30 --despill 0.5

# 第 2 步：再对去绿幕后的透明画布切割成独立素材
.venv/Scripts/python tools/ImageCut_Tool/image_cut.py <merged_batch_alpha.png> -o <cut_dir> --alpha 16 --min-area 80 --padding 2 --json

# 第 3 步：按 layout_order（codex 返回的顺序）+ row-then-x 排序，把 cut_dir 里的 _001/_002... 重命名为语义化文件名
```

**为什么必须先抠图再切图**：去绿幕是按颜色距离做 alpha 计算，对单张大画布做一次即可；如果先切成小图再分别去绿，纯绿背景在切割阶段可能因为画布本身就是绿底而无法用 `--alpha` 连通域算法正确分割边界（连通域识别依赖前景与背景的 alpha 差异，没做 chroma-key 之前画布没有 alpha 通道）。**顺序错了 `image_cut.py` 直接没法工作**，这不是风格问题是技术依赖顺序。

**验证**：切割完成后用 `Bash`/`Read` 检查每个目标文件存在、size > 1KB、PIL 能正常打开。**⚠️ alpha 透明硬检查（强制，不做等于没拆）**：对每张组件素材（非满铺背景 `_bg.png`）采样四角 + 网格点 alpha，**四角 alpha 必须 = 0，且存在 alpha < 40 的透明像素**——`mode == RGBA` 不代表真透明，从 mockup 裁出的深色矩形块同样是 RGBA 但 alpha 全 255。任一张四角不透明 = 该张走了"裁 mockup"歪路（违反 §一铁律），必须回 §3.2 用 Codex 绿幕重生成或程序化 PIL 重做，**不得入库**。检查脚本：

```bash
.venv/Scripts/python - <<'PY'
import glob, os
from PIL import Image
for p in sorted(glob.glob('openspec/changes/<change>/art/raw/<FormName>/*.png')):
    if p.endswith('_bg.png'): continue          # 满铺背景图豁免
    img = Image.open(p).convert('RGBA'); w, h = img.size; px = img.load()
    corners = [px[2,2][3], px[w-3,2][3], px[2,h-3][3], px[w-3,h-3][3]]
    alphas = [px[x,y][3] for y in range(0,h,max(1,h//30)) for x in range(0,w,max(1,w//30))]
    transp = sum(1 for a in alphas if a < 40)
    ok = max(corners) == 0 and transp > 0
    print(f"{'OK  ' if ok else 'FAIL'} {os.path.basename(p)} 四角alpha={corners} 透明采样={transp}/{len(alphas)}")
PY
```

**任一行 FAIL 即禁止进入 §3.5 入库**。切出张数与清单数核对，若不一致按 [codex-image-gen SKILL §3.6 异常处理](../codex-image-gen/SKILL.md)做视觉校正。

### 3.5 搬运入库 + 自动导入设置

```bash
mkdir -p "Assets/Resources/Sprite/UI/<FormName>"
cp art/raw/<FormName>/*.png "Assets/Resources/Sprite/UI/<FormName>/"
```

文件落到 `Assets/Resources/Sprite/UI/` 下后，Unity 重新聚焦/编译时 `UISpriteImportProcessor`（`Assets/Editor/UISpriteImportProcessor.cs`）会**自动**在导入阶段把 Texture Type 设为 `Sprite (2D and UI)` + 关闭 mipmap + Bilinear 过滤，**不需要手动在 Inspector 里改**。若文件是在 Unity 未运行时拷贝进去的，下次打开 Editor 触发导入时才会生效；可用 `unity-skills` MCP 的 `manage_asset` 主动触发一次 reimport 确认。

### 3.6 写生成记录

`art/raw/<FormName>/生成记录.md`，格式同 [codex-image-gen SKILL §3.9](../codex-image-gen/SKILL.md)，额外记录"拆分清单"（背景 1 张 + 组件 N 张，含状态变体）与搬运目标路径。

## 四、并发处理（多张 mockup 同时拆）

多张已确认的 mockup 互不依赖时，**主对话必须 fan-out，禁止自己手动逐张处理**：每张 mockup 对应一个通用 Agent（`Agent` 工具，`subagent_type: general-purpose` 或按需 `client-unity`），在**同一条消息里**并行发起全部调用，各自独立跑 3.1-3.6，互不等待。主对话本身只做任务分配和最终汇总，**不要**因为"图不多"或"已经看过内容"就在主对话里手动跑 chroma-key/image_cut 命令——批量重复性工作一律下放给子 Agent，这是本 SKILL 区别于单图 codex-image-gen 调用的关键效率点（2026-06-28 实测：10 张 mockup 中有 4 张被主对话错误地手动逐张处理，违反此规则，已纠正）。

## 五、踩坑实录（2026-06-28 实测 10 个 Form 拆分时的教训）

1. **`chroma_key.py -o` 必须传文件夹，不能传文件名**——传一个看起来像文件名（以 `.png` 结尾）的路径会被当成"文件夹"创建，产生 `xxx_alpha.png/batch_1.png` 这种嵌套怪路径。**规避**：`-o` 统一传目录路径（如 `_merged/alpha`），输出文件名由工具自己决定（默认与输入同名）。
2. **Codex 有时会完全无视绿幕网格指令**，生成一张无关的场景图而不是绿幕画布。**规避**：生成后必须先 `Read` 合并画布本身确认是纯绿背景 + 网格状构图，不符合直接重新提交更强措辞的 prompt（强调 "ENTIRE background must be solid green #00ff00" + "N icons arranged in R×C grid"），不要跳过这一步直接抠图。
3. **Codex 有时会漏画清单里的项目**。**规避**：抠图切割后核对实际切出数量与清单数量，**不一致时先 `Read` 画布原图肉眼核对漏了哪个/多了哪个**，缺项单独开一次小画布（仍是 L2，哪怕只放 1-2 个图）补齐，不要将错就错改清单凑数。
4. **`image_cut.py` 切出的张数可能多于预期**（实测 ThreeChoiceForm 6 个目标切出 19 个连通域，多出的 13 个是图标内部纹理断裂出的小碎片）。**规避**：按面积排序，明显小于其他目标 1-2 个数量级的碎片视为噪声丢弃，但**必须先 `Read` debug.png 肉眼确认**哪些是真实目标、哪些是碎片，不能只按数量截断（防止误删一个本来就小的真实组件）。
5. **背景图实际生成尺寸常常不等于请求尺寸**（请求 1920×1080，实际经常拿到 1254×1254 或 1672×941 等）。这是 imagegen 自由构图的正常行为，**不强制 resize**，只要画面内容正确即可直接使用。
6. **despill 后边缘仍可能有轻微绿色残留**（实测 PauseMenuForm/ThreeChoiceForm 个别组件描边处可见）。轻微残留**不阻断交付**，明确记录在生成记录里告知用户即可；只有残留明显影响视觉时才需要单独提高 `--despill` 到 0.7 重跑该张。
7. **⚠️ 背景图可能被错误的"最近生成文件"覆盖（实测 5/11 个 Form 中招，最严重的一类坑）**：codex 内部常用 `Get-ChildItem ... | Sort-Object LastWriteTime -Descending | Select-Object -First 1` 取"最近 1 张"再复制到目标路径；如果背景图和组件合并画布在相近时间窗口内生成（同一次 exec 里混着生成，或多个 Form 的任务前后脚执行），这个逻辑会**抓错文件**，把别的 Form 的绿幕组件画布甚至别的 Form 的空面板边框复制成了当前 Form 的 `_bg.png`。**规避**：
   - 背景图（L1 单图）**单独一次 codex exec 调用**生成，不要和组件合并画布（L2）混在同一次 exec 里
   - 多个 Form 的背景图可以在一次 exec 里批量生成（如 §三 示例的多 index 清单），但**输出文件名要明确且不重复**，避免 codex 内部用"第几张最新"做索引映射时序错位
   - **每张 `_bg.png` 落盘后必须 `Read` 工具肉眼核验内容是场景图、不含 UI 元素**，不能只信任 `size > 1KB` 的机械校验，也不能信任 codex 自己汇报的"已核对内容正确"（实测 codex 嘴上说核对过，但文件内容其实是错的）
   - 全部 Form 拆分完成后，建议**收尾阶段统一抽查一遍全部 `_bg.png`**（哪怕之前单独验证过），这是本条踩坑被发现的方式
8. **⚠️⚠️ 直接从 mockup 裁矩形当素材（实测 SettingsForm v2 中招 5/10 张，最典型的绕过规范）**：子 Agent 图省事，跳过 §3.2/3.3 的"Codex 绿幕重生成"，直接从已确认的整面板效果图上按 bbox 裁一块下来当素材。**后果**：mockup 里组件画在深色面板底上，裁下来 = 带 `#1A1C2E` 面板底色的不透明矩形块（alpha 全 255），贴到 UI 上是一个个深色方块盖住面板背景。CloseIcon_X / RadioCircle / SliderTrack / SliderFill 全中招，拖到阶段 6 用户肉眼才发现。**根因三连**：① SKILL 早期把"不是从原图抠图"埋在描述段没设硬门；② §3.4 旧验证只查"mode 含 alpha 通道"（深色矩形也是 RGBA，照样过）；③ 主对话 delegate 时用"MVP 只拆 default 态"简化，被子 Agent 顺手扩大成"连生成方式也省了"。**规避（本次已固化）**：
   - §一「铁律」白纸黑字禁止裁 mockup，明确合法来源只有 Codex 绿幕重生成 + 程序化 PIL 两条
   - §3.4 验证升级为 **alpha 硬检查**（四角 alpha 必须 = 0），跑脚本任一 FAIL 禁止入库
   - **"MVP 简化"只能砍状态变体数量（如只拆 default 态），绝不能砍"透明背景重生成"这个生产方式** —— 简化的是"出几张"，不是"怎么出"
   - 普通文字（标签/数值/按钮文案/键名）不进素材，走 Prefab 里 TMP_Text 独立节点；只有特殊艺术字（logo/标题美术字/带特效字形）才作为图片拆分

## 六、Definition of Done

- [ ] 每个 mockup 拆出 1 张背景 + N 张组件（含必要状态变体），均落在 `art/raw/<FormName>/`
- [ ] **每张组件素材都由 Codex 绿幕重生成或程序化 PIL 生成，无一从 mockup 直接裁矩形**（§一铁律 + §五踩坑 8）
- [ ] **alpha 硬检查全 OK**：§3.4 脚本跑完所有组件素材（非 `_bg.png`）四角 alpha=0、有透明像素，无一行 FAIL
- [ ] **无普通文字进素材**：标签/数值/按钮文案/键名走 TMP_Text 节点；仅特殊艺术字作图片
- [ ] **每张 `_bg.png` 已用 Read 工具肉眼核验是干净场景图**（不含 UI 面板/图标/文字），不是别的 Form 串号过来的组件画布（参考 §五 踩坑 7）
- [ ] 已搬运到 `Assets/Resources/Sprite/UI/<FormName>/`
- [ ] `Assets/Editor/UISpriteImportProcessor.cs` 存在且生效（抽查 1 张素材的 `.meta` 确认 `textureType: 8`）
- [ ] `生成记录.md` 完整覆盖所有拆分项
- [ ] 失败/抠图瑕疵项已明确告知用户，未隐瞒
