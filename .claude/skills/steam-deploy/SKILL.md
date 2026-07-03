---
name: steam-deploy
description: Steamworks 发版与 steampipe depot 上传流程，覆盖 steamcmd、VDF 配置、分支管理、Achievement/Stats 集成，触发 Steam 发版、steamcmd、steampipe、Steamworks、depot、Steam 分支、Steam beta
tags: steam, steamworks, steampipe, deployment, depot
---

# Steam 发版流水线（Steamworks + steampipe）

## 何时使用
- 把 Unity 出的可执行包推到 Steam 后台
- 配置/调整 default、beta、canary 分支
- 上传新版本到 depot 并发布到指定分支
- 排查 steamcmd 登录、Guard 验证、depot mapping 报错
- 同步 release notes / What's New 公告

## 核心组件
- **Steamworks 后台**（partner.steamgames.com）—— 创建 App ID / Depot / Branch
- **steamcmd** —— 命令行工具（Windows: `steamcmd.exe`，Linux/Mac: `steamcmd.sh`）
- **steampipe** —— 内嵌在 steamcmd 中的上传命令 `run_app_build`
- **VDF 文件**（Valve Data Format）：
  - `app_build_<appid>.vdf`：定义 build 元数据、关联 depot、目标分支
  - `depot_build_<depotid>.vdf`：定义 depot 内容映射（文件路径 → depot 路径）
- **commando**（社区 GUI 工具）—— 适合一次性发版/培训新人
- **Steamworks SDK**（C++ 头文件 + Steamworks.NET / Facepunch.Steamworks 包装）
- **Build Account**：专用账号 + Steam Guard Mobile Authenticator

## 关键流程

### 流程 A：Steamworks 后台注册（一次性）
1. partner.steamgames.com 创建 App，付 $100 USD 押金。
2. App Admin → Steamworks Settings → Depots 新建 Depot（一般 1 个主 depot，多语言/DLC 再加）。
3. Builds → Branches 创建 `default`（公开）、`beta`（密码保护）、`canary`（内部测试）。
4. Users & Permissions 创建 "Build Account"，分配 "Edit App Metadata" + "Publish Builds" 权限。
5. 给 Build Account 绑定 Steam Guard Mobile Authenticator，导出 `ssfn` 或 `config.vdf` 用于 CI 免交互登录。

### 流程 B：steamcmd 安装与首次登录
```bash
# Linux
mkdir -p ~/steamcmd && cd ~/steamcmd
curl -sqL "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz" | tar zxvf -
./steamcmd.sh +login <buildaccount> +quit  # 首次需手动输入 Guard 码
# 完成后 config/config.vdf 会缓存登录态，CI 用这个文件
```
- CI 把 `config.vdf` + `ssfn*` 文件 base64 后存 GitHub Secret，运行时解码到 `~/Steam/config/`。

### 流程 C：编写 VDF（app_build + depot_build）

**app_build_480.vdf**（appid=480 为示例）
```
"appbuild"
{
    "appid" "480"
    "desc" "SampleProject v1.2.3 - hotfix bug #123"
    "buildoutput" "../output/"
    "contentroot" "../content/"
    "setlive" "canary"
    "preview" "0"
    "local" ""
    "depots"
    {
        "481" "depot_build_481.vdf"
    }
}
```

**depot_build_481.vdf**
```
"DepotBuildConfig"
{
    "DepotID" "481"
    "ContentRoot" "../content/"
    "FileMapping"
    {
        "LocalPath" "*"
        "DepotPath" "."
        "recursive" "1"
    }
    "FileExclusion" "*.pdb"
    "FileExclusion" "*_BurstDebugInformation_DoNotShip/*"
    "FileExclusion" "*_BackUpThisFolder_ButDontShipItWithYourGame/*"
}
```

### 流程 D：上传 build
```bash
steamcmd \
  +login <buildaccount> \
  +run_app_build /path/to/app_build_480.vdf \
  +quit
```
- 退出码 0 = 成功，但务必去后台 Builds 页确认 BuildID 出现。
- `setlive` 留空 = 仅上传不发布；写 `canary`/`beta` = 直接发布到该分支（注意 `default` 通常需后台手动确认）。

