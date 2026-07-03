---
name: grill-with-docs
description: 一场针对现有领域模型挑战你的计划、打磨术语，并在决策明确时同步更新文档（CONTEXT.md、ADRs）的审查会议。所有提问以 AskUserQuestion 弹窗形式呈现，每轮只问一个问题并给出推荐答案。当用户想要针对其项目的语言规范和已记录的决策对计划进行压力测试时使用。
disable-model-invocation: true
tags: domain-model, documentation-update, adr-management, plan-review, terminology-sharpening
tags_cn: 领域模型, 文档更新, ADR管理, 计划审查, 术语打磨
---

针对该计划的各个方面对我进行全面提问，直到我们达成共识。逐一梳理设计树的每个分支，逐个解决决策之间的依赖关系。**对于每个问题，请给出你的推荐答案。**

如果某个问题可以通过探索代码库找到答案，请直接探索代码库。

## 交互方式（强制）

**每轮提问必须通过 `AskUserQuestion` 工具弹窗呈现，禁止用纯文本让用户打字回答。**

每次调用规则：
- **严格每次 1 个问题**（不像 grill-me 可以 1-2 个并列）
- 2–4 个 mutually exclusive 选项
- **推荐项放第一个**，label 末尾加 `(Recommended)`，并在 `description` 里说"因 CONTEXT.md / ADR-XXXX 已有 Y，推荐 A"
- `header` ≤12 字短 chip（如 `Order 状态`、`聚合根`）
- 涉及术语对比 / 状态机 / 数据流时用 `preview` 字段渲染对比 ASCII
- 不要自己加 "Other"——平台自动追加

## 领域感知

在探索代码库时，同时查找现有文档：

### 文件结构

大多数代码库只有一个上下文：

```
/
├── CONTEXT.md
├── docs/
│   └── adr/
│       ├── 0001-event-sourced-orders.md
│       └── 0002-postgres-for-write-model.md
└── src/
```

如果根目录下存在 `CONTEXT-MAP.md`，则该代码库包含多个上下文。该文件会指向每个上下文的位置：

```
/
├── CONTEXT-MAP.md
├── docs/
│   └── adr/                          ← 系统级决策
├── src/
│   ├── ordering/
│   │   ├── CONTEXT.md
│   │   └── docs/adr/                 ← 上下文专属决策
│   └── billing/
│       ├── CONTEXT.md
│       └── docs/adr/
```

延迟创建文件——仅当有内容可写时再创建。如果不存在 `CONTEXT.md`，则在第一个术语明确后创建它。如果不存在 `docs/adr/` 目录，则在需要创建第一个 ADR 时创建该目录。

## 会议期间

### 对照术语表提出质疑

当用户使用的术语与 `CONTEXT.md` 中的现有术语冲突时，立即弹窗指出：选项 A 是「沿用 CONTEXT.md 的定义 X」、选项 B 是「改 CONTEXT.md 为 Y」，让用户在弹窗里二选一，不要让他自由打字。

### 明确模糊表述

当用户使用模糊或多义术语时（如 "account"），用弹窗给标准术语选项：
- A. Customer（领域里有独立生命周期的客户）
- B. User（登录态的用户）
- C. 二者其一的别名

### 讨论具体场景

讨论领域关系时用具体场景压力测试。把"边缘情况触发后该怎么处理"写成 2–3 个 mutually exclusive 选项给用户选，迫使他明确边界。

### 与代码交叉验证

当用户说明某事物的工作方式时，检查代码是否一致。如果发现矛盾，弹窗呈现：
- A. 代码当前的行为（贴 `preview` 代码片段）
- B. 用户口头描述的行为
- C. 第三种新写法

让用户选一项作为唯一答案。

### 同步更新 CONTEXT.md

当术语明确后，**立即**更新 `CONTEXT.md`。不要批量处理——在决策确定时就记录下来。使用 [CONTEXT-FORMAT.md](./CONTEXT-FORMAT.md) 中的格式。

不要将 `CONTEXT.md` 与实现细节绑定。仅包含对领域专家有意义的术语。

### 谨慎创建 ADR

仅当**同时满足以下三个条件**时，才创建 ADR：

1. **难以逆转**——后续更改决策的成本很高
2. **缺乏上下文会令人困惑**——未来的读者会疑惑"他们为什么要这么做？"
3. **是实际权衡的结果**——存在真正的替代方案，且你因特定原因选择了其中一个

如果缺少任意一个条件，就不要创建 ADR。使用 [ADR-FORMAT.md](./ADR-FORMAT.md) 中的格式。

## 反模式

| ❌ 错 | ✅ 对 |
|---|---|
| 一次抛多个问题 | 严格每轮 1 个问题 |
| markdown 表格 + "请回复 A/B/C" | `AskUserQuestion` 选项 |
| 不给推荐答案 | 第一项 `(Recommended)` 并说明依据 |
| 术语冲突时让用户自己想 | 把冲突的两种定义写进选项 |
| 决策定了不更新 CONTEXT.md | 立即写入文档 |
