# OutPackages — 外部依赖本地缓存

> 所有外部包以**纯源码/本地文件**形式存储于此，项目克隆后开箱即用，无需网络下载。

## 工作方式

- 每个子目录包含一个完整的外部包
- `Packages/manifest.json` 通过 `file:` 协议引用本地路径
- 不依赖 Git URL，不受 GitHub 网络影响

## 依赖清单

| 包 | 版本来源 | 引用路径 |
|---|---|---|
| UniTask | GitHub: Cysharp/UniTask (shallow clone) | `OutPackages/UniTask/src/UniTask/Assets/Plugins/UniTask` |
| DOTween | Asset Store (DEMIGIANT) | `Assets/Plugins/Demigiant/DOTween/` (直接嵌入) |

## 如何更新外部包

1. 删除对应目录
2. 重新 shallow clone 或下载新版本
3. Unity 打开项目后自动检测变更并重新导入
