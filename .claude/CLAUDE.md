# AI 友好型项目 — Claude Code 指南

本项目是 Unity 6.3 LTS 的 AI 协作模板，不再内置或约束运行时代码框架。

## 基本准则

- 始终用中文回答。
- 优先简单方案，不做过度工程。
- 改代码前先查询当前项目真实结构，不臆猜接口或目录。
- 不要假设项目存在 `Assets/Scripts/Core`、`InputModule`、`EventBus`、`DataTableGenerator` 或任何预置模块系统。
- 具体项目的程序架构由项目自行决定，可使用 MonoBehaviour、ECS、第三方框架、自研框架或其他方案。

## Agent 路由

主对话作为 orchestrator。轻量任务可直接处理；专业任务按下表路由。

| 任务 | Agent |
|---|---|
| 项目计划 / PRD / 排期 / 风险 / 竞品 | `producer` |
| 核心玩法 vision / GDD / MDA / 留存哲学 | `gd-lead` |
| 公式 / 数值 / loot / 状态机 / 任务规格 | `gd-system` |
| 关卡布局 / 节奏 / encounter / puzzle / 引导 | `level-designer` |
| 美术风格统筹 / art bible / 风格审稿 | `art-director` |
| HUD / 菜单 / icon 设计 | `art-ui` |
| 字体选型 / 排版 / CJK | `art-font` |
| 特效设计 / 粒子配方 | `art-vfx` |
| 立绘 / sprite / 像素美术 | `art-2d` |
| 3D 建模 / UV / 贴图 / Blender | `art-3d` |
| 动画 / 骨骼 / Mecanim / Timeline | `art-anim` |
| 客户端架构 / 设计模式 / 性能预算 | `client-lead` |
| Unity C# 实现 / UI 接入 / 存档 / 输入 | `client-unity` |
| Shader / URP/HDRP / 后处理 / TA 工具 | `client-ta` |
| 服务端架构 / 协议 / 匹配 / 反作弊 | `net-lead` |
| API / JWT / Redis / 消息队列实现 | `net-backend` |
| DB schema / 索引 / 迁移 / 查询优化 | `net-db` |
| 测试策略 / UTF / bug / crash / playtest | `qa-engineer` |
| CI/CD / Unity 构建 / 发版 / 签名 | `devops-engineer` |
| Editor 扩展 / 内部工具 / 新建 skill | `tools-engineer` |

## 决策门槛

检测到 `设计 / 架构 / 重构 / 大改 / 重写 / GDD / PRD / 系统 / 范式 / 方案 / 思路` 时：

1. 先按 `grill-me` / `grill-with-docs` 的问题框架澄清目标、关键决策、边界、验收标准、约束。
2. 再评估是否需要 OpenSpec change。
3. 只有共识冲突、不可逆变更、或触及 `.claude/` / `openspec/` 等项目协作契约时才中断用户。

## Skill 系统

- `.claude/skills/<skill>/SKILL.md` 是 skill 入口。
- `.claude/SKILL_MATRIX.md` 维护 agent 与 skill 白名单。
- `/graphify` 映射到 `graphify-windows` skill。

## Unity 代码原则

- 先看当前项目是否已有代码、asmdef、输入方案、资源加载方案、测试方案。
- 没有现成约定时，采用最小可用实现。
- 不在 `Update` 中制造 GC alloc。
- ScriptableObject 是配置资产，不当作运行时数据库。
- 输入、配置表、资源加载、事件系统都按具体项目需求选择，不套用模板默认框架。

## 不要

- 不要直接改 `.codex/agents/*.toml`，改 `.claude/agents/*.md` 后运行 `python Tools/sync-agents.py`。
- 不要把业务示例或框架示例重新混入模板核心。
- 不要把缓存、构建产物或本机依赖提交进模板。
