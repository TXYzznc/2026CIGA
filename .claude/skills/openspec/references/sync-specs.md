
将变更中的增量 spec 同步到主 spec。

这是一个**代理驱动**的操作——您将读取增量 spec 并直接编辑主 spec 以应用更改。这允许智能合并（例如，添加场景而不复制整个需求）。

**输入**：可选指定变更名称。若未提供，尝试从对话上下文中推断。若含糊不清，**必须**提示用户选择可用的变更。

**步骤**

1. **若未提供变更名称，提示用户选择**

   运行 `openspec list --json` 获取可用变更。使用 **AskUserQuestion 工具**让用户选择。

   显示有增量 spec 的变更（在 `specs/` 目录下）。

   **重要**：不要猜测或自动选择变更，始终让用户做选择。

2. **查找增量 spec**

   在 `openspec/changes/<name>/specs/*/spec.md` 中查找增量 spec 文件。

   每个增量 spec 文件包含如下节：
   - `## ADDED Requirements` - 要添加的新需求
   - `## MODIFIED Requirements` - 对现有需求的更改
   - `## REMOVED Requirements` - 要删除的需求
   - `## RENAMED Requirements` - 要重命名的需求（FROM:/TO: 格式）

   若未找到增量 spec，通知用户并停止。

3. **对每个增量 spec，将更改应用到主 spec**

   对每个在 `openspec/changes/<name>/specs/<capability>/spec.md` 有增量 spec 的 capability：

   a. **读取增量 spec** 以了解预期的更改

   b. **读取主 spec**（位于 `openspec/specs/<capability>/spec.md`，可能尚不存在）

   c. **智能应用更改**：

      **ADDED 需求：**
      - 若主 spec 中不存在该需求 → 添加它
      - 若需求已存在 → 更新以匹配（视为隐式 MODIFIED）

      **MODIFIED 需求：**
      - 在主 spec 中找到该需求
      - 应用更改——可以是：
        - 添加新场景（无需复制现有场景）
        - 修改现有场景
        - 更改需求描述
      - 保留增量中未提及的场景/内容

      **REMOVED 需求：**
      - 从主 spec 中删除整个需求块

      **RENAMED 需求：**
      - 找到 FROM 需求，重命名为 TO

   d. **若 capability 尚不存在，创建新主 spec**：
      - 创建 `openspec/specs/<capability>/spec.md`
      - 添加 Purpose 节（可简短，标记为 TBD）
      - 添加包含 ADDED 需求的 Requirements 节

4. **显示摘要**

   应用所有更改后，总结：
   - 哪些 capability 已更新
   - 做了哪些更改（需求的添加/修改/删除/重命名）

**增量 Spec 格式参考**

```markdown
## ADDED Requirements

### Requirement: 新功能
系统应执行某些新操作。

#### Scenario: 基本情况
- **WHEN** 用户执行 X
- **THEN** 系统执行 Y

## MODIFIED Requirements

### Requirement: 现有功能
#### Scenario: 要添加的新场景
- **WHEN** 用户执行 A
- **THEN** 系统执行 B

## REMOVED Requirements

### Requirement: 已废弃功能

## RENAMED Requirements

- FROM: `### Requirement: 旧名称`
- TO: `### Requirement: 新名称`
```

**核心原则：智能合并**

与程序化合并不同，您可以应用**部分更新**：
- 要添加场景，只需在 MODIFIED 下包含该场景——无需复制现有场景
- 增量代表*意图*，不是全量替换
- 凭判断力合理合并更改

**成功时的输出**

```
## Spec 已同步：<change-name>

已更新主 spec：

**<capability-1>**：
- 已添加需求："新功能"
- 已修改需求："现有功能"（添加了 1 个场景）

**<capability-2>**：
- 已创建新 spec 文件
- 已添加需求："另一功能"

主 spec 已更新。变更保持活跃——实现完成后归档。
```

**注意事项**
- 进行更改前读取增量 spec 和主 spec
- 保留增量中未提及的现有内容
- 若有不清楚的地方，请求澄清
- 在更改时显示您正在做什么
- 操作应是幂等的——运行两次应得到相同结果
