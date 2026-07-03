---
name: opentelemetry
license: Apache-2.0
description: 结合Grafana栈使用OpenTelemetry。内容涵盖Go/Java/Python/Node.js/.NET的OTel SDK埋点、OTLP协议与端点配置、通过OTLP端点将遥测数据发送至Grafana
  Cloud、作为OTel收集器的Grafana Alloy、采样策略、Kubernetes OTel Operator，以及从其他可观测性工具迁移的方法。适用于使用OTel为应用埋点、配置OTLP端点、搭建收集器或迁移至OpenTelemetry的场景。
tags: opentelemetry-grafana, otlp-configuration, observability-instrumentation, grafana-alloy,
  kubernetes-otel-operator
tags_cn: OpenTelemetry与Grafana集成, OTLP配置, 可观测性埋点, Grafana Alloy收集器, Kubernetes OTel
  Operator
---

# OpenTelemetry 结合 Grafana 使用

## 概述

OpenTelemetry（OTel）是一个厂商中立的可观测性数据（指标、日志、链路追踪、性能剖析）采集框架。Grafana Labs 将其作为核心集成策略，提供一套完整的栈来采集、摄入、存储、分析和可视化遥测数据。

### 四步实施模型

1. **埋点** - 使用Grafana SDK、Beyla（eBPF）或上游OTel SDK添加遥测数据
2. **流水线** - 使用Grafana Alloy或OTel Collector构建处理基础设施
3. **摄入** - 将数据路由至Grafana Cloud OTLP端点或自托管后端
4. **分析** - 仪表盘、告警、应用可观测性、钻取应用

### Grafana 后端

| 信号类型 | 后端服务 |
|--------|---------|
| 指标 | Grafana Mimir |
| 日志 | Grafana Loki |
| 链路追踪 | Grafana Tempo |
| 性能剖析 | Grafana Pyroscope |

---

## OTLP 端点与认证

### Grafana Cloud OTLP 端点

Grafana Cloud 提供一个托管的OTLP网关端点：

```
https://otlp-gateway-<region>.grafana.net/otlp
```

示例区域：`prod-us-east-0`, `prod-eu-west-0`, `prod-ap-southeast-0`

完整示例：
```
https://otlp-gateway-prod-us-east-0.grafana.net/otlp
```

### 认证 - 基础认证

Grafana Cloud OTLP 使用**HTTP基础认证**：
- **用户名**：Grafana Cloud实例ID（数字格式，例如 `123456`）
- **密码**：Grafana Cloud API令牌（需具备MetricsPublisher、LogsPublisher、TracesPublisher权限）

#### 通过环境变量（推荐方式）

```bash
# 对"instanceID:apiToken"进行Base64编码
export OTEL_EXPORTER_OTLP_HEADERS="Authorization=Basic $(echo -n '123456:glc_eyJ...' | base64)"
```

#### 通过Alloy环境变量

```bash
export GRAFANA_CLOUD_INSTANCE_ID=123456
export GRAFANA_CLOUD_API_KEY=glc_eyJ...
export GRAFANA_CLOUD_OTLP_ENDPOINT=https://otlp-gateway-prod-us-east-0.grafana.net/otlp
```

### 直接发送（无收集器）- 环境变量配置

```bash
export OTEL_EXPORTER_OTLP_ENDPOINT=https://otlp-gateway-prod-us-east-0.grafana.net/otlp
export OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
export OTEL_EXPORTER_OTLP_HEADERS="Authorization=Basic <base64(instanceID:apiToken)>"
export OTEL_RESOURCE_ATTRIBUTES="service.name=myapp,service.namespace=myteam,deployment.environment=production"
```

---

## 各语言埋点方案

### Go

**要求：** Go 1.22+

