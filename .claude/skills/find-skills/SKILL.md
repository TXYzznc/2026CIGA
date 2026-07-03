---
name: find-skills
description: 当用户提出‘我该如何完成X’‘寻找适用于X的技能’‘有没有能……的技能’这类问题，或是表达出扩展功能的需求时，帮助他们发现并安装Agent技能。当用户寻求可能作为可安装技能存在的功能时，即可使用本技能。
tags: agent-skills, skill-discovery, skill-installation, skills-cli, agent-ecosystem
tags_cn: Agent技能, 技能发现, 技能安装, Skills CLI, Agent生态系统
---

# 寻找技能

本技能可帮助你从开放Agent技能生态中发现并安装技能。

## 何时使用本技能

当用户：

- 提出‘我该如何完成X’这类问题，且X是已有对应技能的常见任务时
- 说‘寻找适用于X的技能’或‘有没有适用于X的技能’
- 询问‘你能完成X吗’，其中X是一项专业功能
- 表达出扩展Agent功能的需求
- 想要搜索工具、模板或工作流
- 提到希望在特定领域（设计、测试、部署等）获得帮助时

## 什么是Skills CLI？

Skills CLI（`npx skills`）是开放Agent技能生态的包管理器。技能是模块化的软件包，可通过专业知识、工作流和工具扩展Agent的功能。

**核心命令：**

- `npx skills find [query]` - 通过交互方式或关键词搜索技能
- `npx skills add <package>` - 从GitHub或其他来源安装技能
- `npx skills check` - 检查技能更新
- `npx skills update` - 更新所有已安装的技能

**浏览技能请访问：** https://skills.sh/

## 如何帮助用户寻找技能

### 步骤1：了解用户需求

当用户请求帮助时，明确：

1. 领域（例如React、测试、设计、部署）
2. 具体任务（例如编写测试、创建动画、审核PR）
3. 该任务是否足够常见，很可能已有对应技能存在

### 步骤2：搜索技能

运行find命令并传入相关查询词：

```bash
npx skills find [query]
```

例如：

- 用户问‘我该如何让我的React应用更快？’ → `npx skills find react performance`
- 用户问‘你能帮我审核PR吗？’ → `npx skills find pr review`
- 用户问‘我需要创建变更日志’ → `npx skills find changelog`

该命令将返回如下结果：

```
Install with npx skills add <owner/repo@skill>

vercel-labs/agent-skills@vercel-react-best-practices
└ https://skills.sh/vercel-labs/agent-skills/vercel-react-best-practices
```

### 步骤3：为用户展示选项

当你找到相关技能时，向用户展示以下信息：

1. 技能名称及其功能
2. 可运行的安装命令
3. 可了解更多信息的skills.sh链接

示例回复：

```
我找到了一个可能对你有帮助的技能！“vercel-react-best-practices”技能由Vercel Engineering提供，包含React和Next.js性能优化指南。

安装命令：
npx skills add vercel-labs/agent-skills@vercel-react-best-practices

了解更多：https://skills.sh/vercel-labs/agent-skills/vercel-react-best-practices
```

### 步骤4：提供安装帮助

如果用户希望继续，你可以帮他们安装技能：

```bash
npx skills add <owner/repo@skill> -g -y
```

`-g` 标志表示全局安装（用户级别），`-y` 标志表示跳过确认提示。

## 常见技能分类

搜索时，可参考以下常见分类：

| 分类        | 示例查询词                          |
| --------------- | ---------------------------------------- |
| 网页开发 | react, nextjs, typescript, css, tailwind |
| 测试         | testing, jest, playwright, e2e           |
| DevOps          | deploy, docker, kubernetes, ci-cd        |
| 文档   | docs, readme, changelog, api-docs        |
| 代码质量    | review, lint, refactor, best-practices   |
| 设计          | ui, ux, design-system, accessibility     |
| 生产力    | workflow, automation, git                |

## 高效搜索技巧

1. **使用具体关键词**："react testing"比单纯的"testing"效果更好
2. **尝试替代术语**：如果"deploy"没有结果，试试"deployment"或"ci-cd"
3. **查看热门来源**：许多技能来自`vercel-labs/agent-skills`或`ComposioHQ/awesome-claude-skills`

## 未找到技能时

如果没有找到相关技能：

1. 告知用户未找到现有技能
2. 提出可利用自身通用能力直接帮助完成任务
3. 建议用户使用`npx skills init`创建自己的技能

示例：

```
我搜索了与“xyz”相关的技能，但未找到匹配结果。
我仍可直接帮你完成这项任务！是否需要我继续？

如果你经常需要完成这项任务，可以创建自己的技能：
npx skills init my-xyz-skill
```