# SKILL 系统索引

> AI 每次任务开始时应先查阅本文档，确认是否有可复用的 SKILL；不要重复造轮子。
>
> **结构**：本目录下每个 SKILL 都是一个独立子目录，含 `SKILL.md` 主入口，可能附带 `references/`、`scripts/`、`templates/`、`examples/`、`evals/`、`assets/`、`checklists/`、`agents/` 等子目录。Claude Code **不递归扫描**，请勿把 SKILL 移到子目录里。

---

## 一、使用流程

1. **快速匹配** — 在下方"快速匹配表"或"按职能分组"中找到候选 SKILL。
2. **打开 `SKILL.md`** — 理解 trigger / 边界 / 输出形态。
3. **按需读取 `references/`** — 大多数详细规范放在 references 子文件，不会自动加载。
4. **按 SKILL 的 instructions 执行任务** — 严格遵循其规范。

> **不确定时**：调用 `find-skills` 让它帮你搜更广义的语义匹配。

---

## 二、按职能分组

### 2.1 常用项目 SKILL

> **代码分析 / 架构理解 / 影响范围 / Bug 追踪 / 重构 impact** 由 `codebase-memory` MCP 承担——详见 [CLAUDE.md §八](../CLAUDE.md)「codebase-memory MCP 使用准则」。

| SKILL | 适用场景 |
|---|---|
| [ai-art](./ai-art/SKILL.md) | AI 绘图提示词与素材实现（前置）：图标/立绘/场景/UI 提示词与需求沉淀（见 `references/drawing-prompt-*.md`） |
| [codex-image-gen](./codex-image-gen/SKILL.md) | AI 绘图实际出图（后置）：通过 `codex exec` 把出图任务外包给 Codex CLI，落盘到 `art/raw/` |
| [ui-asset-splitting](./ui-asset-splitting/SKILL.md) | UI 效果图拆分（更后置）：已确认 mockup 拆成背景+组件+状态变体素材，搬进 `Assets/Resources/Sprite/UI/` |
| [deep-research](./deep-research/SKILL.md) | 联网深度研究，收集整理资料（`scripts/research.py`） |
| [dev-tools](./dev-tools/SKILL.md) | 创建 MCP Server / 创建/改进 Skill |
| [document-tools](./document-tools/SKILL.md) | Word/PDF/PPT/Excel/markitdown/docx 复制 |
| [openspec](./openspec/SKILL.md) | 规范化需求 + 变更流程（new / apply / verify / archive） |
| [unity-dev](./unity-dev/SKILL.md) | Unity 综合开发参考（UI/动画/配置表/Editor 总入口） |

### 2.2 项目管理 / 产品 / 规划

| SKILL | 用途 |
|---|---|
| [project-management](./project-management/SKILL.md) | PRD、任务分解、优先级 |
| [task-estimation](./task-estimation/SKILL.md) | 工作量估算、Story Points |
| [milestone-tracker](./milestone-tracker/SKILL.md) | 里程碑、关键路径、EVM |
| [risk-assessment](./risk-assessment/SKILL.md) | 风险识别与缓解策略 |
| [sprint-retrospective](./sprint-retrospective/SKILL.md) | Sprint 回顾、行动项 |
| [competitive-analysis](./competitive-analysis/SKILL.md) | 竞品矩阵、定位分析 |
| [propagate-design-change](./propagate-design-change/SKILL.md) | 跨 ADR / 索引同步 GDD 变更 |
| [ab-testing](./ab-testing/SKILL.md) | A/B 实验设计、样本量、显著性 |
| [grill-me](./grill-me/SKILL.md) | 计划/设计压力测试（拷问） |
| [grill-with-docs](./grill-with-docs/SKILL.md) | ADR / CONTEXT 同步审视 |
| [find-skills](./find-skills/SKILL.md) | SKILL 语义检索 |
| [skill-creator](./skill-creator/SKILL.md) | 新建 / 升级 SKILL |
| [semver](./semver/SKILL.md) | 语义化版本控制 |

