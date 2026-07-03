---
name: unity-build-pipeline
description: Unity 6 LTS 命令行打包流水线，覆盖 BuildPipeline API、batchmode、License v2 激活、GameCI/unity-builder GitHub Actions 工作流，触发 Unity build、BuildPipeline、batchmode、GameCI、unity-builder、Unity Cloud Build、命令行打包
tags: unity, cicd, build, gameci, batchmode
---

# Unity 命令行打包流水线 (Unity 6 LTS)

## 何时使用
- 需要在 CI（GitHub Actions / GitLab CI / Jenkins）里 headless 出包
- 多平台批量出包（Win / Mac / Linux / Android / iOS / WebGL）
- 需要在出包阶段注入版本号、Build Number、预处理脚本符号
- 想从 Unity Cloud Build 迁移到自建 GameCI/unity-builder workflow
- 排查 batchmode 卡死、License v2 激活失败、IL2CPP 报错

## 核心组件
- `UnityEditor.BuildPipeline.BuildPlayer(BuildPlayerOptions)` —— 主入口
- 命令行参数：`-batchmode -nographics -quit -projectPath -executeMethod -logFile`
- Unity License v2：`Unity.Licensing.Client` / `unity-licensing-client` CLI（替代 v1 的 ULF 激活）
- `PlayerSettings.SetScriptingDefineSymbolsForGroup` —— 预定义符号注入
- `PlayerSettings.SetScriptingBackend` —— IL2CPP / Mono 切换
- `EditorUserBuildSettings.SwitchActiveBuildTarget` —— 切平台
- GameCI 镜像：`unityci/editor:ubuntu-6000.0.XXf1-<target>-3.1.0`
- GitHub Action：`game-ci/unity-builder@v4`

## 关键流程

### 流程 A：编写 Editor 出包脚本
1. 在 `Assets/Editor/BuildScript.cs` 写静态方法（必须 `public static`，无参或读取 CommandLineArgs）。
2. 从 `System.Environment.GetCommandLineArgs()` 读自定义参数（如 `-buildVersion 1.2.3`）。
3. 配置 `BuildPlayerOptions`：scenes / locationPathName / target / options。
4. 调用 `BuildPipeline.BuildPlayer`，检查 `BuildResult`，失败时 `EditorApplication.Exit(1)`。

### 流程 B：本地命令行触发
```bash
"C:/Program Files/Unity/Hub/Editor/6000.0.32f1/Editor/Unity.exe" \
  -batchmode -nographics -quit \
  -projectPath "D:/unity/UnityProject/GameDesinger" \
  -executeMethod BuildScript.BuildWindows \
  -buildVersion 1.2.3 \
  -logFile "./build.log"
```
- 退出码 0 = 成功；非 0 必须排查 log 末尾 100 行。
- `-nographics` 仅 Linux 必加；Windows/Mac 加了反而会让 UI shader 编译失败。

### 流程 C：Unity License v2 激活（CI）
1. 本地用 Unity Hub 登录账号，生成 `Unity_lic.ulf`（Personal）或获取 Serial（Plus/Pro）。
2. GitHub Secrets 注入：`UNITY_LICENSE`（ulf 文件全文）/ `UNITY_EMAIL` / `UNITY_PASSWORD` / `UNITY_SERIAL`。
3. Personal 走 `UNITY_LICENSE`；Plus/Pro 走 `UNITY_SERIAL`（GameCI 自动判别）。
4. 退出时 GameCI 自动 return-license，避免占座。

### 流程 D：GameCI GitHub Actions Workflow
见下方 yaml。注意 runner 必须 `ubuntu-latest`（free tier）或自建 large runner（IL2CPP iOS 必须 macOS runner）。

### 流程 E：多平台 BuildTarget
- `BuildTarget.StandaloneWindows64` → `.exe`
- `BuildTarget.StandaloneOSX` → `.app`
- `BuildTarget.StandaloneLinux64` → 无后缀可执行
- `BuildTarget.Android` → `.apk` / `.aab`（`EditorUserBuildSettings.buildAppBundle = true`）
- `BuildTarget.iOS` → Xcode 工程目录（需二次 `xcodebuild archive`）
- `BuildTarget.WebGL` → `Build/` + `index.html`

