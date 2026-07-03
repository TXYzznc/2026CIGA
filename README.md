# AI 友好型 Unity 项目模板

Unity 6.3 LTS 项目的 AI 协作模板。这个模板只保留协作入口、agent/skill 工作流、MCP 配置和基础项目结构，不预置或约束运行时代码框架。

## 定位

1. **AI 协作模板**：保留 `.claude/`、`.codex/`、项目 skill、20 个虚拟开发角色与 OpenSpec。
2. **Unity 空项目底座**：保留 Unity 工程必要目录与配置，不附带 `IGameModule`、`EventBus`、`InputModule`、DataTable 生成器等程序框架。

## 目录速览

| 目录 / 文件 | 作用 |
|---|---|
| `.claude/` | Claude Code 工作流源文件、agents、skills 与设置 |
| `.codex/` | Codex 适配配置，agents 由 `.claude/agents` 同步生成 |
| `Assets/` | Unity 资源目录，不再预置 `Assets/Scripts` 程序框架 |
| `Packages/` | Unity Package Manager 配置 |
| `ProjectSettings/` | Unity 项目设置 |
| `Tools/` | 可选辅助工具与同步脚本 |
| `项目知识库（AI自行维护）/` | AI 维护的项目知识库入口 |
| `openspec/` | 需求与规格变更记录 |

## 快速开始

1. 用 Unity 6.3 LTS 打开本目录。
2. 如需启用 MCP 或 Python 工具，参考 [setup.md](./setup.md) 创建 `.venv` 并复制 `.env.example` 为 `.env`。
3. 新项目的程序架构由项目自行决定；需要时再创建 `Assets/Scripts`、asmdef、输入系统、配置表或服务接入。

## 维护准则

- `.claude/agents/*.md` 是 agent 源文件；同步到 Codex 时运行 `python Tools/sync-agents.py`。
- `.codex/agents/*.toml` 是生成物，不直接手改。
- 不提交 Unity/IDE/语言运行时缓存，例如 `Library/`、`Temp/`、`UserSettings/`、`.csproj`、`.sln`、`__pycache__/`、`node_modules/`。
- 模板不规定程序框架。任何运行时架构、模块系统、事件系统、输入封装、配置表方案，都应由具体项目按需求选择。