### 2.3 游戏设计（顶层 / GDD / 平衡 / 系统）

| SKILL | 用途 |
|---|---|
| [game-design-core](./game-design-core/SKILL.md) | 设计理论核心：循环、动机、手感、有意义选择 |
| [game-design-theory](./game-design-theory/SKILL.md) | MDA / 玩家心理学 / 系统平衡 |
| [design-system](./design-system/SKILL.md) | 单系统 GDD 分步引导（写一个系统） |
| [brainstorm](./brainstorm/SKILL.md) | 引导式构思（从无到有） |
| [review-all-gdds](./review-all-gdds/SKILL.md) | 跨 GDD 一致性 + 平衡审稿 |
| [balance-check](./balance-check/SKILL.md) | 平衡数据 / 曲线 / 优势策略 audit |
| [combat-balancer](./combat-balancer/SKILL.md) | 难度缩放、CR、行动经济 |
| [casino-math-balancer](./casino-math-balancer/SKILL.md) | 赌博类数学：RTP / 庄家优势 / 派彩表 |
| [game-monetization](./game-monetization/SKILL.md) | F2P 经济、IAP、Battle Pass、Gacha |
| [progression-systems](./progression-systems/SKILL.md) | 进度 / XP / 技能树 / 元进度 |
| [achievement-design](./achievement-design/SKILL.md) | 成就 / Trophy / missable / 平台规范 |
| [quest-mission-design](./quest-mission-design/SKILL.md) | 任务系统：状态机、分支、奖励曲线 |
| [puzzle-design](./puzzle-design/SKILL.md) | 谜题、顿悟时刻、教学 |
| [difficulty-curve](./difficulty-curve/SKILL.md) | 难度曲线、DDA、心流 |
| [player-onboarding](./player-onboarding/SKILL.md) | FTUE / 教程 / 30 秒留存 |
| [player-guidance](./player-guidance/SKILL.md) | 空间叙事、sightline、affordance |
| [playtest-digital](./playtest-digital/SKILL.md) | 数字 playtest 方法论 |
| [playtest-driver](./playtest-driver/SKILL.md) | Unity Editor 中自动驱动 playtest：键鼠/UI 注入 + Play Mode 控制 + 日志 + 报告 |

### 2.4 关卡设计

| SKILL | 用途 |
|---|---|
| [level-design](./level-design/SKILL.md) | 关卡基础、节奏、环境叙事 |

### 2.5 美术（2D / Sprite / Texture / 字体 / 排版）

| SKILL | 用途 |
|---|---|
| [art-direction](./art-direction/SKILL.md) | 创意/视觉 brief、品牌延伸 |
| [game-art](./game-art/SKILL.md) | 游戏美术原则、资源管线 |
| [game-ui-design](./game-ui-design/SKILL.md) | 游戏 UI / HUD / 控制器适配 / 可访问性 |
| [texture-art](./texture-art/SKILL.md) | PBR / Substance / 手绘 / Trim sheet |
| [gpt-image-2-style-library](./gpt-image-2-style-library/SKILL.md) | GPT Image2 风格库 + prompt 模板 |
| [typeset](./typeset/SKILL.md) | 字体层级 / 排版优化 |
| [font-pairing-suggester](./font-pairing-suggester/SKILL.md) | 字体配对建议、Google Fonts 替代 |
| [font-selection-cjk](./font-selection-cjk/SKILL.md) | CJK 字体覆盖率检测 |
| [font-subsetting](./font-subsetting/SKILL.md) | 字体子集化（woff2 / SDF / MSDF） |
| [pixel-font-rendering](./pixel-font-rendering/SKILL.md) | 像素字体 / 点阵 / BMFont |
| [image-compression](./image-compression/SKILL.md) | 压缩 PNG/JPEG/WEBP/TGA 美术素材，按 [美术资源规范.md](../美术资源规范.md) 控制各类型尺寸与体积上限 |

