---
name: addressables-hotfix
description: Unity Addressables 远程热更新方案，覆盖 Catalog Hash、Remote Build/Load Path、Content Update Workflow、CDN 上传、离线降级，触发 Addressables、热更、hotfix、catalog、bundle 更新、CDN 资产、Unity OTA、Content Update
tags: unity, addressables, hotfix, cdn, content-update
---

# Addressables 远程热更（Unity 6 LTS）

## 何时使用
- 发版后不重新提包，只想推新美术 / 配置 / Prefab
- 多包合并发布（基础包 + DLC 包按 Group 区分）
- 修复线上 Bug 的 ScriptableObject 数据
- 玩家首次进游戏需要下载远程资源（OTA）
- 排查 catalog 加载失败、bundle 404、hash 不匹配

## 核心组件
- **AddressableAssetSettings**（`Assets/AddressableAssetsData/AddressableAssetSettings.asset`）
- **Profiles**：定义变量 `RemoteBuildPath` / `RemoteLoadPath` / `BuildTarget`
- **Catalog**：`catalog_<version>.json` + `catalog_<version>.hash`（hash 变化触发更新）
- **Bundle**：`<group>_<hash>.bundle`（hash 文件名 = 内容寻址，CDN 强缓存）
- **Content Update Workflow**：
  - `Check for Content Update Restrictions` → 找出"Cannot Change Post Release"组里的修改资产
  - `Update Pre-existing Build` → 生成增量 catalog + 新 bundle
- **API**：
  - `Addressables.InitializeAsync()` —— 加载本地 catalog
  - `Addressables.CheckForCatalogUpdates()` —— 查远端 hash
  - `Addressables.UpdateCatalogs()` —— 下载新 catalog
  - `Addressables.DownloadDependenciesAsync(label)` —— 预下载
  - `Addressables.GetDownloadSizeAsync(key)` —— 量化下载量
- **CDN**：CloudFront（S3 origin）/ Cloudflare R2（零出口费）/ 七牛 / 阿里 OSS

## 关键流程

### 流程 A：Addressables Settings 配置（首次）
打开 `Window → Asset Management → Addressables → Groups`，再点 `Tools → Profiles`：

```
Profile: Production
├─ Local.BuildPath  = [UnityEngine.AddressableAssets.Addressables.BuildPath]
├─ Local.LoadPath   = {UnityEngine.AddressableAssets.Addressables.RuntimePath}
├─ Remote.BuildPath = ServerData/[BuildTarget]
├─ Remote.LoadPath  = https://cdn.example.com/{BuildVersion}/[BuildTarget]
└─ BuildVersion     = 1.2.3        # 每次发版手动改 / CI 注入
```

Group 配置（每个 Group 的 Inspector）：
- **Build & Load Paths**: `Remote`（首发资源放 Remote，安装包仅含 catalog）
- **Bundle Mode**: `Pack Together`（小组）/ `Pack Separately`（大文件单独）
- **Use Asset Bundle Cache**: ✅
- **Use UnityWebRequest for Local Asset Bundles**: ❌
- **Bundle Naming**: `Filename Hash`（CDN 强缓存友好）
- **Content Update Restriction**:
  - `Can Change Post Release` —— 配置 / 文本 / 调参用
  - `Cannot Change Post Release` —— Prefab 引用、shader（改了会触发 Update Workflow）

### 流程 B：构建 + 上传 CDN
1. `Groups` 窗口 → `Build → New Build → Default Build Script`。
2. 产物在 `ServerData/<BuildTarget>/`，含 `catalog_<ver>.json` + `catalog_<ver>.hash` + 多个 `.bundle`。
3. **保留 `addressables_content_state.bin`** —— Content Update 必须用这个文件做 diff，**提交到 git**。
4. 上传到 CDN（同步整个目录）：
   ```bash
   aws s3 sync ServerData/StandaloneWindows64/ s3://my-bucket/1.2.3/StandaloneWindows64/ \
     --cache-control "public, max-age=31536000, immutable" \
     --exclude "catalog_*.hash"
   aws s3 cp ServerData/StandaloneWindows64/catalog_*.hash s3://my-bucket/1.2.3/StandaloneWindows64/ \
     --cache-control "public, max-age=60"
   ```
   - `.bundle` / `catalog_*.json` 强缓存（文件名含 hash，安全）
   - `catalog_*.hash` 短缓存（60s）—— 客户端就靠它发现更新