**安装依赖包：**
```bash
go get "go.opentelemetry.io/contrib/instrumentation/net/http/otelhttp" \
  "go.opentelemetry.io/contrib/instrumentation/runtime" \
  "go.opentelemetry.io/otel" \
  "go.opentelemetry.io/otel/exporters/otlp/otlpmetric/otlpmetrichttp" \
  "go.opentelemetry.io/otel/exporters/otlp/otlptrace" \
  "go.opentelemetry.io/otel/exporters/otlp/otlptrace/otlptracehttp" \
  "go.opentelemetry.io/otel/sdk" \
  "go.opentelemetry.io/otel/sdk/metric"
```

**结合环境变量运行：**
```bash
OTEL_RESOURCE_ATTRIBUTES="service.name=myapp,service.namespace=myteam,deployment.environment=prod" \
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 \
OTEL_EXPORTER_OTLP_HEADERS="Authorization=Basic <base64>" \
go run .
```

完整Go代码示例请查看`references/instrumentation.md`。

---

### Java（Grafana发行版 - JVM Agent）

**要求：** JDK 8+

**下载：** 从https://github.com/grafana/grafana-opentelemetry-java/releases下载`grafana-opentelemetry-java.jar`

**运行：**
```bash
OTEL_RESOURCE_ATTRIBUTES="service.name=shoppingcart,service.namespace=ecommerce,deployment.environment=production" \
OTEL_EXPORTER_OTLP_ENDPOINT=https://otlp-gateway-prod-us-east-0.grafana.net/otlp \
OTEL_EXPORTER_OTLP_PROTOCOL="http/protobuf" \
OTEL_EXPORTER_OTLP_HEADERS="Authorization=Basic <base64>" \
java -javaagent:/path/to/grafana-opentelemetry-java.jar -jar myapp.jar
```

**可选：数据节省模式**（降低指标基数）：
```bash
export GRAFANA_OTEL_APPLICATION_OBSERVABILITY_METRICS=true
```

**调试：**
```bash
export OTEL_JAVAAGENT_DEBUG=true
# 启用控制台输出与OTLP输出并存
export OTEL_TRACES_EXPORTER=otlp,console
export OTEL_METRICS_EXPORTER=otlp,console
export OTEL_LOGS_EXPORTER=otlp,console
```

---

### Node.js

**安装依赖：**
```bash
pm install --save @opentelemetry/api
npm install --save @opentelemetry/auto-instrumentations-node
```

**运行：**
```bash
OTEL_TRACES_EXPORTER="otlp" \
OTEL_METRICS_EXPORTER="otlp" \
OTEL_LOGS_EXPORTER="otlp" \
OTEL_NODE_RESOURCE_DETECTORS="env,host,os" \
OTEL_RESOURCE_ATTRIBUTES="service.name=myapp,service.namespace=myteam,deployment.environment=prod" \
OTEL_EXPORTER_OTLP_ENDPOINT=https://otlp-gateway-prod-us-east-0.grafana.net/otlp \
OTEL_EXPORTER_OTLP_HEADERS="Authorization=Basic <base64>" \
NODE_OPTIONS="--require @opentelemetry/auto-instrumentations-node/register" \
node app.js
```

**注意：** 像`@vercel/ncc`这样的打包工具可能会破坏自动埋点钩子。

手动SDK设置示例请查看`references/instrumentation.md`。

---

### Python

**安装依赖：**
```bash
pip install "opentelemetry-distro[otlp]"
opentelemetry-bootstrap -a install
```

**运行：**
```bash
OTEL_RESOURCE_ATTRIBUTES="service.name=myapp,service.namespace=myteam,deployment.environment=prod" \
OTEL_EXPORTER_OTLP_ENDPOINT=https://otlp-gateway-prod-us-east-0.grafana.net/otlp \
OTEL_EXPORTER_OTLP_PROTOCOL="http/protobuf" \
OTEL_EXPORTER_OTLP_HEADERS="Authorization=Basic <base64>" \
opentelemetry-instrument python app.py
```

**多进程服务器**（Gunicorn、uWSGI）：需实现post-fork钩子，为每个工作进程重新初始化OTel提供者。