### 2.6 美术（3D / Rigging / Animation / VFX / Shader）

| SKILL | 用途 |
|---|---|
| [3d-modeling](./3d-modeling/SKILL.md) | 拓扑 / UV / retopo / LOD / DCC 流程 |
| [rigging](./rigging/SKILL.md) | 骨骼 / IK / 控制器 |
| [animation-systems](./animation-systems/SKILL.md) | 状态机 / blend tree / 程序化动画 |
| [blender-mcp](./blender-mcp/SKILL.md) | Blender MCP：场景检查、Python、GLTF 导出 |
| [vfx-realtime](./vfx-realtime/SKILL.md) | 粒子 / Niagara / VFX Graph / juice |
| [shader-effects](./shader-effects/SKILL.md) | 发光/泛光/扭曲/暗角/扫描线/故障 |
| [agency-technical-artist](./agency-technical-artist/SKILL.md) | Shader / VFX / LOD 管线 / 性能预算 |
| [agency-unity-shader-graph-artist](./agency-unity-shader-graph-artist/SKILL.md) | Unity Shader Graph / HLSL / URP/HDRP |

### 2.7 Unity 引擎实现

| SKILL | 用途 |
|---|---|
| [unity-foundations](./unity-foundations/SKILL.md) | Unity 6 核心：GameObject / Component / Scene / Prefab / SO |
| [unity-ui](./unity-ui/SKILL.md) | UI Toolkit / uGUI / IMGUI |
| [unity-rect-transform](./unity-rect-transform/SKILL.md) | UGUI 空间语言词典：anchor / pivot / sizeDelta / anchoredPosition / preserveAspect / Canvas Scaler + `prefab-layout.md` 模板（art-ui 阶段 1 + client-unity 阶段 5 共享） |
| [unity-animation](./unity-animation/SKILL.md) | Animator / blend tree / Timeline / Cinemachine |
| [unity-shaders-rendering](./unity-shaders-rendering/SKILL.md) | Shader Graph / HLSL / URP / HDRP / SRP |
| [unity-lighting-vfx](./unity-lighting-vfx/SKILL.md) | 烘焙/实时光、APV、Particle、VFX Graph、后处理 |
| [unity-networking](./unity-networking/SKILL.md) | Netcode / Mirror / Photon |
| [unity-ecs-patterns](./unity-ecs-patterns/SKILL.md) | DOTS / Jobs / Burst |
| [unity-architecture-di](./unity-architecture-di/SKILL.md) | 客户端架构 + DI（Zenject / VContainer） |
| [unity-async-patterns](./unity-async-patterns/SKILL.md) | UniTask / Awaitable / 取消令牌 / 协程 |
| [unity-input-correctness](./unity-input-correctness/SKILL.md) | 新输入系统正确模式 |
| [unity-editor-scripting](./unity-editor-scripting/SKILL.md) | Editor 扩展（EditorWindow / Inspector / Drawer） |
| [unity-build-pipeline](./unity-build-pipeline/SKILL.md) | BuildPipeline + batchmode + License v2 + GameCI |
| [unity-skills](./unity-skills/SKILL.md) | Unity Editor REST API（uloop） |
| [addressables-hotfix](./addressables-hotfix/SKILL.md) | Addressables 远程热更新 |
| [save-serialization](./save-serialization/SKILL.md) | 存档 / JsonUtility / Newtonsoft / MessagePack / 版本迁移 |
| [state-machine](./state-machine/SKILL.md) | FSM / 行为树 / NodeCanvas |
| [physics-collision](./physics-collision/SKILL.md) | Rigidbody / Collider / CharacterController / CCD |
| [localization-i18n](./localization-i18n/SKILL.md) | Unity Localization / Smart String / RTL / TMP fallback |
| [uloop-execute-dynamic-code](./uloop-execute-dynamic-code/SKILL.md) | 通过 uloop 在 Editor 内动态执行 C# |
| [uloop-run-tests](./uloop-run-tests/SKILL.md) | Unity Test Runner 执行 + NUnit XML 落盘 |

