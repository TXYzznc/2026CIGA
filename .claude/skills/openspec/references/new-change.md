
使用实验性 artifact 驱动方式启动新变更。

**输入**：用户的请求应包含变更名称（kebab-case）或对他们想要构建内容的描述。

**步骤**

1. **若未提供明确输入，询问要构建什么**

   使用 **AskUserQuestion 工具**（开放式，无预设选项）提问：
   > "您想处理什么变更？描述您想构建或修复的内容。"

   从他们的描述中推导出 kebab-case 名称（例如"添加用户认证" → `add-user-auth`）。

   **重要**：在不了解用户想构建什么的情况下，不要继续。

2. **确定工作流 schema**

   使用默认 schema（省略 `--schema`），除非用户明确要求不同的工作流。

   **仅在以下情况使用不同 schema：**
   - 用户提到特定 schema 名称 → 使用 `--schema <name>`
   - "显示工作流"或"有哪些工作流" → 运行 `openspec schemas --json` 并让用户选择

   **否则**：省略 `--schema` 以使用默认值。

3. **创建变更目录**
   ```bash
   openspec new change "<name>"
   ```
   仅在用户请求特定工作流时添加 `--schema <name>`。
   这会在 `openspec/changes/<name>/` 创建带有所选 schema 的脚手架变更。

4. **显示 artifact 状态**
   ```bash
   openspec status --change "<name>"
   ```
   显示需要创建哪些 artifact 以及哪些已准备就绪（依赖已满足）。

5. **获取第一个 artifact 的指令**
   第一个 artifact 取决于 schema（例如 spec-driven 的 `proposal`）。
   检查状态输出，找到第一个状态为 "ready" 的 artifact。
   ```bash
   openspec instructions <first-artifact-id> --change "<name>"
   ```
   输出用于创建第一个 artifact 的模板和上下文。

6. **停止并等待用户指示**

**输出**

完成步骤后，总结：
- 变更名称和位置
- 正在使用的 schema/工作流及其 artifact 序列
- 当前状态（0/N artifact 已完成）
- 第一个 artifact 的模板
- 提示："准备好创建第一个 artifact 了吗？只需描述此变更的内容，我来起草；或者让我继续。"

**注意事项**
- 不要创建任何 artifact——只显示指令
- 不要超出显示第一个 artifact 模板的范围
- 若名称无效（非 kebab-case），请求有效名称
- 若该名称的变更已存在，建议继续该变更
- 若使用非默认工作流，传入 --schema
