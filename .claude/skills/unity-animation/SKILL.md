---
name: unity-animation
description: Unity 6动画系统指南。适用于处理Animator Controller、动画状态机、混合树、动画片段、Avatar系统、人形骨骼、根运动、动画事件、Timeline或Cinemachine的场景。基于Unity
  6.3 LTS文档编写。
tags: unity-animation-system, animator-controller, blend-trees, avatar-retargeting,
  timeline-scripting
tags_cn: Unity动画系统, Animator Controller, 混合树, Avatar重定向, Timeline脚本开发
---

# Unity动画系统

## 动画系统概述

Unity的Mecanim动画系统基于三个相互关联的组件构建：

1. **Animation Clips** -- 运动的基本单元（如Idle、Walk、Run）
2. **Animator Controller** -- 将片段组织成包含状态与过渡的流程图式状态机
3. **Avatar系统** -- 将人形角色骨骼映射到通用内部格式，实现动画重定向

**Animator**组件附加在GameObject上，引用播放所需的Animator Controller和Avatar资源。

### 动画类型

- **Humanoid** -- 需要配置Avatar；支持在不同角色骨骼间重定向动画；CPU占用比Generic类型高15-20%
- **Generic** -- 为特定层级结构的Transform或MonoBehaviour属性添加动画；无法在不同层级结构间转移
- **Legacy** -- 旧版Animation组件；适用于简单的单次播放或UI动画

## Animator Controller

Animator Controller资源为角色或带动画的GameObject编排Animation Clips与过渡效果。

**创建方式：** 在Project窗口右键 > Create > Animator Controller

### 核心组件

- **状态** -- 每个状态播放对应的Animation Clip或混合树
- **过渡** -- 定义状态机切换状态的方式与时机
- **参数** -- 脚本设置的变量（Float、Int、Bool、Trigger），用于控制过渡
- **图层** -- 为不同身体部位或动画需求设置独立的状态机
- **子状态机** -- 嵌套的状态机，用于层级化组织

### 参数类型

提供四种参数类型：

| 类型 | 描述 | 脚本方法 |
|------|-------------|---------------|
| Float | 十进制数 | `SetFloat()` / `GetFloat()` |
| Int | 整数 | `SetInteger()` / `GetInteger()` |
| Bool | 布尔值（真/假） | `SetBool()` / `GetBool()` |
| Trigger | 自动重置的布尔值 | `SetTrigger()` / `ResetTrigger()` |

```csharp
using UnityEngine;

public class PlayerAnimController : MonoBehaviour
{
    Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // 注意：为简化使用了旧版Input Manager。新版Input System可参考unity-input相关内容。
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        bool fire = Input.GetButtonDown("Fire1");

        animator.SetFloat("Forward", v);
        animator.SetFloat("Strafe", h);
        animator.SetBool("Fire", fire);
    }

    void OnCollisionEnter(Collision col)
    {
        if (col.gameObject.CompareTag("Enemy"))
        {
            animator.SetTrigger("Die");
        }
    }
}
```

### Animator Override Controller

在保留状态机结构、参数与逻辑的前提下，替换Animator Controller中的动画片段。适用于多个角色共享同一状态机但使用不同动画片段的场景。

**重要提示：** 使用Override Controller时，需将过渡退出时间设置为**标准化时间**（而非秒），否则若替换的片段时长不同，退出时间可能会被忽略。

### 图层

- **覆盖模式** -- 替换之前图层的动画
- **叠加模式** -- 在之前图层的动画基础上添加新动画
- **Avatar遮罩** -- 将图层限制为特定身体部位（如仅上半身）
- **同步图层** -- 复用状态机结构，搭配不同的动画片段

## 状态机与过渡

### 状态

Animator Controller中的每个状态代表一个独立动作。特殊状态包括：
- **Entry** -- 默认入口点
- **Any State** -- 可从当前任意状态触发过渡
- **Exit** -- 退出当前状态机或子状态机

### 过渡

过渡定义了状态间的融合方式。

| 设置项 | 描述 |
|---------|-------------|
| **Has Exit Time** | 在标准化时间点触发过渡（如0.75表示动画完成75%时） |
| **Transition Duration** | 融合时长；单位为秒（固定时长）或源状态的占比 |
| **Transition Offset** | 目标状态的播放起始点（0.5表示从中间开始） |
| **Conditions** | 基于参数的规则；所有条件需同时满足 |
| **Interruption Source** | 可中断当前过渡的来源：无、当前状态、下一状态或组合 |
| **Ordered Interruption** | 过渡解析是否在当前过渡处停止，或继续查找所有有效过渡 |

