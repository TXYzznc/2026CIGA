---
name: state-machine
description: Unity 状态机与行为树实战。涵盖纯 C# FSM（State 接口 + Context）、Animator StateMachineBehaviour 边界、Behavior Tree（NodeCanvas / Behavior Designer / 自建）、AI 决策模式、状态泄漏与重入等常见错误。触发关键词：FSM、状态机、behavior tree、BT、AI、NPC 行为、Animator StateMachineBehaviour、HFSM、决策树、HTN。
tags: unity, fsm, behavior-tree, ai
---

# Unity 状态机与行为树

## 何时使用

- 角色 / 怪物 / Boss 有明确"模式切换"逻辑（Idle / Patrol / Chase / Attack）
- UI 流程有多步骤（Loading → Menu → Game → Settle）
- 网络对局有阶段（连接中 → 准备 → 战斗 → 结算）
- Animator 状态机塞业务逻辑塞到失控，准备拆出
- AI 决策需要可视化编辑、可配置

## 核心原则

- **代码 FSM 管业务逻辑、Animator 管动画播放**，互相通过参数桥接，不要混
- **状态必须有进入、退出、更新三个明确点**，转移条件只放 `Update`/`Tick`
- **状态不持有彼此的引用**，所有共享数据放 Context / Blackboard
- **简单选 FSM，复杂选 HFSM/BT**，超过 8 个平铺状态就要分层
- 状态切换走"请求—验证—执行"，不要在 `Update` 中直接 `currentState = newState`

## 关键模式

### 模式 A：纯 C# FSM（无依赖）

```csharp
public interface IState<TCtx> {
    void Enter(TCtx ctx);
    void Tick(TCtx ctx, float dt);
    void Exit(TCtx ctx);
}
public class StateMachine<TCtx> {
    IState<TCtx> _cur; TCtx _ctx;
    public void Change(IState<TCtx> next) {
        _cur?.Exit(_ctx);
        _cur = next;
        _cur?.Enter(_ctx);
    }
    public void Tick(float dt) => _cur?.Tick(_ctx, dt);
}
```
Context 持有 Rigidbody / Animator / Blackboard，State 是无状态可复用的 class 实例（甚至 static）。

### 模式 B：HFSM（分层状态机）

适用于"战斗 / 非战斗"大状态下又有子状态。父状态 Tick 自身后 Tick 子状态：
```csharp
public abstract class CompoundState<TCtx> : IState<TCtx> {
    protected StateMachine<TCtx> _sub = new();
    public virtual void Tick(TCtx ctx, float dt) {
        OnTick(ctx, dt);
        _sub.Tick(dt);
    }
}
```
**优势**：共享转移规则（"任何战斗子状态被击晕 → Stunned"放父层一次写完）。

### 模式 C：Animator StateMachineBehaviour 的正确用法

```csharp
public class FootstepSMB : StateMachineBehaviour {
    public override void OnStateEnter(Animator a, AnimatorStateInfo s, int l) {
        a.GetComponent<AudioSource>().Play();
    }
}
```
- 只放**与动画状态强耦合**的副作用：音效、特效、IK 权重
- **不要**在 SMB 里转移业务 FSM、不要持有跨场景引用（SMB 实例在 Animator 上共享）
- 想读外部数据：`a.GetComponent<X>()`，**禁止 `FindObjectOfType`**

### 模式 D：Behavior Tree 选型

| 方案 | 价格 | 可视化 | 性能 | 推荐 |
|------|------|--------|------|------|
| 自建 BT | 0 | 无 | 自控 | 小项目、强定制 |
| NodeCanvas | $ | 强（FSM+BT+HTN 三合一） | 中 | 中型 RPG/动作 |
| Behavior Designer | $$ | 强 | 中 | 团队熟悉 |
| Unity Muse Behavior（实验） | 免费 | 官方 | - | 观望 |

自建 BT 最小骨架：
```csharp
public enum Status { Running, Success, Failure }
public abstract class Node { public abstract Status Tick(Blackboard bb); }
public class Sequence : Node { /* 子节点全 Success 才 Success */ }
public class Selector : Node { /* 任一子节点 Success 即 Success */ }
public class Parallel : Node { /* 并行 */ }
```

### 模式 E：转移请求队列（防重入）

```csharp
public class StateMachine<TCtx> {
    IState<TCtx> _pending;
    public void RequestChange(IState<TCtx> s) => _pending = s;
    public void Tick(float dt) {
        _cur?.Tick(_ctx, dt);
        if (_pending != null) {
            var n = _pending; _pending = null;
            _cur?.Exit(_ctx); _cur = n; _cur.Enter(_ctx);
        }
    }
}
```
避免 `Tick` 中 `Change` → 新状态 `Enter` 又 `Change` 的递归地狱。

## 常见坑

- **状态泄漏**：State 实例存了上一次的临时数据（计时器、目标引用）。修法：`Enter` 中重置所有字段，或每次 `new State()`
- **`Exit` 没调用就场景切换**：FSM 持有的协程 / 订阅没解绑 → 重新进入时双倍触发。修法：State 实现 `IDisposable`，StateMachine 在销毁时调用
- **Animator 参数与代码 FSM 不同步**：代码切到 Attack 但 Animator 还在 Idle。约定单一来源：代码 FSM 每次 `Enter` 时 `animator.SetTrigger`，Animator 转移只看参数
- **Trigger 参数被多次 Set**：Animator Trigger 进入下一状态前没消费会累积。改用 `bool` + 手动复位
- **BT Tick 频率与 FrameRate 绑死**：在 30FPS 上 BT 每帧跑 Selector 全树 → CPU 飙升。加 `TickRate`(0.1s) 或事件驱动
- **多个 AI 共享 BT 节点静态实例 + 局部变量**：节点必须无状态，运行时数据放 Blackboard
- **Coroutine 当作 FSM**：`yield return new WaitForSeconds` 串行写成的"状态机"无法中断、无法转移、无法测试。规模一大就重构

## 与其他 skill 的边界

- 与 **animation-systems / unity-animation** 的区别：那俩讲 Animator/Blend Tree/IK 实现，本 skill 讲业务状态层
- 与 **unity-architecture-di** 的区别：架构讲 FSM Context 如何注入，本 skill 讲 FSM 本身的实现
- 与 **godot-genre-stealth** 的区别：那个讲 Godot 隐身 AI 设计模式，本 skill 是 Unity 通用状态机基础设施
- 与 **combat-balancer** 的区别：那个讲战斗数值平衡，本 skill 讲战斗行为流程控制