### 流程 C：Content Update（仅推增量）
1. 在已有 build 基础上修改资源。
2. `Groups → Tools → Check for Content Update Restrictions`：
   - 若改动只在 "Can Change" 组 → 无弹窗，直接走 step 3。
   - 若改动了 "Cannot Change" 组 → 弹窗让你把这些资产挪到一个新 Group（Static Content）。
3. `Tools → Prepare for Content Update`，选 `addressables_content_state.bin`。
4. `Build → Update a Previous Build`，选同一个 `.bin` 文件。
5. 产出**只包含变化的 bundle** + **新 catalog**（catalog hash 必变）。
6. 增量 sync 到 CDN（注意不要 `--delete`，旧 bundle 还要给老客户端用）。

### 流程 D：运行时检查 + 下载
见下方代码示例。关键：
1. App 启动 `InitializeAsync()`。
2. `CheckForCatalogUpdates()` 拿 catalog id list。
3. 有更新 → `UpdateCatalogs()`。
4. `GetDownloadSizeAsync(label)` 估量 → 超过 WiFi-only 阈值就弹窗确认。
5. `DownloadDependenciesAsync(label, true)`（第二参数 autoRelease=true）。
6. 失败重试 + 离线降级：catch 异常后退回上一个本地 catalog，UI 提示"离线模式"。

### 流程 E：CDN 选型
- **CloudFront**：出口费贵（$0.085/GB），但 PoP 多、稳定。配 S3 origin + OAC。
- **Cloudflare R2**：出口免费，适合大型 OTA。需挂自定义域名 + R2 public bucket policy。
- **七牛 / 阿里 OSS**：国内首选，但要 ICP 备案。
- 多 CDN 失败转移：`ResourceManager.InternalIdTransformFunc` 根据 retry 次数切换域名。

## 常见坑
- **坑 1**：客户端报 `RemoteProviderException: Unable to load asset bundle from ...` → CORS。CDN 必须配 `Access-Control-Allow-Origin: *`（WebGL 必须）。
- **坑 2**：换了 `BuildVersion` 但老玩家没收到更新 → catalog hash URL 写死成 `1.2.3` 而非动态，老客户端的 `RemoteLoadPath` 还指着旧版本。正确做法：发版后服务端推送新 `BuildVersion`，或安装包内置 `BuildVersion` 通过远程配置覆盖。
- **坑 3**：`Cannot Change Post Release` 组里的资产忘记 Prepare for Content Update 直接重 build → 老玩家拿到新 catalog 后 bundle hash 对不上，全资产崩。务必走 `Update a Previous Build`。
- **坑 4**：`addressables_content_state.bin` 没提交 git → 下次 Content Update 无 baseline，只能整包重发。
- **坑 5**：iOS 上 catalog 缓存目录被系统清理 → 设置 `Application.persistentDataPath` 子目录手动管理，或接受 catalog 重下载（仅几 KB）。
- **坑 6**：Shader 改动在 URP 下没生效 → Shader Variant Collection 单独成包，且必须放 "Cannot Change" 组走 Update Workflow。
- **坑 7**：Android Split APK / AAB → Play Asset Delivery 和 Addressables 远程并存时，Addressables 必须用 Remote，否则 Google 二次下载冲突。
- **坑 8**：catalog 加载慢（>3s）→ catalog 太大（>1MB）。拆 Group 减少 Entry 数量，或开启 `Build Remote Catalog → Bundle Mode: Pack Separately`。
- **坑 9**：CDN 缓存了旧 `catalog_*.hash` → 必须给 `.hash` 设置短 TTL（60s）或带版本号查询参数（`?v=1.2.3`）。

## 配置示例