同时设置Has Exit Time与Conditions时，Unity仅会在退出时间到达后检查条件。

### State Machine Behaviours

继承自`StateMachineBehaviour`的脚本可附加到状态上。回调方法包括：`OnStateEnter`、`OnStateUpdate`、`OnStateExit`、`OnStateMove`、`OnStateIK`。所有方法都会接收参数`(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)`。

```csharp
public class AttackState : StateMachineBehaviour
{
    public AudioClip attackSound;
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        AudioSource.PlayClipAtPoint(attackSound, animator.transform.position);
    }
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 离开状态时的清理操作
    }
}
```

## 混合树

混合树可根据参数值在多个相似动画间平滑融合，与过渡效果（在不同状态间逐步切换）不同。

**关键要求：** 动画需具有相似的性质与时间节奏。脚部接触点需在标准化时间上对齐（如左脚在0.0，右脚在0.5）。

### 混合类型

- **1D** -- 单个参数控制融合（如用速度控制Walk/Run动画）
- **2D Simple Directional** -- 两个参数，每个方向对应一个运动
- **2D Freeform Directional** -- 两个参数，每个方向对应多个运动
- **2D Freeform Cartesian** -- 两个参数，运动不代表方向
- **Direct** -- 每个运动有独立的权重参数（如面部动画）

**创建方式：** 在Animator Controller中右键 > Create State > From New Blend Tree。双击进入图形界面，通过Inspector添加子运动。

```csharp
// 通过脚本驱动 locomotion 混合树
void Update()
{
    float speed = new Vector3(rb.velocity.x, 0, rb.velocity.z).magnitude;
    float direction = Vector3.SignedAngle(transform.forward,
        rb.velocity.normalized, Vector3.up);

    animator.SetFloat("Speed", speed, 0.1f, Time.deltaTime);
    animator.SetFloat("Direction", direction, 0.1f, Time.deltaTime);
}
```

## Avatar与人形骨骼

Avatar系统将模型识别为人形，并映射身体部位以实现动画重定向。

### 核心概念

- **Avatar** -- 将骨骼结构映射到Unity的内部人形格式
- **重定向** -- 动画可在共享同一Avatar映射的不同人形骨骼间转移
- **肌肉定义** -- 在肌肉空间而非骨骼空间进行直观控制

### 设置步骤

1. 在Project窗口中选择模型
2. 在Import Settings的Rig标签页，将Animation Type设置为**Humanoid**
3. 配置Avatar映射（Unity会尽可能自动完成映射）
4. 在Avatar配置窗口中验证并调整骨骼分配

## 根运动

根运动将动画驱动的运动传递给GameObject的Transform组件。

### 架构

- **Body Transform** -- 角色的重心；存储世界空间曲线
- **Root Transform** -- Body Transform在Y平面的投影；运行时每帧计算

### 片段检查器设置

| 设置项 | 用途 |
|---------|---------|
| **Bake Into Pose (Rotation)** | 方向信息保留在角色身体上；GameObject不会接收旋转信息 |
| **Bake Into Pose (Y)** | 垂直运动保留在角色身体上；除跳跃动画外，建议启用 |
| **Bake Into Pose (XZ)** | 水平运动保留在角色身体上；为Idle片段启用可防止漂移 |
| **Based Upon** | 身体朝向（动作捕捉）、原始（关键帧动画）、脚部（防止浮空） |

`Animator.gravityWeight`由Bake Into Pose Position Y的设置决定：启用时为1，禁用时为0。

```csharp
// 自定义根运动处理
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class RootMotionController : MonoBehaviour
{
    Animator animator;
    CharacterController controller;

    void Start()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
        animator.applyRootMotion = false; // 手动处理根运动
    }

    void OnAnimatorMove()
    {
        // 通过CharacterController应用根运动
        Vector3 deltaPosition = animator.deltaPosition;
        deltaPosition.y -= 9.81f * Time.deltaTime; // 添加重力
        controller.Move(deltaPosition);
        transform.rotation *= animator.deltaRotation;
    }
}
```

## 动画事件

动画事件可在动画时间线的指定点触发函数。

### 参数类型

- **Float** -- 数值（如音量）
- **Int** -- 整数值
- **String** -- 文本数据
- **Object** -- GameObject或Prefab引用

### 设置步骤

1. 在Animation标签页，展开Events区域
2. 将播放头定位到目标帧
3. 点击Add Event
4. 设置与附加脚本中方法名匹配的函数名

