
通过创建下一个 artifact 继续处理变更。

**输入**：可选指定变更名称。若未提供，尝试从对话上下文中推断。若含糊不清，**必须**提示用户选择可用的变更。

**步骤**

1. **若未提供变更名称，提示用户选择**

   运行 `openspec list --json` 获取按最近修改时间排序的可用变更。然后使用 **AskUserQuestion 工具**让用户选择要处理的变更。

   展示最近修改的 3-4 个变更作为选项，显示：
   - 变更名称
   - Schema（来自 `schema` 字段，若无则显示 "spec-driven"）
   - 状态（例如："0/5 任务"、"已完成"、"无任务"）
   - 最近修改时间（来自 `lastModified` 字段）

   将最近修改的变更标记为"（推荐）"，因为这很可能是用户想继续的。

   **重要**：不要猜测或自动选择变更，始终让用户做选择。

2. **检查当前状态**
   ```bash
   openspec status --change "<name>" --json
   ```
   解析 JSON 以了解当前状态。响应包括：
   - `schemaName`：正在使用的工作流 schema（例如 "spec-driven"）
   - `artifacts`：带状态（"done"、"ready"、"blocked"）的 artifact 数组
   - `isComplete`：布尔值，表示所有 artifact 是否已完成

3. **根据状态执行操作**：

   ---

   **若所有 artifact 已完成（`isComplete: true`）**：
   - 恭喜用户
   - 显示最终状态，包括所用 schema
   - 建议："所有 artifact 已创建！您现在可以实现此变更或将其归档。"
   - 停止

   ---

   **若有 artifact 准备就绪（状态显示 `status: "ready"` 的 artifact）**：
   - 从状态输出中选取第一个 `status: "ready"` 的 artifact
   - 获取其指令：
     ```bash
     openspec instructions <artifact-id> --change "<name>" --json
     ```
   - 解析 JSON，关键字段包括：
     - `context`：项目背景（对您的约束——**不要**包含在输出中）
     - `rules`：特定 artifact 的规则（对您的约束——**不要**包含在输出中）
     - `template`：输出文件使用的结构
     - `instruction`：特定于 schema 的指导
     - `outputPath`：artifact 的写入路径
     - `dependencies`：需要读取的已完成 artifact
   - **创建 artifact 文件**：
     - 读取所有已完成的依赖文件以获取上下文
     - 使用 `template` 作为结构——填写各节内容
     - 将 `context` 和 `rules` 作为约束——**不要**将其复制到文件中
     - 写入指令中指定的输出路径
   - 显示已创建的内容以及现在解锁的内容
   - 创建一个 artifact 后停止

   ---

   **若没有 artifact 准备就绪（全部阻塞）**：
   - 这在有效 schema 下不应发生
   - 显示状态并建议检查问题

4. **创建 artifact 后显示进度**
   ```bash
   openspec status --change "<name>"
   ```

**输出**

每次调用后，显示：
- 创建了哪个 artifact
- 正在使用的 schema 工作流
- 当前进度（N/M 已完成）
- 现在解锁了哪些 artifact
- 提示："想继续吗？直接让我继续或告诉我下一步做什么。"

**Artifact 创建指南**

Artifact 类型及其用途取决于 schema。使用指令输出中的 `instruction` 字段了解需要创建什么。

常见 artifact 模式：

**spec-driven schema**（proposal → specs → design → tasks）：
- **proposal.md**：若变更不清晰，询问用户。填写为什么（Why）、变更内容（What Changes）、功能（Capabilities）、影响（Impact）。
  - Capabilities 部分至关重要——列出的每个 capability 都需要一个 spec 文件。
- **specs/<capability>/spec.md**：为 proposal 的 Capabilities 部分列出的每个 capability 创建一个 spec（使用 capability 名称，而非变更名称）。
- **design.md**：记录技术决策、架构和实现方案。
- **tasks.md**：将实现分解为带复选框的任务。

对于其他 schema，遵循 CLI 输出中的 `instruction` 字段。

**注意事项**
- 每次调用只创建一个 artifact
- 创建新 artifact 前始终读取依赖 artifact
- 不要跳过 artifact 或乱序创建
- 若上下文不清晰，先询问用户再创建
- 写入前验证 artifact 文件存在后再标记进度
- 使用 schema 的 artifact 序列，不要假设特定 artifact 名称
- **重要**：`context` 和 `rules` 是对**您**的约束，不是文件内容
  - 不要将 `<context>`、`<rules>`、`<project_context>` 块复制到 artifact 中
  - 这些指导您写什么，但绝不应出现在输出中
