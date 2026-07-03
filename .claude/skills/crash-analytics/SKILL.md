---
name: crash-analytics
description: Unity 移动端崩溃监控全流程，Sentry/Crashlytics 选型、符号上传、Native crash 解析、ANR 检测。触发：crash、崩溃、Sentry、Crashlytics、symbol upload、ANR、stack trace 解析、移动端崩溃监控
tags: crash, sentry, crashlytics, il2cpp, anr, symbol-upload
---

# Unity 崩溃监控（Crash Analytics）

## 何时使用
- Unity 移动端项目准备接入崩溃监控 SDK
- IL2CPP 出包后线上崩溃 stack trace 全是地址、无法定位
- Android NDK / iOS Native 崩溃需要符号化
- ANR / 卡死率高，需要主线程 watchdog
- 同一崩溃在面板上分裂成几十个组，需要分组去重

## 核心组件
- **Sentry Unity SDK**：Unity 一等公民，自动捕获 C# Exception + Native，支持 Performance/Replay，自建可选
- **Firebase Crashlytics**：Android/iOS 主流，免费且与 Firebase 全家桶联动，但 Unity 体验弱于 Sentry
- **Sentry CLI**：`sentry-cli` 用于符号上传与 source map 关联
- **Bugly / 友盟 U-APM**：国内合规备选，海外项目不推荐
- **符号文件**：`il2cpp_symbols.zip`、`lineNumberMappings.json`、Android `.so` + `symbols.zip`、iOS `.dSYM`

## 选型矩阵

| 维度 | Sentry Unity | Firebase Crashlytics |
|---|---|---|
| Unity 支持度 | 一等公民，有官方 SDK | 通过 Firebase Unity SDK，二等 |
| C# Exception | 自动捕获 | 需手工 LogException |
| IL2CPP 符号化 | sentry-cli 一键 | 需手工传 NDK so |
| ANR 检测 | 支持 | 支持 |
| 自建/合规 | 支持自建（OSS） | 仅 SaaS |
| 价格 | 按 event 计费 | 免费 |
| 推荐场景 | 海外 + 重 Unity 项目 | 已用 Firebase 全家桶 |

## 关键流程

### 流程 A：Sentry Unity 初始化
```csharp
// Assets/Plugins/Sentry/SentryBootstrap.cs（runtime 配置）
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
static void Init() {
    SentrySdk.ConfigureScope(scope => {
        scope.User = new User { Id = PlayerPrefs.GetString("uid") };
        scope.SetTag("build_type", Debug.isDebugBuild ? "dev" : "release");
        scope.SetTag("device_tier", DeviceTier.Get()); // low/mid/high
    });
    SentrySdk.AddBreadcrumb("Game launched", "lifecycle");
}
```

Sentry Editor Config (`Tools > Sentry`)：勾选 **Auto Upload Symbols**、**IL2CPP Line Numbers**、**Capture In Editor = false**。

### 流程 B：IL2CPP 符号上传（Android）
```bash
# 构建后 Library/Bee/artifacts 下生成
sentry-cli upload-dif \
  --org my-org --project unity-mobile \
  --include-sources \
  ./Builds/Android/symbols.zip \
  ./Library/il2cpp_android_arm64/il2cpp_symbols/

# lineNumberMappings.json（C# → C++ 行号映射）
sentry-cli upload-il2cpp-mappings \
  --org my-org --project unity-mobile \
  ./Library/Il2cppBuildCache/Android/arm64/lineNumberMappings.json
```

### 流程 C：iOS dSYM 上传
```bash
# Xcode Archive 后
sentry-cli upload-dif --org my-org --project ios-game \
  ~/Library/Developer/Xcode/Archives/2026-06-23/MyGame.xcarchive/dSYMs/

# Firebase 等价
./Pods/FirebaseCrashlytics/upload-symbols -gsp GoogleService-Info.plist \
  -p ios MyGame.app.dSYM
```

### 流程 D：Crashlytics Android 配置（gradle）
```gradle
// launcherTemplate.gradle
apply plugin: 'com.google.gms.google-services'
apply plugin: 'com.google.firebase.crashlytics'

android {
    buildTypes {
        release {
            firebaseCrashlytics {
                nativeSymbolUploadEnabled true
                unstrippedNativeLibsDir 'src/main/jniLibs'
            }
        }
    }
}
// 构建后自动跑 :app:uploadCrashlyticsSymbolFileRelease
```

### 流程 E：ANR 检测（Unity 主线程 watchdog）
```csharp
// 自实现简版：每 5s 心跳，子线程检测超时
Task.Run(async () => {
    while (true) {
        long last = _lastTick;
        await Task.Delay(5000);
        if (System.Environment.TickCount - last > 5000) {
            SentrySdk.CaptureMessage("ANR suspect", SentryLevel.Warning);
        }
    }
});
```

### 流程 F：Breadcrumb 与 User Context
- 场景切换、关键 UI 点击、网络请求成功/失败都打面包屑
- 限制 100 条以内，避免 payload 膨胀
- User Context：uid、vip 等级、当前关卡，**禁止上传手机号/邮箱原文**（GDPR）

## 常见坑
- **stack trace 全是 0x 地址**：忘上传 IL2CPP 符号；检查 `il2cpp_symbols/` 是否包含 `.sym.so`
- **lineNumberMappings 缺失**：Player Settings > **Other > Stack Trace** 勾 Full；只有 Release+IL2CPP 才生成
- **同 bug 分裂百组**：grouping 默认按顶帧地址，需配置 Sentry **fingerprint rules** 按异常类型聚合
- **Crashlytics 上不去符号**：Gradle `nativeSymbolUploadEnabled` 必须配合 `unstrippedNativeLibsDir` 指向 unstripped 的 `libil2cpp.so`
- **ANR 漏报**：仅 Android 8+ 系统级 ANR 才会被 SDK 抓到，<8 需自实现 watchdog
- **告警风暴**：单次更新崩溃率飙到 5%+ 会刷屏，配置 **alert rule = 用户数 > 100 且 crash-free < 99%**
- **PII 泄漏**：Sentry 默认会抓 IP 与设备名，海外项目走 GDPR 必须关 `SendDefaultPii=false`

## 模板/产物

### sentry.options.json
```json
{
  "Dsn": "https://xxx@o123.ingest.sentry.io/456",
  "TracesSampleRate": 0.1,
  "AttachStacktrace": true,
  "MaxBreadcrumbs": 100,
  "Environment": "prod",
  "Release": "mygame@1.4.2+8821",
  "SendDefaultPii": false,
  "Il2CppLineNumbers": true
}
```

### 告警阈值建议
| 指标 | Warning | Critical |
|---|---|---|
| Crash-free users | < 99.5% | < 99% |
| Crash-free sessions | < 99.8% | < 99.5% |
| ANR rate | > 0.47% | > 1% |
| 单 issue 24h 新增用户 | > 100 | > 500 |

### CI 上传脚本（GitHub Actions）
```yaml
- name: Upload Symbols
  env:
    SENTRY_AUTH_TOKEN: ${{ secrets.SENTRY_TOKEN }}
  run: |
    sentry-cli upload-dif --org $ORG --project $PROJ \
      --include-sources ./Builds/symbols/
```