---

### .NET（Grafana发行版）

**安装NuGet包：**
```bash
dotnet add package Grafana.OpenTelemetry
```

**ASP.NET Core 设置：**
```csharp
using Grafana.OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenTelemetry()
    .WithTracing(configure => configure.UseGrafana())
    .WithMetrics(configure => configure.UseGrafana());
builder.Logging.AddOpenTelemetry(options => options.UseGrafana());
```

**运行：**
```bash
OTEL_RESOURCE_ATTRIBUTES="service.name=myapp,service.namespace=myteam,deployment.environment=prod" \
OTEL_EXPORTER_OTLP_ENDPOINT=https://otlp-gateway-prod-us-east-0.grafana.net/otlp \
OTEL_EXPORTER_OTLP_PROTOCOL="http/protobuf" \
OTEL_EXPORTER_OTLP_HEADERS="Authorization=Basic <base64>" \
dotnet run
```

**要求：** .NET 6+ 或 .NET Framework 4.6.2+

完整.NET示例请查看`references/instrumentation.md`。

---

### Beyla（eBPF - 跨语言）

Grafana Beyla 在网络层实现埋点 - 无需修改代码，适用于任意语言。

```bash
# Docker方式运行
docker run --rm -it \
  --privileged \
  -e BEYLA_SERVICE_NAME=myapp \
  -e OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 \
  -v /sys/kernel/security:/sys/kernel/security \
  grafana/beyla
```

验证方式：`curl http://localhost:9090/metrics`

完整文档：https://grafana.com/docs/beyla/

---

## Grafana Alloy 收集器

Grafana Alloy是推荐使用的OTel Collector发行版。它结合了上游OTel Collector组件与Prometheus导出器，实现基础设施与应用可观测性的关联。

### 为什么使用收集器？

- **成本控制**：在发送前聚合、采样、过滤数据
- **可靠性**：连接失败时缓冲并重试
- **数据增强**：添加资源属性、转换、脱敏和路由数据

### Alloy 端口说明

| 端口 | 协议 | 用途 |
|------|----------|---------|
| 4317 | gRPC | OTLP gRPC 接收器 |
| 4318 | HTTP | OTLP HTTP/protobuf 接收器 |

### 应用 -> Alloy -> Grafana Cloud 流程

**应用环境变量**（指向本地Alloy）：
```bash
export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
export OTEL_EXPORTER_OTLP_PROTOCOL=grpc
```

**Alloy配置环境变量**（Alloy -> Grafana Cloud）：
```bash
export GRAFANA_CLOUD_OTLP_ENDPOINT=https://otlp-gateway-prod-us-east-0.grafana.net/otlp
export GRAFANA_CLOUD_INSTANCE_ID=123456
export GRAFANA_CLOUD_API_KEY=glc_eyJ...
```

完整Alloy配置请查看`references/collector-config.md`。

---

## Kubernetes 环境部署

### 选项1：Grafana Kubernetes Monitoring Helm Chart（推荐）

Grafana Kubernetes Monitoring Helm Chart会部署预配置好OTLP接收器的Alloy。

1. 在集群配置页面启用"OTLP Receivers"
2. 从"Configure Application Instrumentation"部分获取gRPC/HTTP端点
3. 将应用指向集群内的Alloy端点：

```bash
export OTEL_EXPORTER_OTLP_ENDPOINT=<GRPC_ENDPOINT_FROM_HELM>
export OTEL_EXPORTER_OTLP_PROTOCOL=grpc
```

### 选项2：OpenTelemetry Operator

按照官方文档安装，然后使用`Instrumentation`自定义资源实现自动注入：

```yaml
apiVersion: opentelemetry.io/v1alpha1
kind: Instrumentation
metadata:
  name: my-instrumentation
spec:
  exporter:
    endpoint: http://otelcol:4317
  propagators:
    - tracecontext
    - baggage
  java:
    # 使用Grafana发行版镜像
    image: us-docker.pkg.dev/grafanalabs-global/docker-grafana-opentelemetry-java-prod/grafana-opentelemetry-java:2.3.0-beta.1
  nodejs: {}
  python: {}
```

