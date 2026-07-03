---
name: k6
description: k6 负载测试与性能压测。覆盖 HTTP/WebSocket/gRPC 脚本、阶梯/峰值/spike 场景、阈值断言、Prometheus/Grafana 集成。触发：k6、负载测试、压测、load test、performance test、stress test、benchmark、API 性能、QPS、TPS。
tags: k6, load-testing, performance-testing, ci-cd-integration, api-testing
tags_cn: k6负载测试, 性能测试, CI/CD集成, API测试, 测试最佳实践
---

# k6

k6是一款以开发者为中心的开源负载测试工具，适用于测试API、微服务和网站。它由Go语言编写，但你可以使用JavaScript编写测试脚本。

## 适用场景

- **API负载测试**：现代API性能测试的黄金标准。
- **CI/CD集成**：体积极轻的二进制文件（或Docker镜像），轻松设置准入条件（如“若p95延迟超过500ms则测试失败”）。
- **开发者友好**：使用JS（ES6）编写脚本，后端/前端开发者均可编写测试用例。

## 快速开始

```javascript
import http from "k6/http";
import { sleep, check } from "k6";

export const options = {
  vus: 10,
  duration: "30s",
};

export default function () {
  const res = http.get("http://test.k6.io");
  check(res, {
    "status was 200": (r) => r.status == 200,
  });
  sleep(1);
}
```

运行命令：`k6 run script.js`。

## 核心概念

### 虚拟用户（VUs）

模拟用户循环运行你的测试脚本。它们是并发的，但并非基于浏览器（除非使用xk6-browser），因此CPU效率极高。

### 检查与阈值

- **检查（Check）**：布尔断言（类似断言语句）。不会导致测试失败，仅在测试结束时报告通过率/失败率。
- **阈值（Threshold）**：用于CI流水线的通过/失败判定标准。

```javascript
export const options = {
  thresholds: {
    http_req_duration: ["p(95)<500"], // 95%的请求必须在500ms内完成
  },
};
```

## 2025年最佳实践

**建议做法**：

- **模块化**：将逻辑拆分为多个文件夹。k6支持ES模块（`import { ... } from './utils.js'`）。
- **使用场景（Scenarios）**：在单个测试中混合不同模式（如逐步加压、恒定到达率）。
- **关联特定数据**：确保测试并非仅命中缓存。使用动态数据（如随机ID）。

**避免做法**：

- **不要将其当作浏览器使用**：标准k6的`http`模块不会解析HTML或执行页面中的JS，仅会请求端点。如果确实需要浏览器渲染，请使用`k6-browser`模块（但资源消耗更高）。

## 参考资料

- [k6官方文档](https://k6.io/docs/)