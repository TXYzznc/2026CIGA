---
name: unity-networking
description: 使用Unity Netcode、Mirror或Photon实现多人游戏。掌握客户端-服务器架构、状态同步和延迟补偿。适用于多人功能开发、网络问题排查或实时同步场景。
requires:
- unity-csharp-fundamentals
tags: unity-networking, multiplayer-game-development, netcode-for-gameobjects, photon-engine,
  mirror-framework
tags_cn: Unity网络开发, 多人游戏制作, Netcode for GameObjects, Photon引擎, Mirror框架
---

# Unity 网络开发 - 多人游戏制作

## 概述

使用Netcode for GameObjects、Mirror或Photon框架实现Unity多人游戏网络功能。

**必备基础**：`unity-csharp-fundamentals`（TryGetComponent、FindAnyObjectByType、空安全编码）

**核心主题**:
- 客户端-服务器架构
- 状态同步
- 延迟补偿
- RPC（远程过程调用）
- 网络变量
- 匹配机制

## 快速入门（Unity Netcode）

```csharp
using Unity.Netcode;

public class Player : NetworkBehaviour
{
    private NetworkVariable<int> mHealth = new NetworkVariable<int>(100);

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Only owner can control
            HandleInput();
        }

        mHealth.OnValueChanged += OnHealthChanged;
    }

    [ServerRpc]
    void TakeDamageServerRpc(int damage)
    {
        mHealth.Value -= damage;
    }

    [ClientRpc]
    void ShowDamageEffectClientRpc()
    {
        // Visual feedback on all clients
    }
}
```

## 网络架构

- **权威服务器**：服务器验证所有操作（竞技类游戏适用）
- **客户端授权**：客户端控制自身实体（合作类游戏适用）
- **中继服务器**：通过NAT穿透实现点对点连接
- **专用服务器**：专业级托管服务

## 同步模式

- **Transform同步**：位置、旋转插值
- **网络变量**：自动状态复制
- **RPC**：远程方法调用
- **所有权管理**：谁可以修改哪些对象

## 参考文档

### [Netcode for GameObjects 基础](references/netcode-fundamentals.md)
核心网络概念:
- NetworkManager设置与配置
- 客户端-服务器架构模式
- 状态同步与RPC

## 最佳实践

1. **服务器权威**：防止作弊
2. **客户端预测**：实现流畅移动
3. **插值处理**：优雅应对延迟
4. **带宽优化**：增量压缩
5. **网络模拟测试**：模拟延迟、丢包情况