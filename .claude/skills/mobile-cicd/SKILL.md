---
name: mobile-cicd
description: Fastlane、GitHub Actions 移动开发工具集，涵盖代码签名（iOS 配置文件、Android 密钥库）、Beta 版本分发（TestFlight、Firebase
  App Distribution）以及应用商店提交。适用于搭建移动应用构建流水线、自动化版本发布或管理签名流程。
tags: mobile-cicd, fastlane, github-actions, code-signing, app-distribution
tags_cn: 移动应用CI/CD, Fastlane使用, GitHub Actions配置, 代码签名, 应用分发
---

# 移动应用CI/CD技能

为iOS和Android应用提供自动化构建、测试、签名与部署流水线。

---

## Fastlane

### iOS Fastfile

```ruby
# ios/fastlane/Fastfile
default_platform(:ios)

platform :ios do
  desc "Run tests"
  lane :test do
    run_tests(scheme: "MyApp")
  end

  desc "Build and upload to TestFlight"
  lane :beta do
    increment_build_number(xcodeproj: "MyApp.xcodeproj")
    match(type: "appstore", readonly: true)
    build_app(scheme: "MyApp", export_method: "app-store")
    upload_to_testflight(skip_waiting_for_build_processing: true)
    slack(message: "New iOS beta uploaded to TestFlight")
  end

  desc "Deploy to App Store"
  lane :release do
    match(type: "appstore", readonly: true)
    build_app(scheme: "MyApp", export_method: "app-store")
    upload_to_app_store(
      submit_for_review: true,
      automatic_release: false,
      force: true
    )
  end
end
```

### Android Fastfile

```ruby
# android/fastlane/Fastfile
default_platform(:android)

platform :android do
  desc "Run tests"
  lane :test do
    gradle(task: "test")
  end

  desc "Build and upload to Play Store internal track"
  lane :beta do
    gradle(task: "bundleRelease")
    upload_to_play_store(
      track: "internal",
      aab: "app/build/outputs/bundle/release/app-release.aab"
    )
  end

  desc "Promote to production"
  lane :release do
    upload_to_play_store(
      track: "internal",
      track_promote_to: "production",
      rollout: "0.1" # 10% rollout
    )
  end
end
```

---

## 代码签名

### iOS (match)

```ruby
# Matchfile
git_url("https://github.com/org/certificates.git")
storage_mode("git")
type("appstore")
app_identifier("com.example.myapp")

# Usage
match(type: "development")  # Dev certificates
match(type: "appstore")     # Distribution certificates
```

### Android (Keystore)

```bash
# Generate keystore (once)
keytool -genkey -v -keystore release.keystore \
  -alias my-key-alias -keyalg RSA -keysize 2048 -validity 10000

# Store securely - NEVER commit to git
# Use CI secrets or encrypted storage
```

```groovy
// android/app/build.gradle
android {
    signingConfigs {
        release {
            storeFile file(System.getenv("KEYSTORE_PATH") ?: "release.keystore")
            storePassword System.getenv("KEYSTORE_PASSWORD")
            keyAlias System.getenv("KEY_ALIAS")
            keyPassword System.getenv("KEY_PASSWORD")
        }
    }
}
```

---

## GitHub Actions

### React Native / Expo

```yaml
# .github/workflows/mobile-ci.yml
name: Mobile CI/CD
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: '20' }
      - run: npm ci
      - run: npm test
      - run: npx tsc --noEmit

  build-ios:
    needs: test
    runs-on: macos-latest
    if: github.ref == 'refs/heads/main'
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: '20' }
      - uses: ruby/setup-ruby@v1
        with: { ruby-version: '3.2' }
      - run: npm ci
      - run: cd ios && bundle install && bundle exec fastlane beta
        env:
          MATCH_PASSWORD: ${{ secrets.MATCH_PASSWORD }}
          FASTLANE_APPLE_APPLICATION_SPECIFIC_PASSWORD: ${{ secrets.APPLE_APP_PASSWORD }}

  build-android:
    needs: test
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-java@v4
        with: { java-version: '17', distribution: 'temurin' }
      - uses: actions/setup-node@v4
        with: { node-version: '20' }
      - run: npm ci
      - run: cd android && bundle install && bundle exec fastlane beta
        env:
          KEYSTORE_PASSWORD: ${{ secrets.KEYSTORE_PASSWORD }}
          KEY_PASSWORD: ${{ secrets.KEY_PASSWORD }}
```

---

## EAS Build (Expo)

```json
// eas.json
{
  "cli": { "version": ">= 12.0.0" },
  "build": {
    "development": {
      "developmentClient": true,
      "distribution": "internal"
    },
    "preview": {
      "distribution": "internal",
      "ios": { "simulator": false },
      "channel": "preview"
    },
    "production": {
      "channel": "production",
      "autoIncrement": true
    }
  },
  "submit": {
    "production": {
      "ios": { "appleId": "your@email.com", "ascAppId": "1234567890" },
      "android": { "serviceAccountKeyPath": "./google-sa-key.json" }
    }
  }
}
```

```bash
# Build for preview
eas build --profile preview --platform all

# Build for production
eas build --profile production --platform all

# Submit to stores
eas submit --platform ios --latest
eas submit --platform android --latest

# OTA update (no store review)
eas update --branch production --message "Bug fix"
```

---

## Beta版本分发

| 平台 | iOS | Android |
|----------|-----|---------|
| 官方原生渠道 | TestFlight | 应用商店内部测试轨道 |
| Firebase | App Distribution | App Distribution |
| Expo | EAS 内部分发 | EAS 内部分发 |

---

## 应用商店优化（ASO）

- 标题：主关键词 + 品牌名称（iOS 限30字符，Android 限50字符）
- 副标题/短描述：次要关键词
- 截图：展示核心功能，使用设备框架
- 关键词字段（仅iOS）：逗号分隔，无空格
- 每次版本更新时撰写发布说明
- 回复用户评价（提升排名）

---

## 发布检查清单

- [ ] CI上所有测试通过
- [ ] 版本号已更新
- [ ] 构建号已递增
- [ ] 发布说明已撰写
- [ ] 截图已更新（若UI有变更）
- [ ] QA团队已完成Beta测试
- [ ] 代码签名有效且近期不会过期
- [ ] 隐私政策链接为最新版本
- [ ] 已确认符合应用商店审核指南

---

## 相关资源

- `~/.claude/skills/react-native/SKILL.md` - React Native开发
- `~/.claude/skills/flutter-development/SKILL.md` - Flutter开发
- `~/.claude/agents/mobile-release-manager.md` - 版本发布管理Agent
- `~/.claude/docs/reference/workflows/deployment-cicd.md` - 通用CI/CD模式

---

_自动化所有流程。安全签名。放心发布。_