### 2.8 网络 / 后端 / 数据库

| SKILL | 用途 |
|---|---|
| [arch-api](./arch-api/SKILL.md) | REST / 版本 / HATEOAS / 网关 |
| [game-networking](./game-networking/SKILL.md) | 多人 / netcode / 延迟补偿 / 权威服务器 |
| [multiplayer-game](./multiplayer-game/SKILL.md) | 匹配 / Tick / 兴趣管理 |
| [atomic-matchmaking](./atomic-matchmaking/SKILL.md) | 2PC 匹配 / 连接验证 |
| [algo-rank-trueskill](./algo-rank-trueskill/SKILL.md) | TrueSkill 评分（团队/多人） |
| [jwt-auth](./jwt-auth/SKILL.md) | JWT + 刷新令牌轮转 |
| [oauth-implementation](./oauth-implementation/SKILL.md) | OAuth 2.0/2.1 + PKCE |
| [backend-testing](./backend-testing/SKILL.md) | 单元 / 集成 / API / mock |
| [database-schema-design](./database-schema-design/SKILL.md) | Schema / 索引 / 关系 / 迁移 |
| [redis-best-practices](./redis-best-practices/SKILL.md) | Redis 缓存 / 数据结构 |
| [redis-specialist](./redis-specialist/SKILL.md) | Pub/Sub / 限流 / 分布式锁 / 排行榜 |
| [kafka-development](./kafka-development/SKILL.md) | 事件流 / 分布式消息 |

### 2.9 DevOps / CI/CD / 监控 / 发版

| SKILL | 用途 |
|---|---|
| [devops-deployment](./devops-deployment/SKILL.md) | CI/CD / Docker / K8s / Terraform |
| [github-actions-docs](./github-actions-docs/SKILL.md) | GitHub Actions 官方文档对照 |
| [mobile-cicd](./mobile-cicd/SKILL.md) | Fastlane / TestFlight / Firebase Distribution |
| [setup-fastlane](./setup-fastlane/SKILL.md) | Fastlane 配置 |
| [mobile-device-testing](./mobile-device-testing/SKILL.md) | BrowserStack / Firebase Test Lab / Appium |
| [secrets-management](./secrets-management/SKILL.md) | Vault / AWS Secrets / 凭据轮转 |
| [feature-flags](./feature-flags/SKILL.md) | Feature Flag / 灰度 / Kill Switch |
| [cdn-setup](./cdn-setup/SKILL.md) | CloudFront / Cloudflare / Fastly |
| [steam-deploy](./steam-deploy/SKILL.md) | Steamworks 发版 / steampipe / depot |
| [asc-submission-health](./asc-submission-health/SKILL.md) | App Store 预检 / 提交 / 审核监控 |
| [deploy-checklist](./deploy-checklist/SKILL.md) | 发布前验证 / 迁移 / 回滚触发 |
| [opentelemetry](./opentelemetry/SKILL.md) | OTel + Grafana 接入 |
| [prometheus](./prometheus/SKILL.md) | PromQL / 记录规则 / 告警 |
| [crash-analytics](./crash-analytics/SKILL.md) | Sentry / Crashlytics / Symbol / ANR |
| [k6](./k6/SKILL.md) | k6 负载测试 |
| [testing-strategies](./testing-strategies/SKILL.md) | 测试金字塔 / TDD |

### 2.10 文档 / Web 自动化 / 工具

| SKILL | 用途 |
|---|---|
| [agent-browser](./agent-browser/SKILL.md) | 浏览器自动化 CLI（导航/表单/截图/抓取） |
| [moai-docs-generation](./moai-docs-generation/SKILL.md) | Sphinx / MkDocs / TypeDoc / Nextra |
| [xlsx](./xlsx/SKILL.md) | 电子表格创建/编辑/分析/公式 |

---

## 三、快速匹配表

