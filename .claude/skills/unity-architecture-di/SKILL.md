---
name: unity-architecture-di
description: Unity 客户端代码架构与依赖注入实践。涵盖 Zenject vs VContainer 选型、Service Locator、事件总线（MessagePipe/UniRx）、MVC/MVP/MVVM 分层、单例与 ScriptableObject 配置取舍。触发关键词：DI 容器、Zenject、VContainer、依赖注入、架构、service locator、事件总线、单例、SignalBus、MessagePipe、UniRx。
tags: unity, architecture, di, vcontainer, zenject
---

# Unity 客户端架构与 DI

## 何时使用

- 新项目搭脚手架，要选 DI 容器或决定要不要上 DI
- MonoBehaviour 之间互相 `FindObjectOfType` / 单例满天飞，准备重构
- 设计模块解耦、跨场景服务（Audio、Save、Network、Analytics）
- 评估事件总线 vs C# event vs UnityEvent 的使用边界
- ScriptableObject 是"配置"还是"运行时单例"分不清

## 核心原则

- **MonoBehaviour 是边界，不是逻辑容器**：纯 C# 类承载业务，MB 只接 Unity 生命周期与 Inspector 绑定
- **构造注入 > 字段注入 > Service Locator > 全局单例**，优先级从高到低
- **场景内依赖用 DI，跨场景服务用 DontDestroyOnLoad 或 SO**
- **事件总线只解耦"不知道接收者是谁"的广播**，已知通信用接口/委托
- DI 容器一旦选定，不要混用；Zenject 已停更，新项目首选 **VContainer**

## 关键模式

### 模式 A：VContainer 基础布线

```csharp
public class GameLifetimeScope : LifetimeScope {
    [SerializeField] GameConfig _config;
    protected override void Configure(IContainerBuilder builder) {
        builder.RegisterInstance(_config);
        builder.Register<ISaveService, JsonSaveService>(Lifetime.Singleton);
        builder.RegisterEntryPoint<GameStartup>(); // IStartable / ITickable
        builder.RegisterComponentInHierarchy<PlayerController>();
    }
}
```

- `Lifetime.Singleton`：容器生命周期；`Scoped`：子 Scope；`Transient`：每次新建
- `RegisterEntryPoint` 自动接 `IStartable.Start()` / `ITickable.Tick()`，替代 MB 的 Update
- **跨场景共享服务**：父 LifetimeScope 配 `autoRun = false`，子 Scope `parentReference.Type` 引用

### 模式 B：分层 MVP（推荐用于业务 UI）

```
View (MonoBehaviour, 只发事件 + 显示) 
  ↑ Presenter (纯 C#, 注入 Model + View，处理逻辑)
  ↑ Model (纯数据 / Repository, 注入 Service)
```

- View 暴露 `IObservable<Unit> OnClickBuy` 或 `event Action OnClickBuy`
- Presenter 在 `IStartable.Start()` 订阅，在 `IDisposable.Dispose()` 解绑
- Model 用 ReactiveProperty / ObservableCollection，View 单向绑定

### 模式 C：MessagePipe 事件总线（VContainer 友好）

```csharp
// 注册
builder.RegisterMessagePipe();
builder.RegisterMessageBroker<PlayerDiedEvent>(options);

// 发布
[Inject] IPublisher<PlayerDiedEvent> _pub;
_pub.Publish(new PlayerDiedEvent(playerId));

// 订阅（自动 Dispose 跟随 LifetimeScope）
[Inject] ISubscriber<PlayerDiedEvent> _sub;
_sub.Subscribe(e => ...).AddTo(_disposables);
```

- 比 UnityEvent 强类型、可单测、支持异步（`IAsyncPublisher`）
- 滥用警告：调用栈不可追、生命周期易泄漏。**只用于真正多对多解耦**

### 模式 D：ScriptableObject 三种角色

| 角色 | 用法 | 是否运行时修改 |
|------|------|----------------|
| 配置 | `[CreateAssetMenu]` 数值表 | 否（编辑器只读，Build 后不变） |
| 事件 | SO Event Channel 模式 | 否，但承载 runtime 订阅 |
| 运行时容器 | 跨场景状态共享 | 是，**注意 Editor 下 PlayMode 退出不重置** |

运行时 SO 必须在 `OnEnable` 重置默认值，否则编辑器残留状态。

### 模式 E：单例的最后防线

仅在以下情况用静态单例：第三方 SDK 包装、Logger、不依赖容器的工具类。
```csharp
public sealed class Logger {
    static Logger _i; public static Logger I => _i ??= new();
}
```
**不要把 GameManager / Player / Audio 做成 MonoBehaviour 单例**，用 DI 注入。

## 常见坑

- **Zenject `[Inject]` 字段在 Awake 仍是 null**：注入发生在 Awake 之后，初始化代码放 `[Inject] void Construct()` 或 `Start`
- **VContainer 找不到组件**：`RegisterComponentInHierarchy` 要求组件在 LifetimeScope 的 GameObject 子树下；跨场景用 `RegisterComponentOnNewGameObject`
- **循环依赖**：A 注入 B、B 注入 A → 容器抛异常。拆出共享接口或用 `Lazy<T>` / `Func<T>` 延迟
- **场景切换 disposable 泄漏**：MessagePipe / UniRx 订阅必须 `.AddTo(this)` 或 LifetimeScope 的 `IDisposable`
- **SO 当单例还跨场景写状态**：打包后 SO 在某些平台是只读 + 内存共享实例，PlayMode Domain Reload 关闭时旧值残留
- **静态事件 `+= ` 不 `-=`**：场景重载后旧实例被静态字段持有 → 内存泄漏 + 重复回调
- **`FindObjectOfType` 在 Awake 调用**：执行顺序未定，可能查不到。改用 DI 或 ScriptExecutionOrder

## 与其他 skill 的边界

- 与 **unity-foundations** 的区别：foundations 讲 GameObject/Component 基础概念，本 skill 讲组织这些概念的架构层
- 与 **unity-async-patterns** 的区别：async 讲 await/UniTask 正确性，本 skill 讲服务的生命周期注册
- 与 **save-serialization** 的区别：save 讲存档实现细节，本 skill 讲 `ISaveService` 接口如何注入与替换
- 与 **state-machine** 的区别：FSM 是业务模式，本 skill 讲 FSM Context 如何被容器注入
