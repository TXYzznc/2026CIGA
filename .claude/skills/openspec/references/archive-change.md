
归档实验性工作流中已完成的变更。

**输入**：可选指定变更名称。若未提供，尝试从对话上下文中推断。若含糊不清，**必须**提示用户选择可用的变更。

**步骤**

1. **若未提供变更名称，提示用户选择**

   运行 `openspec list --json` 获取可用变更。使用 **AskUserQuestion 工具**让用户选择。

   只显示活跃变更（未归档的）。
   若可用，显示每个变更所用的 schema。

   **重要**：不要猜测或自动选择变更，始终让用户做选择。

2. **检查 artifact 完成状态**

   运行 `openspec status --change "<name>" --json` 检查 artifact 完成情况。

   解析 JSON 以了解：
   - `schemaName`：正在使用的工作流
   - `artifacts`：带状态（`done` 或其他）的 artifact 列表

   **若有 artifact 未完成（状态不为 `done`）：**
   - 显示列出未完成 artifact 的警告
   - 使用 **AskUserQuestion 工具**确认用户是否要继续
   - 用户确认后继续

3. **检查任务完成状态**

   读取任务文件（通常是 `tasks.md`）检查未完成的任务。

   统计标记为 `- [ ]`（未完成）与 `- [x]`（已完成）的任务数量。

   **若发现未完成任务：**
   - 显示未完成任务数量的警告
   - 使用 **AskUserQuestion 工具**确认用户是否要继续
   - 用户确认后继续

   **若不存在任务文件：** 无需任务相关警告，直接继续。

4. **评估增量 spec 同步状态**

   检查 `openspec/changes/<name>/specs/` 下是否有增量 spec。若不存在，直接继续无需同步提示。

   **若存在增量 spec：**
   - 将每个增量 spec 与 `openspec/specs/<capability>/spec.md` 中对应的主 spec 进行比较
   - 确定将应用的更改（添加、修改、删除、重命名）
   - 在提示前显示综合摘要

   **提示选项：**
   - 若需要更改："立即同步（推荐）"、"不同步直接归档"
   - 若已同步："立即归档"、"重新同步"、"取消"

   若用户选择同步，使用 Task 工具（subagent_type: "general-purpose"，prompt: "使用 Skill 工具为变更 '<name>' 调用 openspec-sync-specs。增量 spec 分析：<包含已分析的增量 spec 摘要>"）。无论用户选择什么，都继续归档。

5. **执行归档**

   若归档目录不存在则创建：
   ```bash
   mkdir -p openspec/changes/archive
   ```

   使用当前日期生成目标名称：`YYYY-MM-DD-<change-name>`

   **检查目标是否已存在：**
   - 若存在：报错，建议重命名现有归档或使用不同日期
   - 若不存在：将变更目录移动到归档

   ```bash
   mv openspec/changes/<name> openspec/changes/archive/YYYY-MM-DD-<name>
   ```

6. **显示摘要**

   显示归档完成摘要，包括：
   - 变更名称
   - 所用 schema
   - 归档位置
   - spec 是否已同步（如适用）
   - 任何警告说明（未完成的 artifact/任务）

**成功时的输出**

```
## 归档完成

**变更：** <change-name>
**Schema：** <schema-name>
**归档位置：** openspec/changes/archive/YYYY-MM-DD-<name>/
**Spec：** ✓ 已同步到主 spec（或 "无增量 spec" 或 "已跳过同步"）

所有 artifact 已完成。所有任务已完成。
```

**注意事项**
- 若未提供变更，始终提示用户选择
- 使用 artifact 图（openspec status --json）进行完成度检查
- 不因警告阻止归档——只需通知并确认
- 移动到归档时保留 .openspec.yaml（随目录一起移动）；`art/`、`tests/`、`brainstorm.md`、`CONTRACT.md` 等子项一并随 change 目录归档
- 清晰展示发生了什么
- 若请求同步，使用 openspec-sync-specs 方式（代理驱动）
- 若存在增量 spec，始终运行同步评估并在提示前显示综合摘要

**陷阱：CLI vs 手动 mv 两条路径**

如果想走 CLI 快捷路径 `openspec archive <name>`（替代步骤 5 的 mv），**必须加 `--yes`**：

```bash
openspec archive <change-name> --yes
```

否则 CLI 在以下任一情况会弹 interactive prompt 中断 Auto Mode / Loop / Goal 流程：
- tasks.md 有任何 `- [ ]` 未完成任务（即使是「archive 本身」这种递归任务）
- proposal.md 缺 `## Why` / `## What Changes` 章节（warning，但仍要确认）
- specs/ 缺 delta（warning，但仍要确认）

走 SKILL 引导的 AskUserQuestion + 手动 mv 路径不会触发 CLI prompt（因为是 Claude 自己控制交互节奏）。Auto Mode 下推荐 `openspec archive --yes`；手动确认场景走 SKILL 引导。
