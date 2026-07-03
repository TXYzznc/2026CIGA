# AI 友好型项目 — Codex 入口

> Claude Code 入口为 [.claude/CLAUDE.md](./.claude/CLAUDE.md)。`.codex/agents/*.toml` 由 `.claude/agents/*.md` 通过 [Tools/sync-agents.py](./Tools/sync-agents.py) 生成，不要直接改 `.toml`。

Unity 6.3 LTS 的 AI 协作模板。主对话作为 orchestrator，把任务路由到 20 人虚拟开发团队；轻量任务可由主对话直接处理。

## Codex 执行语义

- “delegate 给对应 agent”在 Codex 中解释为：先读取/遵循 `.codex/agents/<agent>.toml` 的职责、边界、skill 白名单与交回规则。
- 如果当前 Codex 会话没有可用 sub-agent，则主对话按对应 agent 的 prompt 与白名单等价执行。
- `.claude/skills/<skill>/SKILL.md` 是项目 skill 唯一入口；触发 skill 时先读取入口文件。
- `/graphify` 映射到 `graphify-windows` skill，用户输入 `/graphify` 时先执行该 skill。

## 决策门槛

检测到 `设计 / 架构 / 重构 / 大改 / 重写 / GDD / PRD / 系统 / 范式 / 方案 / 思路` 时，先按 `grill-me` / `grill-with-docs` 的问题框架澄清目标、关键决策、边界、验收标准、约束，再做任务规模评估。命中 OpenSpec 信号时推进 OpenSpec change。

## 路由规则

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

## 项目环境

- 平台：Unity 6.3 LTS
- OS：Windows 10
- Python：可选 `.venv/`
- MCP：见 [.mcp.json](./.mcp.json) 与 [.codex/config.toml](./.codex/config.toml)

## 程序框架原则

这个模板不约束运行时代码架构。不要假设项目存在 `Assets/Scripts/Core`、`EventBus`、`InputModule`、DataTable 生成器或任何预置模块系统。实现 Unity 代码前先读取当前实际目录结构，按具体项目选择最简单可行方案。

## AI 行为准则

- 始终用中文回答。
- 优先简单方案。
- 以认真查询为荣，以臆猜接口为耻。
- 以主动验证为荣，以跳过验证为耻。
- 不要把业务示例或框架示例混入模板核心。
