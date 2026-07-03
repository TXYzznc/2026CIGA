---
name: mobile-device-testing
description: Unity 移动端真机测试矩阵设计与自动化执行，覆盖 BrowserStack / Firebase Test Lab / AWS Device Farm 选型与性能录制。触发：mobile device test、真机测试、BrowserStack、Firebase Test Lab、设备矩阵、Appium、压力测试
tags: mobile-test, browserstack, firebase-test-lab, appium, device-matrix
---

# 移动设备测试（Mobile Device Testing）

## 何时使用
- Unity 移动包发版前，需要在 20+ 真机上跑冒烟回归
- 线上反馈某机型闪退/卡顿，本地没有对应设备复现
- 接入 CI/CD，希望每次 nightly build 自动跑真机自动化
- 压测电池/温度/GPU 帧率，评估是否过热降频
- 上架审核被拒，需要复现 Apple/Google 测试环境

## 核心组件
- **BrowserStack App Live**：人工远程真机，按分钟计费，调试首选
- **BrowserStack App Automate**：Appium/Espresso/XCUITest 云端跑，支持并行
- **Firebase Test Lab**：Google 真机+虚拟机，Robo Test 无脚本爬虫极便宜
- **AWS Device Farm**：AWS 生态集成好，价格中等
- **本地设备农场**：自购 10-30 台主流机插 USB Hub，配 STF（OpenSTF）共享

## 选型矩阵

| 维度 | BrowserStack | Firebase Test Lab | AWS Device Farm |
|---|---|---|---|
| 真机数 | 3000+ | ~100 物理 + 虚拟 | ~200 |
| Unity 友好 | 高（GameAutomation 插件） | 中（需手工 Appium） | 中 |
| Robo 爬虫 | 无 | **有，零脚本** | 有 |
| 价格 | $$$ | $（最便宜） | $$ |
| 并行度 | 高 | 中 | 高 |
| 中国可用 | 部分 | 否（需 VPN） | 否 |
| 推荐 | 海外调试 | 海外 CI nightly | AWS 全家桶 |

## 关键流程

### 流程 A：设备矩阵设计（覆盖率优先）
按 **市场份额 + 性能分层 + OS 版本** 三维交叉。建议每分层至少 1 台：

| 分层 | iOS | Android |
|---|---|---|
| 高端旗舰 | iPhone 15 Pro / 14 Pro | Galaxy S24 / Pixel 8 Pro |
| 中端 | iPhone 13 / SE3 | Galaxy A54 / Xiaomi 13 |
| 低端 | iPhone 11 / XR | Redmi Note 12 / OPPO A78 |
| 老旧 OS | iOS 15 设备 | Android 8/9 (低 RAM) |
| 特殊 | iPad Air / Pro | 折叠屏 Galaxy Z Fold5 / 平板 |

参考数据：StatCounter + Firebase Analytics 自家 DAU 分布，覆盖率目标 **≥ 85% 用户**。

### 流程 B：Firebase Test Lab gcloud 命令
```bash
# Robo Test（无脚本自动爬 UI 30 分钟）
gcloud firebase test android run \
  --type robo \
  --app ./Builds/Android/mygame.apk \
  --device model=oriole,version=33,locale=zh,orientation=portrait \
  --device model=a54xq,version=34,locale=en,orientation=portrait \
  --timeout 30m \
  --results-bucket gs://my-bucket \
  --robo-directives "click:com.mygame:btn_start=,text:com.mygame:edit_name=TestBot"

# Instrumentation Test（Appium / Espresso）
gcloud firebase test android run \
  --type instrumentation \
  --app app-release.apk \
  --test app-androidTest.apk \
  --device model=oriole,version=33 \
  --environment-variables coverage=true,coverageFile=/sdcard/coverage.ec

# iOS XCUITest
gcloud firebase test ios run \
  --test MyGameTests.zip \
  --device model=iphone14pro,version=17.4
```

### 流程 C：Appium + Unity 接入
Unity 端开启 **Accessibility**：`Player Settings > Resolution > Hide Toolbar = false`，并给关键 UI 设 `accessibilityIdentifier`（用 `AltUnityTester` 或 `AltTester` 插件，给 GameObject 加 tag 暴露给 Appium）。

```python
# pytest + Appium 示例
from appium import webdriver
caps = {
    "platformName": "Android",
    "appium:app": "/builds/mygame.apk",
    "appium:automationName": "UiAutomator2",
    "appium:autoGrantPermissions": True,
}
driver = webdriver.Remote("http://hub:4723", caps)
driver.find_element("accessibility id", "btn_start").click()
```

### 流程 D：性能录制
- **GPU/CPU/帧率**：Firebase Test Lab 自动返回 `video.mp4` + `performance.json`（含 FPS、CPU%、Mem MB）
- **电池**：Android 用 `dumpsys batterystats`；iOS 走 `xcrun simctl` 或 Xcode Energy Log
- **温度**：仅 Android `dumpsys thermalservice`，iOS 走私有 API（线下设备实测）
- **网络节流**：BrowserStack 内置 4G/3G/2G profile；本地用 Android `tc qdisc` 或 Charles 限速

### 流程 E：权限与系统弹窗
Unity 包首启常被 Permission Dialog 卡住，自动化必须处理：
- Android：`autoGrantPermissions=true`（仅 manifest 声明的）
- iOS：XCUITest 用 `springboardAlertsConfig` 自动点 Allow
- Robo Test：用 `--robo-directives` 注入点击指令

## 常见坑
- **OpenGL ES 3 在低端机黑屏**：FTL 部分虚拟机仅支持 ES2，矩阵显式排除虚拟机
- **Mali GPU 上 Shader 编译慢导致首启 ANR**：必须真机跑，不能纯模拟器
- **Robo 卡在登录页**：用 `--robo-directives text:` 提供测试账号；或设计 dev build 跳过登录
- **iOS 上 Metal validation 仅 debug 报错**：自动化必须用 Release 构建复现
- **设备温度被并发任务污染**：性能压测必须设备冷却 5 分钟再开始；FTL 不保证冷启动
- **录屏文件巨大**：FTL 30min 视频 800MB+，必须设 `--directories-to-pull` 精确捞需要的
- **国内设备不在矩阵**：华为/Vivo/OPPO 国内特化 ROM，海外平台没有，需自建本地设备
- **AltTester 拖累性能 30%**：Release 包务必关闭 AltTester 编译宏

## 模板/产物

### 设备矩阵 YAML（CI 用）
```yaml
android:
  - { model: oriole, version: 34 }     # Pixel 6
  - { model: a54xq, version: 34 }      # Galaxy A54
  - { model: redfin, version: 30 }     # Pixel 5
  - { model: hwALP, version: 28 }      # Huawei
ios:
  - { model: iphone15pro, version: 17.4 }
  - { model: iphone13, version: 16.6 }
  - { model: iphonese3rd, version: 15.7 }
```

### 真机测试 checklist
- [ ] 首启耗时 < 3s（中端机）/ < 5s（低端机）
- [ ] 30 分钟连玩温度 < 42°C
- [ ] 内存峰值 < 1.5GB（Android 2GB RAM 机型）
- [ ] 后台 10min 回前台不闪退
- [ ] 切换网络 WiFi → 4G → 飞行模式不卡死
- [ ] 来电/通知打断后能恢复
- [ ] 横竖屏切换 UI 不错位
- [ ] 推送权限弹窗、相册权限弹窗能处理