## 常见坑
- **坑 1**：batchmode 下 `Debug.Log` 不刷盘 → 用 `-logFile -` 输出到 stdout，或显式 `Console.Out.Flush()`。
- **坑 2**：License v2 在 Linux runner 报 `No valid Unity Editor license found` → 检查 ulf 是否含 BOM；Secret 应用 base64 编码后再解码。
- **坑 3**：IL2CPP Android 出包卡在 `il2cpp.exe` → 确认安装了对应 Android NDK r23b（Unity 6 默认），且 `ANDROID_NDK_ROOT` 环境变量正确。
- **坑 4**：`SwitchActiveBuildTarget` 异步，立即 BuildPlayer 会用旧平台资源 → 改用 `BuildPlayerOptions.target` 直接指定，无需切平台。
- **坑 5**：`PlayerSettings.bundleVersion` 修改后没生效 → 必须 `AssetDatabase.SaveAssets()` 后再 BuildPlayer。
- **坑 6**：GameCI 镜像没有 `-android` / `-ios` 后缀 → 拉到的是 base 镜像，缺少 Android Module，出包必失败。
- **坑 7**：iOS Build 在 macOS runner 上 Xcode 版本不匹配 → workflow 里加 `xcode-select -s /Applications/Xcode_15.4.app`。

## 配置示例

### Editor 出包脚本（Assets/Editor/BuildScript.cs）
```csharp
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildScript
{
    static string GetArg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }

    [MenuItem("Build/Windows64")]
    public static void BuildWindows()
    {
        var version = GetArg("-buildVersion") ?? "0.0.1";
        PlayerSettings.bundleVersion = version;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetScriptingDefineSymbolsForGroup(
            BuildTargetGroup.Standalone, "RELEASE;ANALYTICS_ON");

        var opts = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = $"Builds/Win64/SampleProject-{version}.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(opts);
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"Build failed: {report.summary.result}");
            EditorApplication.Exit(1);
        }
        Debug.Log($"Build OK: {report.summary.totalSize} bytes");
    }

    public static void BuildAndroid()
    {
        var version = GetArg("-buildVersion") ?? "0.0.1";
        var versionCode = int.Parse(GetArg("-versionCode") ?? "1");
        PlayerSettings.bundleVersion = version;
        PlayerSettings.Android.bundleVersionCode = versionCode;
        EditorUserBuildSettings.buildAppBundle = true; // 出 .aab
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        var opts = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = $"Builds/Android/SampleProject-{version}.aab",
            target = BuildTarget.Android,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(opts);
        if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
```

### GameCI GitHub Actions Workflow（.github/workflows/build.yml）
```yaml
name: Unity Build
on:
  push:
    tags: ['v*']
  workflow_dispatch:

jobs:
  build:
    name: Build ${{ matrix.targetPlatform }}
    runs-on: ${{ matrix.os }}
    strategy:
      fail-fast: false
      matrix:
        include:
          - os: ubuntu-latest
            targetPlatform: StandaloneWindows64
          - os: ubuntu-latest
            targetPlatform: StandaloneLinux64
          - os: ubuntu-latest
            targetPlatform: Android
          - os: macos-14
            targetPlatform: iOS
          - os: macos-14
            targetPlatform: StandaloneOSX
    steps:
      - uses: actions/checkout@v4
        with: { lfs: true }

      - uses: actions/cache@v4
        with:
          path: Library
          key: Library-${{ matrix.targetPlatform }}-${{ hashFiles('Assets/**', 'Packages/**', 'ProjectSettings/**') }}
          restore-keys: |
            Library-${{ matrix.targetPlatform }}-
            Library-

      - uses: game-ci/unity-builder@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
          UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
          UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
          UNITY_SERIAL: ${{ secrets.UNITY_SERIAL }}
        with:
          unityVersion: 6000.0.32f1
          targetPlatform: ${{ matrix.targetPlatform }}
          buildMethod: BuildScript.Build${{ matrix.targetPlatform }}
          customParameters: -buildVersion ${{ github.ref_name }} -versionCode ${{ github.run_number }}
          allowDirtyBuild: true

      - uses: actions/upload-artifact@v4
        with:
          name: Build-${{ matrix.targetPlatform }}
          path: build/${{ matrix.targetPlatform }}
          retention-days: 14
```

### License v2 本地激活（CLI 方式）
```bash
# Personal license
/opt/unity/Editor/Data/Resources/Licensing/Client/Unity.Licensing.Client \
  --activate-ulf --license-file Unity_lic.ulf

# Pro/Plus
unity-licensing-client --activate --serial XX-XXXX-XXXX-XXXX-XXXX-XXXX \
  --username "$UNITY_EMAIL" --password "$UNITY_PASSWORD"

# 用完归还（CI 必须）
unity-licensing-client --return-ulf
```
