
在单次操作中批量归档多个已完成的变更。

此 skill 允许批量归档变更，通过检查代码库确定实际实现情况来智能处理 spec 冲突。

**输入**：无需输入（提示用户选择）

**步骤**

1. **获取活跃变更**

   运行 `openspec list --json` 获取所有活跃变更。

   若不存在活跃变更，通知用户并停止。

2. **提示用户选择变更**

   使用 **AskUserQuestion 工具**进行多选，让用户选择变更：
   - 显示每个变更及其 schema
   - 包含"所有变更"选项
   - 允许任意数量的选择（1 个以上均可，2 个以上是典型用例）

   **重要**：不要自动选择，始终让用户做选择。

3. **批量验证——收集所有选中变更的状态**

   对每个选中的变更，收集：

   a. **Artifact 状态** - 运行 `openspec status --change "<name>" --json`
      - 解析 `schemaName` 和 `artifacts` 列表
      - 记录哪些 artifact 是 `done` 或其他状态

   b. **任务完成情况** - 读取 `openspec/changes/<name>/tasks.md`
      - 统计 `- [ ]`（未完成）与 `- [x]`（已完成）
      - 若不存在任务文件，记录为"无任务"

   c. **增量 spec** - 检查 `openspec/changes/<name>/specs/` 目录
      - 列出存在的 capability spec
      - 对每个 spec，提取需求名称（匹配 `### Requirement: <name>` 的行）

4. **检测 spec 冲突**

   构建 `capability -> [涉及该 capability 的变更]` 的映射：

   ```
   auth -> [change-a, change-b]  <- 冲突（2 个以上变更）
   api  -> [change-c]            <- 正常（只有 1 个变更）
   ```

   当 2 个以上选中的变更对同一 capability 有增量 spec 时，即存在冲突。

5. **自主解决冲突**

   **对每个冲突**，调查代码库：

   a. **读取每个冲突变更的增量 spec**，了解各自声称要添加/修改的内容

   b. **搜索代码库**寻找实现证据：
      - 查找实现每个增量 spec 需求的代码
      - 检查相关文件、函数或测试

   c. **确定解决方案**：
      - 若只有一个变更已实现 → 仅同步该变更的 spec
      - 若两者均已实现 → 按时间顺序应用（较旧的先，较新的覆盖）
      - 若均未实现 → 跳过 spec 同步，警告用户

   d. **记录每个冲突的解决方案**：
      - 应用哪个变更的 spec
      - 应用顺序（若两者均需应用）
      - 依据（代码库中发现了什么）

6. **显示综合状态表**

   显示汇总所有变更的表格：

   ```
   | 变更                  | Artifact | 任务  | Spec     | 冲突     | 状态   |
   |----------------------|----------|-------|----------|----------|--------|
   | schema-management    | 完成     | 5/5   | 2 个增量 | 无       | 就绪   |
   | project-config       | 完成     | 3/3   | 1 个增量 | 无       | 就绪   |
   | add-oauth            | 完成     | 4/4   | 1 个增量 | auth (!) | 就绪*  |
   | add-verify-skill     | 剩余 1   | 2/5   | 无       | 无       | 警告   |
   ```

   对冲突显示解决方案：
   ```
   * 冲突解决方案：
     - auth spec：将先应用 add-oauth，再应用 add-jwt（两者均已实现，按时间顺序）
   ```

   对未完成的变更显示警告：
   ```
   警告：
   - add-verify-skill：1 个未完成 artifact，3 个未完成任务
   ```

7. **确认批量操作**

   使用 **AskUserQuestion 工具**进行单次确认：

   - "归档 N 个变更？"，根据状态提供选项
   - 选项可能包括：
     - "归档所有 N 个变更"
     - "仅归档 N 个就绪变更（跳过未完成的）"
     - "取消"

   若存在未完成的变更，明确说明它们将带有警告被归档。

