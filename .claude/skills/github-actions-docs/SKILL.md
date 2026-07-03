---
name: github-actions-docs
description: 当用户询问如何编写、解释、自定义、迁移、保护或排查GitHub Actions工作流、工作流语法、触发器、矩阵、运行器、可复用工作流、工件、缓存、密钥、OIDC、部署、自定义操作或Actions
  Runner Controller相关问题时使用，尤其是当他们需要官方GitHub文档、准确链接或基于文档的YAML指导时。
tags: github-actions, workflow-authoring, ci-cd-documentation, runner-management,
  security-best-practices
tags_cn: GitHub Actions, 工作流编写, CI/CD文档, 运行器管理, 安全最佳实践
---

# GitHub Actions 文档

GitHub Actions相关问题很容易凭借陈旧记忆回答。请使用本技能，基于官方GitHub文档给出答案，并返回最相关的权威页面，而非通用CI/CD建议。

## 使用场景

当请求涉及以下内容时，使用本技能：

- GitHub Actions的概念、术语或产品边界
- 工作流YAML、触发器、任务、矩阵、并发、变量、上下文或表达式
- GitHub托管运行器、大型运行器、自托管运行器或Actions Runner Controller
- 工件、缓存、可复用工作流、工作流模板或自定义操作
- 密钥、`GITHUB_TOKEN`、OpenID Connect、工件认证或安全工作流模式
- 环境、部署保护规则、部署历史或部署示例
- 从Jenkins、CircleCI、GitLab CI/CD、Travis CI、Azure Pipelines或其他CI系统迁移
- 当用户需要文档、语法指导或官方参考来排查工作流行为问题时

以下场景请勿使用本技能：

- 特定的PR检查失败、缺失工作流日志或CI故障分类。请使用`gh-fix-ci`。
- 通用的GitHub拉取请求、分支或仓库操作。请使用`github`。
- CodeQL特定配置或代码扫描指导。请使用`codeql`。
- Dependabot配置、分组或依赖更新策略。请使用`dependabot`。

## 工作流程

### 1. 对请求进行分类

在搜索前，先确定问题所属类别：

- 入门或教程
- 工作流编写与语法
- 运行器与执行环境
- 安全与供应链
- 部署与环境
- 自定义操作与发布
- 监控、日志与故障排查
- 迁移

如果需要快速找到切入点，请加载`references/topic-map.md`并跳转到最相关的章节。

### 2. 优先搜索官方GitHub文档

- 将`docs.github.com`视为权威来源。
- 优先选择<https://docs.github.com/en/actions>下的页面。
- 使用用户的准确术语加上聚焦的Actions相关短语进行搜索，例如`workflow syntax`、`OIDC`、`reusable workflows`或`self-hosted runners`。
- 当多个页面看似相关时，对比2-3个候选页面，选择最直接回答用户问题的页面。

### 3. 在回答前打开最佳匹配页面

- 阅读最相关的页面，如有可能，阅读具体章节。
- 仅使用主题地图来缩小搜索范围或找到可能的起始页面。
- 如果页面已重命名、移动或内容不完整，请明确说明，并返回最接近的权威页面，而非猜测内容。

### 4. 基于文档给出指导

- 先用直白语言给出直接答案。
- 包含准确的GitHub文档链接，而非仅文档首页。
- 仅当用户要求或文档页面明确需要时，才提供YAML或分步示例。
- 如有推断，请明确说明。例如：
  - `根据GitHub文档，……`
  - `推断：这可能意味着……`

## 回答结构

除非用户要求深入解释，否则请使用简洁结构：

1. 直接答案
2. 相关文档
3. YAML示例或步骤（仅在需要时提供）
4. 明确的推断说明（仅当需要关联多个文档页面时提供）

请将引用内容紧邻其支持的结论放置。

## 搜索与路由技巧

- 对于概念类问题，优先选择概述或概念页面，而非深度参考页面。
- 对于语法类问题，优先选择工作流语法、事件、上下文、变量或表达式参考页面。
- 对于安全类问题，优先选择`安全使用`、`密钥`、`GITHUB_TOKEN`、`OpenID Connect`和工件认证相关文档。
- 对于部署类问题，优先选择环境与部署保护文档，而非云服务商特定示例。
- 对于迁移类问题，优先选择迁移中心页面，再选择平台特定的迁移指南。
- 如果用户要求初学者入门教程，请从教程或快速开始页面入手，而非原始参考页面。

## 常见错误

- 未验证当前文档，仅凭记忆回答
- 当存在更具体的页面时，仍链接GitHub Actions文档首页
- 混淆可复用工作流与复合操作
- 当OIDC是文档推荐的更优方案时，仍建议使用长期云凭证
- 将仓库特定的CI调试视为文档问题，而实际上应交给`gh-fix-ci`处理
- 当`codeql`或`dependabot`更合适时，仍用本技能处理相关请求

## 内置参考

仅将`references/topic-map.md`作为可能的文档入口点的紧凑索引使用。该文档内容故意不完整，绝不能替代实时GitHub文档作为最终权威来源。