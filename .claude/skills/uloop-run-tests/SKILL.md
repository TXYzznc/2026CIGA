---
name: uloop-run-tests
description: 执行Unity Test Runner并获取详细测试结果。适用场景：(1) 运行单元测试（EditMode/PlayMode），(2) 验证代码变更，(3)
  诊断测试失败——当测试失败时，会自动保存包含错误信息和堆栈跟踪的NUnit XML文件。
tags: unity-test-runner, test-automation, nunit-xml, test-filtering, uloop-cli
tags_cn: Unity Test Runner, 测试自动化, NUnit XML结果, 测试筛选, Uloop CLI工具
---

# uloop run-tests

执行Unity Test Runner。当测试失败时，会自动保存包含错误信息和堆栈跟踪的NUnit XML结果文件。可读取`XmlPath`路径下的XML文件以进行详细的失败诊断。

## 使用方法

```bash
uloop run-tests [options]
```

## 参数

| 参数 | 类型 | 默认值 | 描述 |
|-----------|------|---------|-------------|
| `--test-mode` | 字符串 | `EditMode` | 测试模式：`EditMode`、`PlayMode` |
| `--filter-type` | 字符串 | `all` | 筛选类型：`all`、`exact`、`regex`、`assembly` |
| `--filter-value` | 字符串 | - | 筛选值（测试名称、正则表达式或程序集） |

## 示例

```bash
# 运行所有EditMode测试
uloop run-tests

# 运行PlayMode测试
uloop run-tests --test-mode PlayMode

# 运行指定测试
uloop run-tests --filter-type exact --filter-value "MyTest.TestMethod"

# 运行匹配指定模式的测试
uloop run-tests --filter-type regex --filter-value ".*Integration.*"
```

## 输出结果

返回包含以下字段的JSON：
- `Success`（布尔值）：是否所有测试均通过
- `Message`（字符串）：摘要信息
- `TestCount`（数字）：执行的测试总数
- `PassedCount`（数字）：通过的测试数量
- `FailedCount`（数字）：失败的测试数量
- `SkippedCount`（数字）：跳过的测试数量
- `XmlPath`（字符串）：NUnit XML结果文件的路径（测试失败时自动保存）

### XML结果文件

当测试失败时，NUnit XML结果会自动保存到`{project_root}/.uloop/outputs/TestResults/<timestamp>.xml`。该XML文件包含每个测试用例的结果，包括：
- 测试名称和完整名称
- 测试状态（通过/失败/跳过）和耗时
- 对于失败的测试：`<message>`（断言错误）和`<stack-trace>`（堆栈跟踪）