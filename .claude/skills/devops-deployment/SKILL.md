---
name: devops-deployment
description: 适用于搭建CI/CD流水线、应用容器化、部署至Kubernetes或编写基础设施即代码场景。DevOps与部署内容涵盖GitHub Actions、Docker、Helm及Terraform相关模式。
tags: ci-cd-pipelines, containerization, kubernetes-deployment, infrastructure-as-code,
  devops-practices
context: fork
agent: data-pipeline-engineer
version: 1.0.0
category: Infrastructure & Deployment
agents:
- backend-system-architect
- code-quality-reviewer
- studio-coach
keywords:
- CI/CD
- deployment
- Docker
- Kubernetes
- pipeline
- infrastructure
- GitOps
- container
- automation
- release
author: OrchestKit
user-invocable: false
complexity: medium
tags_cn: CI/CD流水线, 应用容器化, Kubernetes部署, 基础设施即代码, DevOps实践
---

# DevOps与部署技能

CI/CD流水线、容器化、部署策略及基础设施自动化的综合框架。

## 概述

- 搭建CI/CD流水线
- 应用容器化
- 部署至Kubernetes或云平台
- 实施GitOps工作流
- 基础设施即代码管理
- 发布策略规划

## 流水线架构

```
┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│    Code     │──>│    Build    │──>│    Test     │──>│   Deploy    │
│   Commit    │   │   & Lint    │   │   & Scan    │   │  & Release  │
└─────────────┘   └─────────────┘   └─────────────┘   └─────────────┘
       │                 │                 │                 │
       v                 v                 v                 v
   Triggers         Artifacts          Reports          Monitoring
```

## 核心概念

### CI/CD流水线阶段

1. **代码检查与类型校验** - 代码质量门禁
2. **单元测试** - 带报告的测试覆盖率统计
3. **安全扫描** - npm audit + Trivy漏洞扫描器
4. **构建与推送** - 将Docker镜像推送至容器镜像仓库
5. **部署至预发布环境** - 环境门禁式部署
6. **部署至生产环境** - 手动审批或自动化部署

### 容器化最佳实践

**多阶段构建**可最小化镜像体积：
- 阶段1：仅安装生产环境依赖
- 阶段2：使用开发依赖构建应用
- 阶段3：生产运行时，保持最小镜像体积

**安全加固**：
- 非root用户（uid 1001）
- 尽可能使用只读文件系统
- 用于编排器集成的健康检查

### Kubernetes部署

**必备清单**：
- 采用滚动更新策略的Deployment
- 用于内部路由的Service
- 带TLS的外部访问Ingress
- 用于自动扩缩容的HorizontalPodAutoscaler

**安全上下文**：
- `runAsNonRoot: true`
- `allowPrivilegeEscalation: false`
- `readOnlyRootFilesystem: true`
- 移除所有权限

### 部署策略

| 策略 | 适用场景 | 风险 |
|----------|----------|------|
| **滚动部署** | 默认方案，逐步替换实例 | 低风险 - 支持自动回滚 |
| **蓝绿部署** | 即时切换，易于回滚 | 中风险 - 需双倍资源 |
| **金丝雀发布** | 渐进式流量切换 | 低风险 - 逐步暴露新版本 |

**滚动更新**（Kubernetes默认）：
```yaml
strategy:
  type: RollingUpdate
  rollingUpdate:
    maxSurge: 25%
    maxUnavailable: 0  # Zero downtime
```

### 密钥管理

使用External Secrets Operator与云服务商同步密钥：
- AWS Secrets Manager
- HashiCorp Vault
- Azure Key Vault
- GCP Secret Manager

---

## 参考资料

### Docker模式
**查看：`references/docker-patterns.md`**

涵盖核心主题：
- 多阶段构建示例，可减少78%镜像体积
- 镜像层缓存优化
- 安全加固（非root用户、健康检查）
- Trivy漏洞扫描
- Docker Compose开发环境配置

### CI/CD流水线
**查看：`references/ci-cd-pipelines.md`**

涵盖核心主题：
- 分支策略（Git Flow）
- GitHub Actions缓存（节省85%时间）
- 制品管理
- 矩阵测试
- 完整后端CI/CD示例

### Kubernetes基础
**查看：`references/kubernetes-basics.md`**

涵盖核心主题：
- 健康探针（启动探针、存活探针、就绪探针）
- 安全上下文配置
- PodDisruptionBudget
- 资源配额
- 用于数据库的StatefulSets
- Helm Chart结构

### 环境管理
**查看：`references/environment-management.md`**

涵盖核心主题：
- External Secrets Operator
- 基于ArgoCD的GitOps
- Terraform模式（远程状态、模块）
- 零停机数据库迁移
- Alembic迁移工作流
- 回滚流程