**向Pod中注入埋点**添加注解：
```yaml
metadata:
  annotations:
    instrumentation.opentelemetry.io/inject-java: "true"
    # 或者：inject-nodejs, inject-python, inject-dotnet
```

Kubernetes Alloy Helm配置值与OTel Collector YAML请查看`references/collector-config.md`。

---

## 采样策略

### 头部采样

在链路追踪开始时做出采样决策 - 开销低，但可能遗漏罕见错误。

**环境变量（概率采样器）：**
```bash
export OTEL_TRACES_SAMPLER=parentbased_traceidratio
export OTEL_TRACES_SAMPLER_ARG=0.1   # 采样10%的链路
```

**Alloy头部采样配置：**
```alloy
otelcol.processor.probabilistic_sampler "default" {
  sampling_percentage = 10
  output {
    traces = [otelcol.exporter.otlphttp.grafana_cloud.input]
  }
}
```

### 尾部采样

在收集所有Span后做出采样决策 - 可基于结果采样（例如保留所有错误链路）。

**Alloy尾部采样配置：**
```alloy
otelcol.processor.tail_sampling "default" {
  decision_wait            = "10s"
  num_traces               = 100000
  expected_new_traces_per_sec = 10

  policy {
    name = "keep-errors"
    type = "status_code"
    status_code {
      status_codes = ["ERROR"]
    }
  }

  policy {
    name = "probabilistic-sample"
    type = "probabilistic"
    probabilistic {
      sampling_percentage = 10
    }
  }

  output {
    traces = [otelcol.exporter.otlphttp.grafana_cloud.input]
  }
}
```

---

## 关键环境变量参考

| 变量名 | 描述 | 示例 |
|----------|-------------|---------|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP接收器URL | `https://otlp-gateway-prod-us-east-0.grafana.net/otlp` |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | 传输协议 | `grpc` 或 `http/protobuf` |
| `OTEL_EXPORTER_OTLP_HEADERS` | 认证头 | `Authorization=Basic <base64>` |
| `OTEL_RESOURCE_ATTRIBUTES` | 服务元数据 | `service.name=myapp,service.namespace=team,deployment.environment=prod` |
| `OTEL_TRACES_EXPORTER` | 链路追踪导出器类型 | `otlp` |
| `OTEL_METRICS_EXPORTER` | 指标导出器类型 | `otlp` |
| `OTEL_LOGS_EXPORTER` | 日志导出器类型 | `otlp` |
| `OTEL_SERVICE_NAME` | 服务名称（简写） | `myapp` |
| `OTEL_TRACES_SAMPLER` | 采样器类型 | `parentbased_traceidratio` |
| `OTEL_TRACES_SAMPLER_ARG` | 采样器参数 | `0.1`（10%） |

### 关键资源属性

| 属性 | 用途 | 示例 |
|-----------|---------|---------|
| `service.name` | 服务标识 | `shoppingcart` |
| `service.namespace` | 关联相关服务 | `ecommerce` |
| `deployment.environment` | 环境层级 | `production`, `staging` |
| `service.version` | 应用版本 | `1.2.3` |

---

## 实用链接

- Grafana OTel文档：https://grafana.com/docs/opentelemetry/
- Grafana Cloud OTLP：https://grafana.com/docs/grafana-cloud/send-data/otlp/
- Grafana Java Agent：https://github.com/grafana/grafana-opentelemetry-java
- Grafana .NET SDK：https://github.com/grafana/grafana-opentelemetry-dotnet
- Grafana Alloy：https://grafana.com/docs/alloy/
- Grafana Beyla：https://grafana.com/docs/beyla/
- OTel Collector：https://opentelemetry.io/docs/collector/
- OTel Operator：https://opentelemetry.io/docs/kubernetes/operator/