### Editor Script：构建 + 上传 CDN（Assets/Editor/AddressablesBuildScript.cs）
```csharp
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public static class AddressablesBuildScript
{
    [MenuItem("Build/Addressables/Full Build")]
    public static void FullBuild()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        settings.activeProfileId = settings.profileSettings.GetProfileId("Production");
        settings.profileSettings.SetValue(settings.activeProfileId, "BuildVersion",
            System.Environment.GetEnvironmentVariable("BUILD_VERSION") ?? "0.0.1");

        AddressableAssetSettings.CleanPlayerContent(
            AddressableAssetSettingsDefaultObject.Settings.ActivePlayerDataBuilder);
        AddressableAssetSettings.BuildPlayerContent(out var result);
        if (!string.IsNullOrEmpty(result.Error))
        {
            Debug.LogError($"Addressables build failed: {result.Error}");
            EditorApplication.Exit(1);
        }
        Debug.Log($"Addressables built: {result.OutputPath}");
    }

    [MenuItem("Build/Addressables/Content Update")]
    public static void ContentUpdate()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var statePath = ContentUpdateScript.GetContentStateDataPath(false);
        if (!File.Exists(statePath))
        {
            Debug.LogError($"addressables_content_state.bin not found at {statePath}");
            EditorApplication.Exit(1);
            return;
        }
        var result = ContentUpdateScript.BuildContentUpdate(settings, statePath);
        if (result == null || !string.IsNullOrEmpty(result.Error))
        {
            Debug.LogError($"Content update failed: {result?.Error}");
            EditorApplication.Exit(1);
        }
    }
}
```

### 运行时热更代码（Runtime/HotfixManager.cs）
```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class HotfixManager : MonoBehaviour
{
    [SerializeField] string preloadLabel = "preload";
    [SerializeField] long wifiOnlyThreshold = 50 * 1024 * 1024; // 50MB

    public async Task<bool> CheckAndUpdate(Action<float> onProgress)
    {
        try
        {
            await Addressables.InitializeAsync().Task;

            var checkHandle = Addressables.CheckForCatalogUpdates(false);
            var catalogsToUpdate = await checkHandle.Task;
            if (catalogsToUpdate != null && catalogsToUpdate.Count > 0)
            {
                var updateHandle = Addressables.UpdateCatalogs(catalogsToUpdate, false);
                await updateHandle.Task;
                Addressables.Release(updateHandle);
            }
            Addressables.Release(checkHandle);

            var sizeHandle = Addressables.GetDownloadSizeAsync((object)preloadLabel);
            var size = await sizeHandle.Task;
            Addressables.Release(sizeHandle);

            if (size > wifiOnlyThreshold && Application.internetReachability
                != NetworkReachability.ReachableViaLocalAreaNetwork)
            {
                if (!await ConfirmCellularDialog(size)) return false;
            }

            if (size > 0)
            {
                var dlHandle = Addressables.DownloadDependenciesAsync(
                    (object)preloadLabel, true);
                while (!dlHandle.IsDone)
                {
                    onProgress?.Invoke(dlHandle.PercentComplete);
                    await Task.Yield();
                }
                if (dlHandle.Status != AsyncOperationStatus.Succeeded)
                    throw new Exception("Download failed");
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Hotfix failed, fallback to offline: {e}");
            return false; // UI 走离线降级，载入本地 catalog
        }
    }

    Task<bool> ConfirmCellularDialog(long bytes) { /* 弹窗 */ return Task.FromResult(true); }
}
```

### CDN 配置（Cloudflare R2 + Custom Domain）
```
Bucket: gamedesigner-assets
Custom domain: cdn.example.com
CORS rules:
  - AllowedOrigins: ["*"]
    AllowedMethods: ["GET", "HEAD"]
    AllowedHeaders: ["*"]
    MaxAgeSeconds: 86400
Cache rules (Cloudflare dashboard):
  - URL matches *.bundle / catalog_*.json → Cache TTL 1 year
  - URL matches catalog_*.hash → Cache TTL 60 seconds, bypass on cookie
```
