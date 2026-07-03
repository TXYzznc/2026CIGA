---
name: moai-docs-generation
description: 针对技术规格、API文档、用户指南和知识库的文档生成模式，使用Sphinx、MkDocs、TypeDoc和Nextra等实用工具。适用于从代码生成文档、搭建文档站点或自动化文档工作流的场景。
license: Apache-2.0
compatibility: Designed for Claude Code
allowed-tools: Read Write Edit Grep Glob Bash(npm:*) Bash(npx:*) Bash(git:*) Bash(sphinx-build:*)
  Bash(mkdocs:*) Bash(typedoc:*) mcp__context7__resolve-library-id mcp__context7__get-library-docs
user-invocable: false
metadata:
  version: 2.1.0
  category: workflow
  status: active
  updated: '2026-01-08'
  modularized: 'true'
  tags: workflow, documentation, sphinx, mkdocs, typedoc, api-docs, static-sites
  context: fork
  agent: general-purpose
tags: documentation-generation, api-docs, sphinx-guide, mkdocs-material, ci-cd-docs-pipeline
tags_cn: 文档生成, API文档制作, Sphinx使用指南, MkDocs Material主题, CI/CD文档流水线
---

# 文档生成模式

## 快速参考（30秒）

用途：使用成熟工具和框架生成专业文档。

核心文档工具：
- Python：搭配autodoc的Sphinx、搭配Material主题的MkDocs、pydoc
- TypeScript/JavaScript：TypeDoc、JSDoc、TSDoc
- API文档：来自FastAPI/Express的OpenAPI/Swagger、Redoc、Stoplight
- 静态站点：Nextra（Next.js）、Docusaurus（React）、VitePress（Vue）
- 通用格式：Markdown、MDX、reStructuredText

适用场景：
- 从代码注释生成API文档
- 搭建带搜索和导航功能的文档站点
- 创建用户指南和技术规格说明
- 在CI/CD流水线中自动化文档更新
- 转换不同文档格式

---

## 实施指南（5分钟）

### 基于Sphinx的Python文档生成

Sphinx安装与配置：

使用pip安装Sphinx及扩展：pip install sphinx sphinx-autodoc-typehints sphinx-rtd-theme myst-parser

运行sphinx-quickstart docs初始化Sphinx项目，创建基础结构。

在conf.py中配置以下关键设置：
- 扩展列表包含autodoc、napoleon、typehints和myst_parser
- 将html_theme设置为sphinx_rtd_theme以获得专业外观
- 将autodoc_typehints设置为description以启用内联类型提示

运行sphinx-apidoc，指定源码目录，将输出写入docs/api，然后在docs目录中运行make html生成API文档。

### 基于MkDocs的Python文档生成

MkDocs Material安装：

使用pip安装：pip install mkdocs mkdocs-material mkdocstrings mkdocstrings-python

创建mkdocs.yml配置文件：
- 设置site_name和site_url
- 配置主题为material并指定所需配色方案
- 添加search和mkdocstrings等插件
- 定义包含章节和页面的导航结构

在Markdown文件中使用mkdocstrings语法::: module.path，从文档字符串自动生成API文档。

使用mkdocs serve本地预览，mkdocs build构建站点，mkdocs gh-deploy部署站点。

### 基于TypeDoc的TypeScript文档生成

TypeDoc安装：

使用npm安装：npm install typedoc --save-dev

在package.json的scripts中添加：typedoc --out docs/api src/index.ts

在typedoc.json中配置：
- 设置entryPoints为源码文件
- 配置out为docs/api
- 启用includeVersion和categorizeByGroup
- 设置主题为默认或安装自定义主题

运行npm run docs:generate生成文档。

### 基于JSDoc的JavaScript文档生成

JSDoc安装：

使用npm安装：npm install jsdoc --save-dev

创建jsdoc.json配置文件：
- 设置source的包含路径和includePattern
- 配置模板和输出目标
- 启用markdown插件以支持富格式

使用JSDoc注释标记函数：
- @param：带类型和描述的参数
- @returns：返回值说明
- @example：使用示例
- @throws：错误说明

### OpenAPI/Swagger文档生成

FastAPI自动文档：

FastAPI提供自动生成的OpenAPI文档。可在/docs访问Swagger UI，在/redoc访问ReDoc。

增强文档的方法：
- 为路由处理函数添加文档字符串
- 使用response_model定义类型化响应
- 在Pydantic模型的Config类中定义示例
- 为端点分组设置tags
- 在路由装饰器中添加详细描述

通过app.openapi()以编程方式导出OpenAPI规范并保存为openapi.json。

搭配Swagger的Express：

安装swagger-jsdoc和swagger-ui-express。

使用OpenAPI定义和API文件路径配置swagger-jsdoc。

