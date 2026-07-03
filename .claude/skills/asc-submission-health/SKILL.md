---
name: asc-submission-health
description: 使用asc工具对App Store提交内容进行预检、提交构建版本并监控审核状态。适用于应用发布或排查审核提交问题时使用。
tags: asc-cli, app-store-submission, preflight-check, submission-monitoring, mobile-app-release
tags_cn: ASC命令行工具, App Store提交, 提交预检, 审核状态监控, 移动应用发布
---

# ASC提交健康检查

使用该技能可减少审核提交失败并监控状态。

## 前置条件
- 已配置认证信息，且已解析应用/版本/构建ID。
- 构建版本已处理完成（非处理中状态）。
- 所有必填元数据已完善。

## 提交前检查清单

### 1. 验证构建版本状态
```bash
asc builds info --build "BUILD_ID"
```
检查：
- `processingState` 为 `VALID`
- `usesNonExemptEncryption` - 若为`true`，则需要加密声明

### 2. 加密合规性
如果`usesNonExemptEncryption: true`：
```bash
# 列出现有声明
asc encryption declarations list --app "APP_ID"

# 如有需要，创建声明
asc encryption declarations create \
  --app "APP_ID" \
  --app-description "Uses standard HTTPS/TLS" \
  --contains-proprietary-cryptography=false \
  --contains-third-party-cryptography=true \
  --available-on-french-store=true

# 关联到构建版本
asc encryption declarations assign-builds \
  --id "DECLARATION_ID" \
  --build "BUILD_ID"
```

**更优方案：** 在Info.plist中添加`ITSAppUsesNonExemptEncryption = NO`并重新构建。

### 3. 内容权利声明
所有App Store提交均需完成：
```bash
# 检查当前状态
asc apps get --id "APP_ID" --output json | jq '.data.attributes.contentRightsDeclaration'

# 若未设置则进行配置
asc apps update --id "APP_ID" --content-rights "DOES_NOT_USE_THIRD_PARTY_CONTENT"
```
有效值：
- `DOES_NOT_USE_THIRD_PARTY_CONTENT`
- `USES_THIRD_PARTY_CONTENT`

### 4. 版本元数据
```bash
# 检查版本详情
asc versions get --version-id "VERSION_ID" --include-build

# 验证版权信息已设置
asc versions update --version-id "VERSION_ID" --copyright "2026 Your Company"
```

### 5. 本地化内容完善
```bash
# 列出版本本地化内容
asc localizations list --version "VERSION_ID"

# 检查必填字段：描述、关键词、更新说明、支持网址
```

### 6. 截图已上传
每个地区都需要对应目标平台的截图。

### 7. 应用信息本地化（隐私政策）
```bash
# 列出应用信息ID（若存在多个）
asc app-infos list --app "APP_ID"

# 检查隐私政策网址
asc localizations list --app "APP_ID" --type app-info --app-info "APP_INFO_ID"
```

## 提交版本

### 使用审核提交API（推荐）
```bash
# 创建提交请求
asc review submissions-create --app "APP_ID" --platform IOS

# 为提交请求添加版本
asc review items-add \
  --submission "SUBMISSION_ID" \
  --item-type appStoreVersions \
  --item-id "VERSION_ID"

# 提交审核
asc review submissions-submit --id "SUBMISSION_ID" --confirm
```

### 使用提交命令
```bash
asc submit create --app "APP_ID" --version "1.2.3" --build "BUILD_ID" --confirm
```
当存在多个平台时，使用`--platform`参数指定。

## 监控状态
```bash
# 检查提交状态
asc submit status --id "SUBMISSION_ID"
asc submit status --version-id "VERSION_ID"

# 列出所有提交请求
asc review submissions-list --app "APP_ID" --paginate
```

## 取消 / 重试
```bash
# 取消提交
asc submit cancel --id "SUBMISSION_ID" --confirm

# 或通过审核API取消
asc review submissions-cancel --id "SUBMISSION_ID" --confirm
```
修复问题后重新提交。

## 常见提交错误

### "Version is not in valid state"（版本状态无效）
检查：
1. 构建版本已关联且状态为VALID
2. 加密声明已通过（或符合豁免条件）
3. 内容权利声明已设置
4. 所有本地化内容已完善
5. 所有地区均已上传截图

### "Export compliance must be approved"（出口合规必须通过审核）
构建版本的`usesNonExemptEncryption: true`。可选择：
- 上传出口合规文档
- 或在Info.plist中添加`ITSAppUsesNonExemptEncryption = NO`后重新构建

### "Multiple app infos found"（找到多个应用信息）
使用`--app-info`参数指定正确的应用信息ID：
```bash
asc app-infos list --app "APP_ID"
```

## 注意事项
- `asc submit create`会自动使用新的reviewSubmissions API。
- 如需易读的状态信息，使用`--output table`参数。
- macOS提交遵循相同流程，但需使用`--platform MAC_OS`参数。