8. **为每个确认的变更执行归档**

   按确定的顺序处理变更（遵循冲突解决方案）：

   a. **若存在增量 spec，同步 spec**：
      - 使用 openspec-sync-specs 方式（代理驱动的智能合并）
      - 对冲突，按解决方案的顺序应用
      - 跟踪是否已完成同步

   b. **执行归档**：
      ```bash
      mkdir -p openspec/changes/archive
      mv openspec/changes/<name> openspec/changes/archive/YYYY-MM-DD-<name>
      ```

   c. **跟踪每个变更的结果**：
      - 成功：归档成功
      - 失败：归档时出错（记录错误）
      - 跳过：用户选择不归档（如适用）

9. **显示摘要**

   显示最终结果：

   ```
   ## 批量归档完成

   已归档 3 个变更：
   - schema-management-cli -> archive/2026-01-19-schema-management-cli/
   - project-config -> archive/2026-01-19-project-config/
   - add-oauth -> archive/2026-01-19-add-oauth/

   已跳过 1 个变更：
   - add-verify-skill（用户选择不归档未完成的变更）

   Spec 同步摘要：
   - 已同步 4 个增量 spec 到主 spec
   - 已解决 1 个冲突（auth：按时间顺序同时应用）
   ```

   若有失败：
   ```
   失败 1 个变更：
   - some-change：归档目录已存在
   ```

**冲突解决示例**

示例 1：只有一个已实现
```
冲突：specs/auth/spec.md 被 [add-oauth, add-jwt] 涉及

检查 add-oauth：
- 增量添加"OAuth 提供商集成"需求
- 搜索代码库... 发现 src/auth/oauth.ts 实现了 OAuth 流程

检查 add-jwt：
- 增量添加"JWT Token 处理"需求
- 搜索代码库... 未发现 JWT 实现

解决方案：只有 add-oauth 已实现。仅同步 add-oauth 的 spec。
```

示例 2：两者均已实现
```
冲突：specs/api/spec.md 被 [add-rest-api, add-graphql] 涉及

检查 add-rest-api（创建于 2026-01-10）：
- 增量添加"REST 端点"需求
- 搜索代码库... 发现 src/api/rest.ts

检查 add-graphql（创建于 2026-01-15）：
- 增量添加"GraphQL Schema"需求
- 搜索代码库... 发现 src/api/graphql.ts

解决方案：两者均已实现。先应用 add-rest-api 的 spec，
再应用 add-graphql 的 spec（按时间顺序，较新的优先）。
```

**成功时的输出**

```
## 批量归档完成

已归档 N 个变更：
- <change-1> -> archive/YYYY-MM-DD-<change-1>/
- <change-2> -> archive/YYYY-MM-DD-<change-2>/

Spec 同步摘要：
- 已同步 N 个增量 spec 到主 spec
- 无冲突（或：已解决 M 个冲突）
```

**部分成功时的输出**

```
## 批量归档完成（部分）

已归档 N 个变更：
- <change-1> -> archive/YYYY-MM-DD-<change-1>/

已跳过 M 个变更：
- <change-2>（用户选择不归档未完成的变更）

失败 K 个变更：
- <change-3>：归档目录已存在
```

**无变更时的输出**

```
## 无变更可归档

未发现活跃变更。创建新变更以开始。
```

**注意事项**
- 允许任意数量的变更（1 个以上均可，2 个以上是典型用例）
- 始终提示用户选择，不要自动选择
- 尽早检测 spec 冲突，通过检查代码库解决
- 当两个变更均已实现时，按时间顺序应用 spec
- 仅在实现缺失时跳过 spec 同步（警告用户）
- 确认前显示每个变更的清晰状态
- 整批使用单次确认
- 跟踪并报告所有结果（成功/跳过/失败）
- 移动到归档时保留 .openspec.yaml
- 归档目标使用当前日期：YYYY-MM-DD-<name>
- 若归档目标已存在，该变更失败但继续处理其他变更
