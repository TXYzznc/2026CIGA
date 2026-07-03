---
name: setup-fastlane
description: Fastlane 安装与首轮配置（iOS/macOS）。覆盖 Gemfile、Appfile/Matchfile、证书初始化、lane 模板、TestFlight 上传、App Store 提交。触发：Fastlane、setup、安装 Fastlane、iOS 自动化、证书管理、match、TestFlight。❌ 不适用：完整 CI 流水线，请用 mobile-cicd。
argument-hint:
- project-path
allowed-tools: Bash, Read, Write, Edit, Glob
tags: fastlane-setup, ios-automation, macos-app-automation, testflight-submission,
  app-store-deployment
tags_cn: Fastlane配置, iOS应用自动化, macOS应用自动化, TestFlight上传, App Store提交
---

## Fastlane 一次性配置

```
┌─────────────────────────────────────────────────────────────────┐
│  ONE-TIME SETUP                                                 │
│  ══════════════                                                 │
│  配置完成后，你将拥有以下功能：                                       │
│                                                                 │
│    fastlane ios test    → 运行测试                             │
│    fastlane ios beta    → 上传至TestFlight                  │
│    fastlane ios release → 提交至App Store                   │
│                                                                 │
│  每个项目只需配置一次，耗时约10分钟。                   │
└─────────────────────────────────────────────────────────────────┘
```

### 环境检查
- Xcode CLI: !`xcode-select -p 2>/dev/null && echo "✓" || echo "✗ 执行：xcode-select --install"`
- Homebrew: !`brew --version 2>/dev/null | head -1 || echo "✗ 执行：/bin/bash -c \"$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)\""`
- Fastlane: !`fastlane --version 2>/dev/null | grep -o "fastlane [0-9.]*" | head -1 || echo "✗ 执行：brew install fastlane"`

### 你的项目
- 项目文件: !`find . -maxdepth 2 -name "*.xcodeproj" 2>/dev/null | head -1 || echo "未找到"`
- 工作区文件: !`find . -maxdepth 2 -name "*.xcworkspace" ! -path "*/.build/*" ! -path "*/xcodeproj/*" 2>/dev/null | head -1 || echo "未找到"`
- Bundle ID: !`grep -r "PRODUCT_BUNDLE_IDENTIFIER" --include="*.pbxproj" . 2>/dev/null | head -1 | sed 's/.*= //' | tr -d '";' || echo "未找到"`
- 团队ID: !`grep -r "DEVELOPMENT_TEAM" --include="*.pbxproj" . 2>/dev/null | head -1 | sed 's/.*= //' | tr -d '";' || echo "未找到"`

### 目标路径: ${ARGUMENTS:-当前目录}

---

## 步骤1：安装Fastlane

```bash
brew install fastlane
```

> **为什么选择Homebrew？** Bundler 4.x 破坏了Fastlane的Ruby依赖，Homebrew可以避免所有版本冲突。

---

## 步骤2：创建配置文件

### `fastlane/Appfile`
```ruby
app_identifier("{{BUNDLE_ID}}")  # 你的Bundle ID
apple_id("{{APPLE_ID}}")         # 你的Apple ID邮箱
team_id("{{TEAM_ID}}")           # 你的团队ID
```

### `fastlane/Fastfile`
```ruby
default_platform(:ios)

platform :ios do
  desc "运行测试"
  lane :test do
    scan(scheme: "{{SCHEME}}")
  end

  desc "上传至TestFlight"
  lane :beta do
    increment_build_number
    gym(scheme: "{{SCHEME}}", export_method: "app-store")
    pilot(skip_waiting_for_build_processing: true)
  end

  desc "提交至App Store"
  lane :release do
    increment_build_number
    gym(scheme: "{{SCHEME}}", export_method: "app-store")
    deliver(submit_for_review: false, force: true)
  end
end
```

将`{{SCHEME}}`替换为你的应用Scheme名称（通常是应用名称）。

---

## 步骤3：配置元数据（可选）

下载你已有的App Store应用信息：
```bash
fastlane deliver download_metadata
fastlane deliver download_screenshots
```

这会创建`fastlane/metadata/`目录，其中包含可编辑的文本文件，用于存储应用描述、关键词等信息。

---

## 配置完成！

```bash
# 验证配置
fastlane lanes

# 运行第一条lane
fastlane ios test
```

### 快速参考
| 命令 | 功能 |
|---------|--------------|
| `fastlane ios test` | 运行测试 |
| `fastlane ios beta` | 构建并上传至TestFlight |
| `fastlane ios release` | 构建并提交至App Store |
| `fastlane deliver download_metadata` | 获取App Store应用信息 |

---

## 后续步骤

- **代码签名问题？** 请查看：“配置Match进行代码签名”
- **需要截图？** 请查看：“自动化生成App Store截图”
- **CI/CD配置？** 查看[Xcode Cloud 指南](../../docs/xcode-cloud.md)