| 用户意图关键词 | 推荐 SKILL |
|---|---|
| 画图、图标、立绘、场景图、提示词、按提示词生成素材、把美术需求出图 | `ai-art` |
| 搜索资料、深度研究、收集信息 | `deep-research` |
| 创建 MCP、创建 Skill、改进 Skill | `dev-tools` 或 `skill-creator` |
| Word、PDF、PPT、Excel、文档转换 | `document-tools`、`xlsx`、`moai-docs-generation` |
| 代码分析、架构、影响范围、Bug追踪、重构 | `codebase-memory` MCP（见 CLAUDE.md §八） |
| 需求管理、变更流程 | `openspec` |
| Unity 综合开发 | `unity-dev` |
| 制定计划 / 拆任务 / 评估风险 / 竞品调研 | `project-management`、`task-estimation`、`risk-assessment`、`competitive-analysis` |
| 平衡数值 / 经济 / 难度 | `balance-check`、`combat-balancer`、`game-monetization`、`difficulty-curve` |
| GDD / 系统设计 / 平衡审稿 | `design-system`、`review-all-gdds`、`game-design-core`、`game-design-theory` |
| 关卡 / 节奏 / 引导 | `level-design`、`player-guidance` |
| UI / HUD / icon | `game-ui-design`、`unity-ui` |
| UI 结构（prefab-layout / RectTransform） | `unity-rect-transform`（art-ui + client-unity 共享） |
| 字体 / 排版 / CJK | `font-pairing-suggester`、`font-selection-cjk`、`font-subsetting`、`typeset`、`pixel-font-rendering` |
| 3D 建模 / 贴图 / 拓扑 | `3d-modeling`、`texture-art`、`blender-mcp` |
| 动画 / Rigging / 状态机 | `animation-systems`、`rigging`、`state-machine` |
| Shader / VFX / 后处理 | `unity-shaders-rendering`、`shader-effects`、`vfx-realtime`、`agency-unity-shader-graph-artist` |
| Unity Editor 扩展 / uloop | `unity-editor-scripting`、`uloop-execute-dynamic-code`、`uloop-run-tests` |
| 多人 / netcode / 匹配 | `multiplayer-game`、`game-networking`、`atomic-matchmaking`、`algo-rank-trueskill` |
| API / Auth / 后端 | `arch-api`、`jwt-auth`、`oauth-implementation`、`backend-testing` |
| 数据库 / Redis / Kafka | `database-schema-design`、`redis-specialist`、`kafka-development` |
| CI/CD / 发版 / 商店 | `devops-deployment`、`mobile-cicd`、`steam-deploy`、`asc-submission-health` |
| 监控 / 崩溃 / 性能压测 | `opentelemetry`、`prometheus`、`crash-analytics`、`k6` |
| 浏览器自动化 / E2E | `agent-browser`、`playwright`（MCP） |

---

## 四、候选淘汰区（11-skill-governance 标注，下次清理周期前正式删）

以下 SKILL 与本项目无关或可被替代，**先不删**（避免误伤），等下次清理周期评估后决断。

| SKILL | 标注原因 | 当前白名单引用 |
|---|---|---|
| agency-unity-shader-graph-artist | agency 系列模板，可被 `unity-shaders-rendering` + `shader-effects` 替代 | `client-ta` 仍引用，淘汰前需先剔除 |
| agency-technical-artist | agency 系列模板，可被 `client-ta` 自身 system prompt 替代 | 无白名单引用，可优先淘汰 |

> 淘汰流程：（1）确认无 agent 白名单引用 →（2）从 `frontmatter.skills` 移除 →（3）删 SKILL 目录 →（4）更新本索引计数

---

*最后更新：2026-06-26（11-skill-governance：8 极短 description 补强 + 5 极长 description 压缩 + 6 组重叠 SKILL 加 ❌ 不适用 划界 + 候选淘汰区登记）*

*历史：2026-06-25 起多次清理跨引擎、专属风格与历史项目残留 SKILL；实际数量以当前目录和审计脚本为准。*
