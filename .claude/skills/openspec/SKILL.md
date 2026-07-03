---
name: openspec
description: "OpenSpec 结构化工作流管理。当用户使用 OpenSpec 流程来管理功能开发、变更、归档时使用。覆盖：创建新变更、逐步推进 artifact、快进完成、实现任务、验证实现、同步 spec、归档变更、批量归档、探索模式、入门引导、提案变更。触发场景：用户提到 'openspec'、'变更管理'、'artifact 工作流'、'/opsx' 相关命令、或使用结构化方式管理功能开发流程。"
---

# OpenSpec — 结构化变更工作流

## 子技能列表

| 子技能 | 适用场景 | 参考文件 |
|--------|---------|---------|
| **propose** | 一步生成所有 artifact 来提案新变更（快速完整提案） | `references/propose.md` |
| **new-change** | 使用实验性 artifact 工作流逐步创建新变更 | `references/new-change.md` |
| **continue-change** | 通过创建下一个 artifact 继续推进变更 | `references/continue-change.md` |
| **ff-change** | 快进完成所有 artifact 创建（一次性） | `references/ff-change.md` |
| **apply-change** | 实现变更中的任务（开始/继续/逐步完成） | `references/apply-change.md` |
| **verify-change** | 归档前验证实现是否完整、正确且连贯 | `references/verify-change.md` |
| **sync-specs** | 将增量 spec 同步到主 spec（不归档） | `references/sync-specs.md` |
| **archive-change** | 归档已完成的单个变更 | `references/archive-change.md` |
| **bulk-archive-change** | 一次性归档多个已完成的变更 | `references/bulk-archive-change.md` |
| **explore** | 探索模式：思维伙伴，探索想法、调查问题、澄清需求 | `references/explore.md` |
| **onboard** | 引导式入门：完整走一遍工作流循环 | `references/onboard.md` |

## 使用流程

1. 根据用户当前所处的工作流阶段匹配子技能
2. 读取对应的 `references/*.md` 获取详细操作指令
3. 按照指令执行

## 典型工作流顺序

```
explore → propose/new-change → continue-change/ff-change → apply-change → verify-change → sync-specs → archive-change
```
