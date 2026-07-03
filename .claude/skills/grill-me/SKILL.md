---
name: grill-me
description: 持续追问用户关于计划或设计的相关问题，直到双方达成共识，并理清决策树的每个分支。所有提问以 AskUserQuestion 弹窗形式呈现，给出 2-4 个互斥选项并推荐其中一项。当用户想要对计划进行压力测试、针对自己的设计接受严格盘问，或者提到"拷问我"时，即可使用此方法。
tags: plan-validation, design-review, decision-tree-analysis, requirement-clarification,
  codebase-exploration
tags_cn: 计划验证, 设计审查, 决策树分析, 需求澄清, 代码库探索
---

针对该计划的每个方面持续追问我，直到我们达成共识。逐一梳理设计树的每个分支，逐个解决决策之间的依赖关系。

## 交互方式（强制 — 这是本 SKILL 的核心）

**每轮反问必须通过 `AskUserQuestion` 工具弹窗呈现，禁止用纯文本表格或 markdown 列表让用户打字回答。**

每次调用规则：
- **1 个问题最常用**；最多同时抛 2 个互锁问题；上限 4 个
- 每个问题 **2–4 个选项**，必须 mutually exclusive
- **推荐项放第一个**，label 末尾加 `(Recommended)`
- 选项 `label` 1–5 字（如 `Roguelike 构筑`、`单机 PC`）
- `description` 解释这个选项带来的下游后果与取舍
- `header` 用 ≤12 字短 chip（如 `游戏类型`、`平台`、`开发周期`）
- **关键决策（架构选型 / UI 布局 / 数值对比 / 代码片段）用 `preview` 字段**渲染 ASCII 对比表或代码块；side-by-side 时让用户能直接对比
- 不要自己加 "Other"——平台会自动追加，用户随时可以自由文本输入
- 互斥但允许多选时（如"哪些模块本期实现"）设 `multiSelect: true`

## 探索代码

如果某个问题可以通过探索代码库回答（如"是否已存在 XxxModule"、"当前 DataTable 有哪些字段"），**先探索再提问**——不要把已经能确定的事抛回给用户。

## 收敛标准（决策门槛 §五 5 条）

每轮弹窗后用 **≤1 行**复述：`已挖透 X / 5：[目标✓ | A/B✓ | 边界 | 验收 | 约束]`。

5 条全 ✓ 才能退出 grill。任何一条没挖透都不能给方案。

## 反模式

| ❌ 错 | ✅ 对 |
|---|---|
| 一次抛 5 个问题铺开 | 1–2 个互锁的关键问题 |
| 用 markdown 表格让用户打字 | `AskUserQuestion` 选项 + preview |
| 选项没有推荐 | 第一项标 `(Recommended)` |
| 让用户脑补 A/B 后果 | `description` 写明下游影响 |
| 把"Other"作为选项加进去 | 留给平台自动处理 |
