
快进完成 artifact 创建——一次性生成开始实现所需的所有内容。

**输入**：用户的请求应包含变更名称（kebab-case）或对他们想要构建内容的描述。

**步骤**

1. **若未提供明确输入，询问要构建什么**

   使用 **AskUserQuestion 工具**（开放式，无预设选项）提问：
   > "您想处理什么变更？描述您想构建或修复的内容。"

   从他们的描述中推导出 kebab-case 名称（例如"添加用户认证" → `add-user-auth`）。

   **重要**：在不了解用户想构建什么的情况下，不要继续。

2. **创建变更目录**
   ```bash
   openspec new change "<name>"
   ```
   这会在 `openspec/changes/<name>/` 创建脚手架变更。

3. **获取 artifact 构建顺序**
   ```bash
   openspec status --change "<name>" --json
   ```
   解析 JSON 以获取：
   - `applyRequires`：实现前所需的 artifact ID 数组（例如 `["tasks"]`）
   - `artifacts`：所有 artifact 的列表，包含状态和依赖关系

4. **按顺序创建 artifact 直到可以应用**

   使用 **TodoWrite 工具**跟踪 artifact 的处理进度。

   按依赖顺序循环处理 artifact（无待处理依赖的 artifact 优先）：

   a. **对每个 `ready` 状态（依赖已满足）的 artifact**：
      - 获取指令：
        ```bash
        openspec instructions <artifact-id> --change "<name>" --json
        ```
      - 指令 JSON 包含：
        - `context`：项目背景（对您的约束——**不要**包含在输出中）
        - `rules`：特定 artifact 的规则（对您的约束——**不要**包含在输出中）
        - `template`：输出文件使用的结构
        - `instruction`：针对此 artifact 类型的 schema 特定指导
        - `outputPath`：artifact 的写入路径
        - `dependencies`：需要读取的已完成 artifact
      - 读取所有已完成的依赖文件以获取上下文
      - 使用 `template` 作为结构创建 artifact 文件
      - 将 `context` 和 `rules` 作为约束——**不要**将其复制到文件中
      - 显示简短进度："✓ 已创建 <artifact-id>"

   b. **继续直到所有 `applyRequires` 中的 artifact 完成**
      - 每次创建 artifact 后重新运行 `openspec status --change "<name>" --json`
      - 检查 `applyRequires` 中的每个 artifact ID 是否在 artifacts 数组中有 `status: "done"`
      - 所有 `applyRequires` artifact 完成后停止

   c. **若 artifact 需要用户输入**（上下文不明确）：
      - 使用 **AskUserQuestion 工具**澄清
      - 然后继续创建

5. **显示最终状态**
   ```bash
   openspec status --change "<name>"
   ```

**输出**

完成所有 artifact 后，总结：
- 变更名称和位置
- 已创建的 artifact 列表及简短描述
- 就绪内容："所有 artifact 已创建！可以开始实现了。"
- 提示："运行 `/opsx:apply` 或让我实现，开始处理任务。"

**Artifact 创建指南**

- 遵循每种 artifact 类型的 `openspec instructions` 中的 `instruction` 字段
- Schema 定义了每个 artifact 应包含什么——遵循它
- 创建新 artifact 前读取依赖 artifact 以获取上下文
- 使用 `template` 作为输出文件的结构——填写各节内容
- **重要**：`context` 和 `rules` 是对**您**的约束，不是文件内容
  - 不要将 `<context>`、`<rules>`、`<project_context>` 块复制到 artifact 中
  - 这些指导您写什么，但绝不应出现在输出中

**注意事项**
- 创建实现所需的**所有** artifact（由 schema 的 `apply.requires` 定义）
- 创建新 artifact 前始终读取依赖 artifact
- 若上下文极不明确，询问用户——但倾向于做出合理决策以保持推进势头
- 若该名称的变更已存在，建议继续该变更
- 写入后验证每个 artifact 文件存在再继续下一个