```csharp
using UnityEngine;

public class FootstepHandler : MonoBehaviour
{
    public AudioClip[] footstepSounds;
    public GameObject dustPrefab;

    // 由动画事件调用 -- 函数名需与事件设置一致
    public void PlayFootstep(int footIndex)
    {
        if (footstepSounds.Length > 0)
        {
            int clipIndex = Random.Range(0, footstepSounds.Length);
            AudioSource.PlayClipAtPoint(footstepSounds[clipIndex], transform.position);
        }
    }

    // 由传递Object参数的动画事件调用
    public void SpawnEffect(Object effectPrefab)
    {
        Instantiate((GameObject)effectPrefab, transform.position, Quaternion.identity);
    }
}
```

## Timeline

Timeline用于创建影视内容、游戏序列、音频序列与粒子效果。对应的包为`com.unity.timeline`（v1.8.11，兼容Unity 6.3）。

### 核心组件

- **Timeline Asset** -- 定义轨道、片段及其排列方式
- **Timeline Instance** -- 绑定到特定场景对象的运行时实例
- **Playable Director** -- 用于播放Timeline资源并将轨道绑定到场景对象的组件

### 轨道类型

| 轨道 | 用途 |
|-------|---------|
| Animation | 控制绑定GameObject上的Animator |
| Audio | 在绑定的AudioSource上播放AudioClips |
| Activation | 启用/禁用绑定的GameObject |
| Signal | 通过SignalReceiver在特定时间触发事件 |
| Control | 触发子Timeline、粒子系统或其他Playable Director |
| Playable | 使用Playables API的自定义轨道 |

### 功能特性

- 直接在Timeline中录制动画
- 支持人形动画
- 动画覆盖轨道
- 子Timeline用于模块化组合

## 动画脚本开发

### 逆运动学（IK）

要求：人形Avatar，图层启用IK Pass。

```csharp
using UnityEngine;

public class IKController : MonoBehaviour
{
    Animator animator;
    public bool ikActive = true;
    public Transform rightHandTarget;
    public Transform lookTarget;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void OnAnimatorIK()
    {
        if (animator == null || !ikActive) return;

        // 看向目标
        animator.SetLookAtWeight(1f);
        animator.SetLookAtPosition(lookTarget.position);

        // 右手IK
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1f);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1f);
        animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandTarget.position);
        animator.SetIKRotation(AvatarIKGoal.RightHand, rightHandTarget.rotation);
    }
}
```

### 直接状态播放

```csharp
// 立即播放状态
animator.Play("Attack");

// 从标准化时间偏移处播放（从50%开始）
animator.Play("Attack", 0, 0.5f);

// 在0.25秒内交叉淡入到目标状态
animator.CrossFadeInFixedTime("Run", 0.25f);

// 使用标准化时间交叉淡入
animator.CrossFade("Run", 0.1f);
```

### 查询状态信息

```csharp
AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

if (stateInfo.IsName("Attack"))
{
    Debug.Log("当前正在攻击");
}

if (stateInfo.normalizedTime >= 1.0f && !animator.IsInTransition(0))
{
    Debug.Log("动画已完成");
}

// 使用哈希值提升性能
int runHash = Animator.StringToHash("Run");
if (animator.HasState(0, runHash))
{
    animator.Play(runHash);
}
```

### 目标匹配

```csharp
// 在攀爬动画中让角色的手匹配到边缘位置
animator.MatchTarget(ledgePosition, Quaternion.identity, AvatarTarget.RightHand,
    new MatchTargetWeightMask(Vector3.one, 0f), 0.1f, 0.4f);
```

## 常见模式

### 角色移动控制器

```csharp
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class LocomotionController : MonoBehaviour
{
    Animator animator;
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int GroundedHash = Animator.StringToHash("Grounded");
    static readonly int JumpHash = Animator.StringToHash("Jump");

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        float speed = Input.GetAxis("Vertical");
        animator.SetFloat(SpeedHash, Mathf.Abs(speed), 0.1f, Time.deltaTime);
        animator.SetBool(GroundedHash, IsGrounded());

        if (Input.GetButtonDown("Jump") && IsGrounded())
        {
            animator.SetTrigger(JumpHash);
        }
    }

    bool IsGrounded()
    {
        return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.2f);
    }
}
```

### 运行时替换Override Controller

```csharp
using UnityEngine;

public class WeaponAnimSwap : MonoBehaviour
{
    public AnimatorOverrideController swordOverride;
    public AnimatorOverrideController bowOverride;
    Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    public void EquipSword()
    {
        animator.runtimeAnimatorController = swordOverride;
    }

    public void EquipBow()
    {
        animator.runtimeAnimatorController = bowOverride;
    }
}
```

