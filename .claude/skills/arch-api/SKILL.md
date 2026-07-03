---
name: arch-api
cluster: se-architecture
description: API架构：REST设计、版本控制、HATEOAS、认证模式、OpenAPI文档、网关模式
tags: api-architecture, rest-design, openapi-docs, api-gateway, jwt-authentication
dependencies: []
composes: []
similar_to: []
called_by: []
authorization_required: false
scope: general
model_hint: claude-sonnet
embedding_hint: api architecture design rest versioning openapi gateway hateoas
tags_cn: API架构设计, REST设计, OpenAPI文档, API网关, JWT认证
---

## 用途

该技能支持OpenClaw设计、文档化并实现以REST原则、版本控制策略、HATEOAS、认证模式、OpenAPI规范以及API网关设计为核心的API架构，确保API具备可扩展性、安全性并符合标准。

## 使用场景

在设计新的RESTful API、将遗留API迁移至现代标准、实现版本控制以保障向后兼容性，或集成HATEOAS实现超媒体驱动响应时，可使用该技能。适用于微服务环境、为Swagger UI生成OpenAPI文档，或评估JWT等用于受保护端点的认证模式时。

## 核心能力

- REST设计：生成URI结构（例如：/resources/{id}），并将HTTP方法（GET、POST）与特定响应码（200 OK、404 Not Found）进行映射。
- 版本控制：支持基于URI的版本控制（例如：/v1/resources）或基于请求头的版本控制（例如：Accept: application/vnd.myapi.v1+json）。
- HATEOAS：在JSON响应中添加自链接及相关资源链接，示例：{"_links": {"self": "/users/1"}}。
- 认证模式：实现JWT或OAuth2流程，包括令牌验证和权限范围（例如：read:users）。
- OpenAPI文档：从代码或设计自动生成OpenAPI 3.0 YAML/JSON规范，包含数据模型和端点信息。
- 网关模式：设计用于路由的API网关，例如使用Kong或AWS API Gateway等实现负载均衡的模式。

## 使用模式

通过OpenClaw CLI调用该技能，命令结构为：`openclaw arch-api <subcommand> [flags]`。执行命令前，请务必通过环境变量设置认证密钥：`export OPENCLAW_API_KEY=your_api_key`。如需交互式使用，可将输出管道传输至其他工具，例如：`openclaw arch-api design --rest | jq .`。复杂输入可使用JSON配置文件，格式示例：{"endpoints": [{"path": "/users", "method": "GET"}]}.

设计REST API时，运行：`openclaw arch-api design --type rest --version v1 --hateoas`。生成OpenAPI文档时，提供源文件：`openclaw arch-api docs --input api-spec.json --output openapi.yaml`。

## 常用命令/API

- 命令：`openclaw arch-api design --rest --endpoints users/get,users/post --version v1`
  参数：--hateoas用于启用HATEOAS链接；--auth jwt用于添加JWT认证。
  代码片段：
  ```
  openclaw arch-api design --rest > api_design.json
  cat api_design.json | grep "uri"
  ```

- 命令：`openclaw arch-api generate-openapi --from-code ./src/api/controllers.py --output docs/openapi.yaml`
  参数：--validate用于检查错误；--gateway kong用于适配网关特定模式。
  代码片段：
  ```
  export OPENCLAW_API_KEY=abc123
  openclaw arch-api generate-openapi --from-code .
  ```

- API端点：若使用OpenClaw内部API，向/v1/arch-api/design发送POST请求，请求体为：{"type": "rest", "version": "v1"}。响应包含类似如下的JSON：{"endpoints": [{"path": "/users", "method": "GET"}]}。
  配置格式：使用YAML作为输入，示例：
  ```
  endpoints:
    - path: /users
      method: GET
      auth: jwt
  ```

- 命令：`openclaw arch-api validate --spec openapi.yaml --check hateoas`
  参数：--check versioning用于验证版本请求头。

## 集成说明

通过链式命令将该技能与其他OpenClaw技能集成，例如：`openclaw chain arch-api se-deployment --input api_design.json`。如需与外部工具集成，可将输出导出为文件：`openclaw arch-api design > input_for_deployment.txt`。若需要认证，脚本中请始终使用环境变量模式：`$OPENCLAW_API_KEY`，例如在bash脚本中：
```
#!/bin/bash
export OPENCLAW_API_KEY=your_key
openclaw arch-api design --rest
```
配置文件共享时，使用与Swagger Editor等工具兼容的JSON文件，确保字段匹配OpenAPI规范。

## 错误处理

检查命令退出码：若`openclaw arch-api design`返回非零值，请解析标准错误流获取类似“Invalid endpoint format”（端点格式无效）的提示信息。对于API调用，处理HTTP错误：若状态码为401，请使用刷新后的$OPENCLAW_API_KEY重试。在脚本中使用try-catch语句：
```
try {
  openclaw arch-api generate-openapi --input invalid.json
} catch (e) {
  if (e.includes("Validation error")) { console.log("Fix JSON schema"); }
}
```
常见错误：400表示输入格式错误（例如缺少--version参数）；403表示$OPENCLAW_API_KEY无效。使用以下命令记录错误：`openclaw arch-api design 2>> error.log`。

## 实际使用示例

1. 为用户管理系统设计REST API：首先设置认证密钥：`export OPENCLAW_API_KEY=your_key`。然后运行：`openclaw arch-api design --rest --endpoints users/get,users/post --version v1 --auth jwt --hateoas`。该命令会输出包含类似{"path": "/users", "method": "GET", "_links": {"self": "/users/1"}}的端点信息的JSON文件，可用于代码层面的API开发。

2. 为现有代码库生成OpenAPI文档：导出认证密钥：`export OPENCLAW_API_KEY=your_key`。执行命令：`openclaw arch-api generate-openapi --from-code ./src/api --gateway aws --output api_docs.yaml`。该命令会生成一个YAML格式的规范文件，包含/users等路径以及JWT安全方案，可直接集成到Swagger UI中。

## 关联关系

- 关联技能：se-deployment（用于通过网关模式部署设计好的API）。
- 关联集群：se-architecture集群（共享api-design等标签，用于协作技能）。
- 链接技能：se-auth（用于API中认证逻辑的详细实现）。
- 关联工具：openapi-tool（用于扩展OpenAPI文档生成功能）。