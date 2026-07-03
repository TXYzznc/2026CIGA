# 项目上下文

本项目是一个轻量化后的 Unity AI 协作模板。

## 当前状态

- Unity 版本：Unity 6.3 LTS。
- 已移除 Locus：根目录 `Locus/` 与 `Packages/com.farlocus.locus/` 不再作为模板组成部分。
- 已移除程序框架：`Assets/Scripts` 中原有的 `Core`、`Modules`、`DataTable`、`Events`、`ExternalAPI`、`Templates`、`Utils` 已清理。
- 已清理缓存：Unity 临时目录、日志目录、Python `__pycache__`、工具构建产物等已移除。
- 模板保留重点：AI 协作配置、agent/skill 路由、OpenSpec、Unity 基础工程设置。

## 不再内置的内容

模板不再预设以下程序层方案：

- `IGameModule` / `ModuleRunner` 生命周期框架
- `EventBus` 事件系统
- `InputModule` 输入约束
- DataTable JSON 到 C# 的生成流程
- ResourceModule / SceneModule / FlowModule 等框架级模块
- 运行时代码模板与示例代码

具体项目可以按自身需要重新选择架构，例如 MonoBehaviour 直写、ECS、第三方框架、自研模块系统或服务端驱动结构。

## 建议阅读顺序

1. [README.md](./README.md)
2. [AGENTS.md](./AGENTS.md)
3. [.claude/CLAUDE.md](./.claude/CLAUDE.md)
4. [.claude/SKILL_MATRIX.md](./.claude/SKILL_MATRIX.md)
5. [.claude/skills/SKILLS_INDEX.md](./.claude/skills/SKILLS_INDEX.md)

## 给 AI 的注意事项

- 始终用中文回答。
- 需要改 Unity 代码时，先观察当前项目实际存在的代码结构；如果没有 `Assets/Scripts`，不要假设模板已有程序框架。
- 不要要求所有输入必须走某个预置 `InputModule`。
- 不要要求所有配置必须走 DataTable 生成器。
- 不要把业务示例或框架示例重新混入模板核心。