为路由处理函数添加@openapi注释，说明路径、参数和响应。

在/api-docs端点提供Swagger UI服务。

### 静态文档站点搭建

Nextra（Next.js）：

参考Skill("moai-library-nextra")获取完整的Nextra模式。

核心优势：支持MDX、文件系统路由、内置搜索、主题自定义。

使用npx create-nextra-app创建项目，配置theme.config.tsx，在pages目录中组织页面。

Docusaurus（React）：

使用npx create-docusaurus@latest my-docs classic初始化。

在docusaurus.config.js中配置：
- 设置包含标题、标语、网址的siteMetadata
- 配置包含文档和博客设置的presets
- 添加导航栏和页脚的themeConfig
- 启用algolia插件实现搜索功能

在docs文件夹中组织文档，使用category.json文件定义侧边栏结构。

VitePress（Vue）：

使用npm init vitepress初始化。

在.vitepress/config.js中配置：
- 设置标题、描述、基础路径
- 定义包含导航和侧边栏的themeConfig
- 配置搜索和社交链接

使用Markdown搭配Vue组件、代码高亮和前置元数据。

---

## 进阶模式（10+分钟）

### 从SPEC文件生成文档

从MoAI SPEC文件生成文档的模式：

读取SPEC文件内容并提取关键部分：id、标题、描述、需求、api_endpoints。

生成结构化Markdown文档：
- 从描述创建概述部分
- 将需求列为功能要点
- 记录每个API端点的方法、路径和描述
- 根据端点定义添加使用示例

将生成的文档保存到docs目录中的对应位置。

### CI/CD文档流水线

GitHub Actions工作流：

创建.github/workflows/docs.yml，当main分支的src或docs路径发生变更时触发。

工作流步骤：
- 检出代码仓库
- 设置语言运行时（Python、Node.js）
- 安装文档依赖
- 使用对应工具生成文档
- 部署到GitHub Pages、Netlify或Vercel

Python/Sphinx示例：
- 使用pip install sphinx sphinx-rtd-theme安装依赖
- 使用sphinx-build -b html docs/source docs/build生成文档
- 使用actions-gh-pages action部署

TypeScript/TypeDoc示例：
- 使用npm ci安装依赖
- 使用npm run docs:generate生成文档
- 部署到Pages

### 文档验证

链接检查：

使用linkchecker验证HTML输出中的本地链接。

对于Markdown，在pre-commit钩子中使用markdown-link-check。

拼写检查：

使用搭配Aspell的pyspelling实现自动拼写检查。

在.pyspelling.yml中配置不同文件类型的矩阵条目。

文档覆盖率：

对于Python，使用interrogate检查文档字符串覆盖率。

在pyproject.toml中配置最低覆盖率阈值。

如果覆盖率低于阈值，CI构建将失败。

### 多语言文档

Nextra国际化：

在next.config.js中配置i18n，设置locales数组和defaultLocale。

在pages/[locale]目录中创建特定语言的页面。

使用next-intl或类似工具进行翻译。

Docusaurus国际化：

在docusaurus.config.js中配置i18n，设置defaultLocale和locales。

使用docusaurus write-translations生成翻译文件。

在i18n/[locale]目录结构中组织翻译内容。

---

## 协同技能

技能：
- moai-library-nextra - 完整的Nextra文档框架模式
- moai-lang-python - Python文档字符串规范与类型标注
- moai-lang-typescript - TypeScript/JSDoc文档模式
- moai-domain-backend - 后端服务的API文档
- moai-workflow-project - 项目文档集成

代理：
- manager-docs - 文档工作流编排
- expert-backend - API端点文档
- expert-frontend - 组件文档

命令：
- /moai:3-sync - 文档与代码变更同步

---

## 工具参考

Python文档工具：
- Sphinx: https://www.sphinx-doc.org/
- MkDocs: https://www.mkdocs.org/
- MkDocs Material: https://squidfunk.github.io/mkdocs-material/
- mkdocstrings: https://mkdocstrings.github.io/

JavaScript/TypeScript文档工具：
- TypeDoc: https://typedoc.org/
- JSDoc: https://jsdoc.app/
- TSDoc: https://tsdoc.org/

API文档工具：
- OpenAPI Specification: https://spec.openapis.org/
- Swagger UI: https://swagger.io/tools/swagger-ui/
- Redoc: https://redocly.github.io/redoc/
- Stoplight: https://stoplight.io/

静态站点生成器：
- Nextra: https://nextra.site/
- Docusaurus: https://docusaurus.io/
- VitePress: https://vitepress.dev/

风格指南：
- Google开发者文档风格指南: https://developers.google.com/style
- Microsoft写作风格指南: https://learn.microsoft.com/style-guide/

---

版本: 2.0.0
最后更新: 2025-12-30