### 流程 E：分支切换（推荐流程）
1. 先 `setlive=""` 上传 → 拿到 BuildID。
2. 后台 Builds 页找到该 BuildID，点 "Set Build Live on Branch" → 选 `canary`。
3. QA 在 canary 分支（密码保护）测试 24h。
4. 通过后再切到 `beta`（公测分支），最后 `default`（正式公开）。
5. 出问题立即 "Roll Back" 到上一个 BuildID。

### 流程 F：Achievement / Stats Checklist
1. 后台 Stats & Achievements → 定义 API Name（如 `ACH_FIRST_WIN`）+ 图标（256x256 + 64x64 灰色）。
2. 代码侧（Steamworks.NET）：
   ```csharp
   SteamUserStats.SetAchievement("ACH_FIRST_WIN");
   SteamUserStats.StoreStats();
   ```
3. 测试期勾选 "Achievement Test Mode" 才能反复触发。
4. 发版前确认所有 Achievement 都已 "Publish" 到 live，不能只在 test 状态。

## 常见坑
- **坑 1**：CI 登录失败 `Login Failure: Account Logon Denied` → ssfn 文件没复制到对应路径，或路径权限不对（必须 0600）。
- **坑 2**：上传后 BuildID 显示但内容为空 → `ContentRoot` 路径写错，相对路径基于 vdf 文件所在目录。
- **坑 3**：包体比预期大 GB 级 → 没加 `*_BurstDebugInformation_DoNotShip/` 排除，Unity IL2CPP 调试信息会被打进去。
- **坑 4**：分支切到 default 但用户看不到 → Store Page 还在 "unreleased" 状态，需先点 "Release" 按钮。
- **坑 5**：Mac universal binary 上传后 Intel 用户启动崩溃 → Depot 配置漏了 `OSList` 或 `Architecture` 限制。
- **坑 6**：beta 密码改了用户还能用旧密码进 → Steam 客户端缓存最长 24h，告知玩家退出 Steam 再登。
- **坑 7**：Achievement 在 test mode 触发了但 release 后玩家拿不到 → 该成就 "Publish" 状态没开，且对应玩家 stats 已被 reset。
- **坑 8**：steamcmd 在 macOS Sonoma 闪退 → 用 Rosetta 跑（`arch -x86_64 ./steamcmd.sh`）。

## 配置示例

### GitHub Actions 自动发版（依赖 unity-build-pipeline 产出的 artifact）
```yaml
name: Steam Deploy
on:
  workflow_run:
    workflows: ["Unity Build"]
    types: [completed]
  workflow_dispatch:
    inputs:
      branch:
        type: choice
        options: [canary, beta, default]

jobs:
  deploy:
    runs-on: ubuntu-latest
    if: ${{ github.event.workflow_run.conclusion == 'success' }}
    steps:
      - uses: actions/checkout@v4

      - uses: actions/download-artifact@v4
        with:
          name: Build-StandaloneWindows64
          path: ./content/

      - name: Restore steam config
        run: |
          mkdir -p ~/Steam/config
          echo "${{ secrets.STEAM_CONFIG_VDF }}" | base64 -d > ~/Steam/config/config.vdf
          echo "${{ secrets.STEAM_SSFN }}"       | base64 -d > ~/Steam/ssfn<digits>
          chmod 600 ~/Steam/config/config.vdf ~/Steam/ssfn*

      - uses: game-ci/steam-deploy@v3
        with:
          username: ${{ secrets.STEAM_USERNAME }}
          configVdf: ${{ secrets.STEAM_CONFIG_VDF }}
          appId: 480
          buildDescription: "v${{ github.ref_name }} build #${{ github.run_number }}"
          rootPath: content
          depot1Path: "."
          releaseBranch: ${{ inputs.branch || 'canary' }}
```

### 同步 release notes
后台 Community Hub → "Post an Update" → 三段式：标题 / What's New（bullet）/ 完整 changelog。用 BBCode：
```
[h1]v1.2.3 Hotfix[/h1]
[list]
[*] [b]Fixed[/b]: 主菜单点击 "Continue" 闪退（issue #123）
[*] [b]Balance[/b]: 法师法力恢复 +10%
[/list]
[url=https://github.com/your/repo/releases/tag/v1.2.3]完整 changelog[/url]
```
