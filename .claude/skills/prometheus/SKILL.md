---
name: prometheus
description: Prometheus指标与PromQL查询。适用于编写PromQL查询、创建记录或告警规则、调试指标采集问题，以及理解计数器/仪表盘/直方图的行为。
tags: prometheus-metrics, promql-queries, alerting-rules, recording-rules, metric-debugging
tags_cn: Prometheus指标, PromQL查询, 告警规则, 记录规则, 指标调试
---

# Prometheus

## PromQL常见陷阱

### 计数器函数（重点）
计数器只会递增。永远不要使用原始计数器值——务必使用速率函数：
```promql
rate(http_requests_total[5m])      # 每秒平均速率
irate(http_requests_total[5m])     # 瞬时速率（基于最后2个数据点，波动较大）
increase(http_requests_total[1h])  # 时间范围内的总增量
```
- `rate()`会自动处理计数器重置
- 仪表盘使用`rate()`，仅在需要高分辨率峰值监控时使用`irate()`

### 必须使用范围向量
速率函数需要指定`[时长]`：
```promql
rate(metric[5m])    # 正确写法
rate(metric)        # 错误：需要范围向量
```

### 向量匹配
二元运算要求标签匹配：
```promql
# 如果标签集不同，此查询会失败：
metric_a / metric_b

# 忽略额外标签：
metric_a / ignoring(extra_label) metric_b

# 仅基于特定标签匹配：
metric_a / on(common_label) metric_b
```

### 直方图分位数
```promql
histogram_quantile(0.95,
  sum(rate(http_request_duration_seconds_bucket[5m])) by (le)
)
```
- 必须使用带有`le`标签的`_bucket`指标
- 务必用`rate()`包裹计数器类型的指标
- `by (le)`是必填项；可按需添加其他标签：`by (le, endpoint)`

## 常见查询模式

```promql
# 错误率百分比
sum(rate(http_requests_total{status=~"5.."}[5m]))
  / sum(rate(http_requests_total[5m]))

# CPU使用率（node_exporter）
100 - avg by (instance) (rate(node_cpu_seconds_total{mode="idle"}[5m]) * 100)

# 内存使用率
1 - (node_memory_MemAvailable_bytes / node_memory_MemTotal_bytes)

# 容器内存（Kubernetes）
sum by (pod) (container_memory_working_set_bytes{container!=""})
```

## 告警规则

```yaml
groups:
  - name: example
    rules:
      - alert: HighErrorRate
        expr: |
          sum(rate(http_requests_total{status=~"5.."}[5m])) by (job)
            / sum(rate(http_requests_total[5m])) by (job)
            > 0.05
        for: 5m          # 必须持续触发该时长才会告警
        labels:
          severity: warning
        annotations:
          summary: "{{ $labels.job }}的错误率为{{ $value | humanizePercentage }}"
```

### for子句
- 避免短暂峰值导致的告警抖动
- 告警会先处于“pending”状态，直到满足持续时长
- 省略`for`则会立即触发告警

## 记录规则

预计算开销较大的查询：
```yaml
rules:
  - record: job:http_requests:rate5m
    expr: sum by (job) (rate(http_requests_total[5m]))
```

命名规范：`level:metric:operations`

## 数据过期

- 超过5分钟的样本会被标记为“过期”
- 仅当目标最近被采集过时，`up == 0`才会触发
- 使用`absent(metric)`来检测指标是否完全缺失