## 反模式

| 反模式 | 问题 | 解决方案 |
|-------------|---------|----------|
| 每帧使用字符串参数 | 产生GC分配，查找速度慢 | 使用`Animator.StringToHash()`缓存哈希值 |
| 为非人形对象使用人形骨骼 | 产生15-20%不必要的CPU开销 | 对道具、动物、特效使用Generic动画类型 |
| 共享根节点下有多个Animator | 每个层级结构的单线程写入限制会阻碍并行处理 | 为每个Animator分配独立的根GameObject |
| 过度使用StateMachineBehaviour | 创建同步点，破坏并行评估 | 尽量减少SMB回调；优先使用脚本端逻辑 |
| 通过Animator动画RectTransform | 存在确定性写入问题 | 对UI动画使用旧版Animation组件 |
| 为单次播放动画使用复杂状态机 | 即使在 idle 时也会持续评估过渡 | 使用Animation组件或Playables API替代 |
| 手动调用`Animator.Update()` | 绕过并行执行 | 使用PlayableGraph进行手动控制以支持并行 |
| 未将过渡退出时间设置为标准化时间 | Override Controller的片段可能会跳过过渡 | 始终为退出时间使用标准化时间 |
| 替换控制器后忘记调用`Rebind()` | 绑定过期导致播放错误 | 运行时更改控制器时调用`Rebind()` |
| Idle片段未启用Bake Into Pose (XZ) | Idle动画会累积漂移 | 对静止片段启用Bake Into Pose XZ |

## 关键API速查

### Animator -- 核心属性

`applyRootMotion`, `speed`, `updateMode`（Normal/AnimatePhysics/UnscaledTime）, `cullingMode`, `deltaPosition`, `deltaRotation`, `velocity`, `isHuman`, `layerCount`, `bodyPosition`, `gravityWeight`

### Animator -- 核心方法

- **播放控制：** `Play(state, layer, time)`, `CrossFade(state, duration)`, `CrossFadeInFixedTime(state, duration)`
- **参数控制：** `SetFloat/Int/Bool/Trigger(name, value)`, `GetFloat/Int/Bool(name)`, `ResetTrigger(name)`
- **状态查询：** `GetCurrentAnimatorStateInfo(layer)`, `GetNextAnimatorStateInfo(layer)`, `IsInTransition(layer)`
- **IK控制：** `SetIKPosition/Rotation(goal, value)`, `SetIKPositionWeight/RotationWeight(goal, weight)`, `SetLookAtPosition(pos)`
- **工具方法：** `MatchTarget(...)`, `GetBoneTransform(bone)`, `Rebind()`, `StringToHash(name)`, `SetLayerWeight(layer, weight)`

## 相关技能

- **unity-foundations** -- Unity核心概念、GameObject、组件、项目结构
- **unity-scripting** -- MonoBehaviour生命周期、C#模式、事件系统
- **unity-graphics** -- 渲染、材质、着色器（与动画材质属性相关）

## 额外资源

- [动画概述](https://docs.unity3d.com/6000.3/Documentation/Manual/AnimationOverview.html) | [Animator Controller](https://docs.unity3d.com/6000.3/Documentation/Manual/class-AnimatorController.html) | [动画片段](https://docs.unity3d.com/6000.3/Documentation/Manual/AnimationClips.html)
- [状态机](https://docs.unity3d.com/6000.3/Documentation/Manual/AnimationStateMachines.html) | [参数](https://docs.unity3d.com/6000.3/Documentation/Manual/AnimationParameters.html) | [图层](https://docs.unity3d.com/6000.3/Documentation/Manual/AnimationLayers.html)
- [混合树](https://docs.unity3d.com/6000.3/Documentation/Manual/class-BlendTree.html) | [Avatar设置](https://docs.unity3d.com/6000.3/Documentation/Manual/AvatarCreationandSetup.html) | [根运动](https://docs.unity3d.com/6000.3/Documentation/Manual/RootMotion.html)
- [动画事件](https://docs.unity3d.com/6000.3/Documentation/Manual/AnimationEventsOnImportedClips.html) | [IK](https://docs.unity3d.com/6000.3/Documentation/Manual/InverseKinematics.html) | [Override Controller](https://docs.unity3d.com/6000.3/Documentation/Manual/AnimatorOverrideController.html)
- [Animator API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Animator.html) | [StateMachineBehaviour API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/StateMachineBehaviour.html) | [Timeline](https://docs.unity3d.com/Packages/com.unity.timeline@1.8/manual/index.html)