### 可观测性
**查看：`references/observability.md`**

涵盖核心主题：
- Prometheus指标暴露
- Grafana仪表盘查询（PromQL）
- SLO告警规则
- 黄金信号（SRE）
- 结构化日志
- 分布式追踪（OpenTelemetry）

### 部署策略
**查看：`references/deployment-strategies.md`**

涵盖核心主题：
- 滚动部署
- 蓝绿部署
- 金丝雀发布
- 基于Istio的流量拆分

---

## 部署检查清单

### 部署前
- [ ] CI中所有测试已通过
- [ ] 安全扫描无问题
- [ ] 数据库迁移已准备就绪
- [ ] 回滚计划已文档化

### 部署中
- [ ] 监控部署进度
- [ ] 关注错误率
- [ ] 验证健康检查已通过

### 部署后
- [ ] 验证指标正常
- [ ] 检查日志是否存在错误
- [ ] 更新状态页面

---

## Helm Chart结构

```
charts/app/
├── Chart.yaml
├── values.yaml
├── scripts/
│   ├── deployment.yaml
│   ├── service.yaml
│   ├── ingress.yaml
│   ├── configmap.yaml
│   ├── secret.yaml
│   ├── hpa.yaml
│   └── _helpers.tpl
└── values/
    ├── staging.yaml
    └── production.yaml
```

---

## 相关技能

- `zero-downtime-migration` - 用于零停机部署的数据库迁移模式
- `security-scanning` - CI/CD流水线集成安全扫描
- `observability-monitoring` - 已部署应用的监控与告警
- `alembic-migrations` - 后端部署的Python/Alembic迁移工作流

## 关键决策

| 决策项 | 选择方案 | 理由 |
|----------|--------|-----------|
| 容器用户 | 非root用户（uid 1001） | 安全最佳实践，多数编排器强制要求 |
| 部署策略 | 滚动更新（默认） | 零停机、自动回滚、资源高效 |
| 密钥管理 | External Secrets Operator | 与云服务商同步，兼容GitOps |
| 健康检查 | 分离启动/存活/就绪探针 | 避免过早接入流量，支持优雅停机 |

---

## 拓展思考触发点

使用Opus 4.6自适应思考解决以下问题：
- **架构决策** - Kubernetes vs 无服务器架构、多区域部署
- **迁移规划** - 跨云服务商迁移
- **事件响应** - 复杂部署故障处理
- **安全设计** - 零信任架构

---

## 模板参考

| 模板 | 用途 |
|----------|---------|
| `github-actions-pipeline.yml` | 包含6个阶段的完整CI/CD工作流 |
| `Dockerfile` | 多阶段Node.js构建 |
| `docker-compose.yml` | 开发环境配置 |
| `k8s-manifests.yaml` | Deployment、Service、Ingress配置 |
| `helm-values.yaml` | Helm Chart配置值 |
| `terraform-aws.tf` | VPC、EKS、RDS基础设施配置 |
| `argocd-application.yaml` | GitOps应用配置 |
| `external-secrets.yaml` | Secrets Manager集成配置 |

---

## 能力细节

### ci-cd
**关键词:** ci, cd, pipeline, github actions, gitlab ci, jenkins, workflow
**解决问题:**
- 如何搭建CI/CD？
- GitHub Actions工作流模式
- 流水线缓存策略
- 矩阵测试配置

### docker
**关键词:** docker, dockerfile, container, image, build, compose, multi-stage
**解决问题:**
- 如何将应用容器化？
- 多阶段Dockerfile最佳实践
- Docker Compose开发环境配置
- 容器安全加固

### kubernetes
**关键词:** kubernetes, k8s, deployment, service, ingress, helm, statefulset, pdb
**解决问题:**
- 如何部署至Kubernetes？
- K8s健康探针与资源限制
- Helm Chart结构
- 用于数据库的StatefulSet

### infrastructure-as-code
**关键词:** terraform, pulumi, iac, infrastructure, provision, gitops, argocd
**解决问题:**
- 如何编写基础设施即代码？
- Terraform AWS模式（VPC、EKS、RDS）
- 基于ArgoCD的GitOps
- 密钥管理模式

### deployment-strategies
**关键词:** blue green, canary, rolling, deployment strategy, rollback, zero downtime
**解决问题:**
- 应选择哪种部署策略？
- 零停机数据库迁移
- 蓝绿部署配置
- 带流量拆分的金丝雀发布

### observability
**关键词:** prometheus, grafana, metrics, alerting, monitoring, health check
**解决问题:**
- 如何为应用添加监控？
- Prometheus指标暴露
- Grafana仪表盘查询
- SLO告警规则