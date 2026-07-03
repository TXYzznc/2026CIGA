---
name: physics-collision
description: Unity 物理与碰撞实战。涵盖 Rigidbody 2D/3D 选型、Collider 类型、Layer Collision Matrix、OnTrigger vs OnCollision 生命周期、Raycast/SphereCast/BoxCast、CharacterController vs Rigidbody movement、CCD 连续碰撞、物理性能优化。触发关键词：物理、collision、碰撞、rigidbody、raycast、collider、layer、CharacterController、触发器、kinematic、CCD、joint。
tags: unity, physics, collision, rigidbody, raycast
---

# Unity 物理与碰撞

## 何时使用

- 角色移动方案选型：CharacterController / Rigidbody / 自实现
- 子弹穿墙、快速物体穿模
- 触发器 OnTriggerEnter 不响应、调一半失灵
- 配 Layer Collision Matrix 减少无效碰撞
- 用 Raycast 做交互检测、地面检测、射线攻击
- 物理性能瓶颈分析

## 核心原则

- **物理代码只放 `FixedUpdate`**，`Update` 中改 Rigidbody 速度会抖
- **要响应碰撞至少一方有非 Kinematic Rigidbody**，两个 Static Collider 永远不触发
- **Raycast 第一次必查 LayerMask**，全 Layer 遍历每帧拖性能
- **CCD 默认关，只对必要物体开**（快速子弹、小球），全开会爆 CPU
- 2D 用 `Rigidbody2D` + `Collider2D`，3D 用 `Rigidbody` + `Collider`，**不能混用**

## 关键模式

### 模式 A：角色移动三选一

| 方案 | 优势 | 劣势 | 场景 |
|------|------|------|------|
| `CharacterController` | 简单、可控、不需 Rigidbody | 不响应物理力、楼梯/斜坡需自处理 | 第一/三人称、FPS |
| `Rigidbody` (Dynamic) | 真实物理、可被推动 | 调参痛苦、易抖 | 物理沙盒、滚球 |
| `Rigidbody` (Kinematic) + `MovePosition` | 可控 + 触发碰撞 | 需手算地面/斜坡 | 自定义 controller、Top-down |

CharacterController 推荐：
```csharp
void Update() {
    if (cc.isGrounded) _velY = -2f; // 贴地
    var move = transform.TransformDirection(input) * speed;
    _velY += gravity * Time.deltaTime;
    cc.Move((move + Vector3.up * _velY) * Time.deltaTime);
}
```

### 模式 B：Trigger vs Collision 生命周期

| 事件 | 触发条件 |
|------|----------|
| `OnCollisionEnter` | 双方实体碰撞，至少一方 Rigidbody 非 Kinematic |
| `OnTriggerEnter` | 至少一方 `isTrigger = true`，至少一方 Rigidbody（**Kinematic 也行**） |
| `OnCollisionStay` | 每 FixedUpdate 调用，性能敏感 |
| `OnTriggerExit` | 物体被销毁时**不会触发**，注意手动清理 |

**铁律**：触发器物体上必须有 Rigidbody（Kinematic 也行），否则与另一个 Static Collider 不触发。

### 模式 C：Layer Collision Matrix

Edit → Project Settings → Physics → Layer Collision Matrix。

策略：
- Player / Enemy / Projectile / Environment / UI / Trigger 分 Layer
- 子弹层只与 Enemy + Environment 勾选
- UI 层与所有层取消勾选（除非要 UI 物理）
- 减少 60-80% 无效碰撞计算

```csharp
int mask = LayerMask.GetMask("Enemy", "Environment");
if (Physics.Raycast(origin, dir, out var hit, dist, mask)) { ... }
```

### 模式 D：Raycast 家族选型

| API | 形状 | 用途 |
|-----|------|------|
| `Raycast` | 线 | 精确点检测、瞄准 |
| `SphereCast` | 移动球 | 地面检测、宽松交互 |
| `BoxCast` | 移动盒 | 平台跳跃地面检测 |
| `CapsuleCast` | 移动胶囊 | 角色前进路径检测 |
| `OverlapSphere` | 静态球 | 范围伤害、AI 视野 |
| `*NonAlloc` 版本 | 同上 | **高频调用必用**，传入预分配数组 |

```csharp
readonly RaycastHit[] _hits = new RaycastHit[8];
int n = Physics.RaycastNonAlloc(origin, dir, _hits, dist, mask);
for (int i = 0; i < n; i++) { ... }
```

### 模式 E：CCD（连续碰撞检测）

Rigidbody.collisionDetectionMode：
- `Discrete`：默认，最快，快速物体穿模
- `Continuous`：检测 Dynamic Rigidbody 间 + Static Mesh，慢
- `ContinuousDynamic`：检测所有 Dynamic + Static，最慢
- `ContinuousSpeculative`：基于预测，便宜但偶有误判

**只给必须的物体开**：子弹、高速碰撞球、电梯。

### 模式 F：性能优化清单

- 用 Primitive Collider（Box/Sphere/Capsule）替代 MeshCollider
- MeshCollider 必须 `convex = true` 否则不能与非 Kinematic Rigidbody 碰撞
- 静态物体 Rigidbody → Static Collider（Unity 自动合批）
- `Physics.autoSyncTransforms = false`（Unity 默认已关），手动 `Physics.SyncTransforms` 在批量 `transform.position` 后
- 把 `Fixed Timestep` 从 0.02 调到 0.0166 或 0.0333 看项目需求
- Profile 看 `Physics.Simulate` 时间

## 常见坑

- **`OnTriggerEnter` 不响应**：两个 Collider 都没 Rigidbody。任意一方加 Kinematic Rigidbody 即可
- **快速物体穿墙**：开 CCD 或加厚墙体或用 SphereCast 替代直接位移
- **`transform.position` 改 Rigidbody 物体**：跳变、无碰撞响应。改用 `rb.MovePosition`
- **Rigidbody 卡墙抖动**：摩擦系数为 0 + 重力作用。给地面 Physic Material 调摩擦
- **`Collider.bounds` 在 OnEnable 时是上一帧的**：调用 `Physics.SyncTransforms` 或延到 Start
- **MeshCollider Concave vs Convex**：非 Convex 不能动 + 不能撞非 Kinematic
- **OnCollisionStay 卡 CPU**：物体堆叠时每帧调用，避免在里面做复杂逻辑
- **`Raycast` 起点在 Collider 内部不返回该 Collider**：Physics.queriesHitBackfaces / queriesHitTriggers 设置
- **Layer 设错忘了 `LayerMask.NameToLayer` 拼写**：返回 -1 导致 mask 全 0，永远不命中

## 与其他 skill 的边界

- 与 **ue-physics-collision** 的区别：那个是 Unreal，本 skill 是 Unity
- 与 **unity-input-correctness** 的区别：那个讲输入系统，本 skill 讲输入后的物理响应
- 与 **state-machine** 的区别：那个讲行为状态，本 skill 讲物理触发
- 与 **godot-combat-system** 的区别：Godot 引擎下的 Hitbox/Hurtbox 实现，本 skill 是 Unity 通用碰撞
