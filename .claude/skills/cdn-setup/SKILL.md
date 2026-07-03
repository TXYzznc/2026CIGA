---
name: cdn-setup
description: 配置CDN以实现内容分发。设置CloudFront、Cloudflare和Fastly。适用于优化全球内容分发场景。
license: MIT
metadata:
  author: devops-skills
  version: '1.0'
tags: cdn-setup, cloudfront-config, cloudflare-api, cache-optimization, content-delivery
tags_cn: CDN配置, CloudFront设置, Cloudflare API调用, 缓存优化, 内容分发
---

# CDN设置

配置内容分发网络。

## AWS CloudFront

```bash
aws cloudfront create-distribution --distribution-config '{
  "CallerReference": "my-distribution",
  "Origins": {
    "Quantity": 1,
    "Items": [{
      "Id": "myS3Origin",
      "DomainName": "mybucket.s3.amazonaws.com",
      "S3OriginConfig": {"OriginAccessIdentity": ""}
    }]
  },
  "DefaultCacheBehavior": {
    "TargetOriginId": "myS3Origin",
    "ViewerProtocolPolicy": "redirect-to-https",
    "CachePolicyId": "658327ea-f89d-4fab-a63d-7e88639e58f6"
  },
  "Enabled": true
}'
```

## Cloudflare

```bash
# 通过API
curl -X POST "https://api.cloudflare.com/client/v4/zones" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"name":"example.com","jump_start":true}'
```

## 缓存头

```nginx
location ~* \.(jpg|jpeg|png|gif|ico|css|js)$ {
    expires 30d;
    add_header Cache-Control "public, immutable";
}
```

## 最佳实践

- 设置合适的缓存头
- 谨慎使用缓存失效
- 实施缓存预热
- 监控缓存命中率