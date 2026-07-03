---
name: unity-async-patterns
description: Unity异步与协程正确性模式。涵盖Awaitable重复等待、缺少取消令牌、BackgroundThreadAsync后的线程上下文、协程异常吞噬、批量模式下的WaitForEndOfFrame以及Addressables句柄泄漏等常见错误。模式格式：WHEN/WRONG/RIGHT/GOTCHA。基于Unity
  6.3 LTS文档。
globs:
- '**/*.cs'
tags: unity-async, coroutine-patterns, awaitable-api, addressables, cancellation-tokens
tags_cn: Unity异步编程, 协程模式, Awaitable API, Addressables资源管理, 取消令牌使用
---

# 异步与协程模式——正确性模式

> **前置技能：** `unity-scripting`（协程、Awaitable API、yield类型）、`unity-lifecycle`（销毁时机、destroyCancellationToken）

这些模式针对的是一类特别危险的异步Bug：它们在测试阶段往往能正常运行，但在生产环境中会失效，比如异常被静默吞噬、对象在等待过程中被销毁、线程上下文违规等。

---

## 模式：Awaitable重复等待

WHEN：存储`Awaitable`实例并多次等待它

WRONG（Claude默认写法）：
```csharp
Awaitable task = Awaitable.WaitForSecondsAsync(2f);
await task; // 第一次等待——正常工作
await task; // 第二次等待——未定义行为（可能立即完成或抛出异常）
```

RIGHT：
```csharp
// Awaitable采用对象池机制——第一次等待完成后，实例会被回收
// 每个Awaitable实例应仅被等待一次

// 如果需要从多个地方等待同一个操作，请使用.AsTask()：
var task = Awaitable.WaitForSecondsAsync(2f).AsTask();
await task; // 正常工作
await task; // 正常工作——Task不使用对象池

// 或者直接创建独立的Awaitable实例：
await Awaitable.WaitForSecondsAsync(2f);
await Awaitable.WaitForSecondsAsync(2f); // 全新实例
```

GOTCHA：Unity通过对象池复用`Awaitable`实例以避免内存分配。实例完成后会被返回对象池，可能被完全不同的操作复用。对同一个实例进行第二次`await`可能会看到其他操作的状态、立即完成或抛出异常。这与`Task`不同，`Task`可以安全地多次等待。当你需要多次等待语义时使用`.AsTask()`，但要注意这会产生内存分配。

---

## 模式：缺少destroyCancellationToken

WHEN：在MonoBehaviour中编写异步方法

WRONG（Claude默认写法）：
```csharp
async Awaitable Start()
{
    await Awaitable.WaitForSecondsAsync(5f);
    // 如果等待期间对象被销毁：
    // - 下一次调用Unity API时会抛出MissingReferenceException
    // - 更糟的情况：静默操作“伪空”对象
    transform.position = Vector3.zero;
}
```

RIGHT：
```csharp
async Awaitable Start()
{
    try
    {
        await Awaitable.WaitForSecondsAsync(5f, destroyCancellationToken);
        transform.position = Vector3.zero;
    }
    catch (OperationCanceledException)
    {
        // 对象已被销毁——这是预期情况，并非错误
    }
}

// 对于包含多个等待步骤的方法：
async Awaitable DoMultiStepWork()
{
    var token = destroyCancellationToken;

    await Awaitable.NextFrameAsync(token);
    ProcessStep1();

    await Awaitable.WaitForSecondsAsync(1f, token);
    ProcessStep2(); // 安全：如果对象已销毁，会在执行到此处前抛出异常

    await LoadAssetAsync(token);
    ProcessStep3();
}
```

GOTCHA：`destroyCancellationToken`是`MonoBehaviour`的属性，当`OnDestroy`开始时触发。每个`Awaitable`等待方法都接受一个可选的`CancellationToken`。如果不传入该令牌，即使对象已被销毁，等待仍会正常完成，进而导致`MissingReferenceException`。务必传入令牌并捕获`OperationCanceledException`。

---

## 模式：BackgroundThreadAsync后的线程上下文

WHEN：在后台线程完成工作后返回Unity API调用

WRONG（Claude默认写法）：
```csharp
async Awaitable ProcessData()
{
    await Awaitable.BackgroundThreadAsync();
    var result = HeavyComputation(); // 正常：在后台线程运行

    // 崩溃：从后台线程访问Unity API
    transform.position = new Vector3(result, 0, 0);
}
```

RIGHT：
```csharp
async Awaitable ProcessData()
{
    await Awaitable.BackgroundThreadAsync();
    var result = HeavyComputation(); // 在后台线程运行

    await Awaitable.MainThreadAsync(); // 切换回主线程
    transform.position = new Vector3(result, 0, 0); // 现在安全

    // 可以来回切换：
    await Awaitable.BackgroundThreadAsync();
    var moreData = AnotherHeavyTask();

    await Awaitable.MainThreadAsync();
    ApplyResults(moreData);
}
```

GOTCHA：调用`BackgroundThreadAsync()`后，所有后续代码都会在线程池线程上运行，直到你通过`MainThreadAsync()`显式切换回主线程。Unity API（Transform、GameObject、Physics等）**不是线程安全的**，如果从后台线程调用会抛出异常或破坏状态。`MainThreadAsync()`会在下一帧的玩家循环更新时恢复执行，而非立即恢复。

---

## 模式：协程异常吞噬

WHEN：协程内部发生异常

WRONG（Claude默认写法）：
```csharp
IEnumerator LoadAndProcess()
{
    yield return LoadData(); // 如果此处抛出异常，协程会静默停止
    ProcessData();           // 永远不会执行，控制台无错误（或仅日志，无堆栈信息）
}

// try/catch无法与yield配合使用：
IEnumerator BadErrorHandling()
{
    try
    {
        yield return SomethingDangerous(); // 编译错误：无法在带有catch的try块中使用yield
    }
    catch (Exception e)
    {
        Debug.LogError(e);
    }
}
```

RIGHT：
```csharp
// 选项1：改用Awaitable（支持异常传播）
async Awaitable LoadAndProcess()
{
    try
    {
        await LoadDataAsync();
        ProcessData();
    }
    catch (Exception e)
    {
        Debug.LogError($"加载失败：{e}");
    }
}

// 选项2：不在try块中使用yield的错误处理方式
IEnumerator LoadAndProcessCoroutine()
{
    bool success = false;
    Exception error = null;

    // 将yield包裹在try/catch外部
    yield return LoadDataCoroutine(result =>
    {
        success = true;
    });

    // 在yield完成后处理错误
    if (!success)
    {
        Debug.LogError("加载失败");
        yield break;
    }

    ProcessData();
}
```

GOTCHA：在协程中，`yield return`不能出现在带有`catch`子句的`try`块中（C#语言限制）。yield的协程中发生的异常会被记录到控制台，但执行会静默停止——不会传播到调用者。调用者的协程会继续执行，就像嵌套协程已完成一样。对于任何可能失败且需要错误处理的操作，使用`Awaitable`。

---

## 模式：批量模式下的WaitForEndOfFrame

WHEN：在无头/服务器/测试环境中使用`WaitForEndOfFrame`或`Awaitable.EndOfFrameAsync`

WRONG（Claude默认写法）：
```csharp
IEnumerator CaptureScreenshot()
{
    yield return new WaitForEndOfFrame(); // 在批量模式下挂起（无渲染）
    var tex = ScreenCapture.CaptureScreenshotAsTexture();
}

// Awaitable存在同样问题：
async Awaitable WaitForRender()
{
    await Awaitable.EndOfFrameAsync(); // 在批量模式下挂起
}
```

RIGHT：
```csharp
IEnumerator CaptureScreenshot()
{
    // 检查是否处于批量模式
    if (Application.isBatchMode)
    {
        yield return null; // 改为等待一帧
        Debug.LogWarning("批量模式下无法截图");
        yield break;
    }

    yield return new WaitForEndOfFrame();
    var tex = ScreenCapture.CaptureScreenshotAsTexture();
}

// 对于需要推进帧但无需渲染的测试：
IEnumerator TestCoroutine()
{
    yield return null; // 推进一帧（适用于所有模式）
    // yield return new WaitForFixedUpdate(); // 在批量模式下也能正常工作
}
```

GOTCHA：`WaitForEndOfFrame`和`EndOfFrameAsync`会等待渲染阶段完成。在批量模式（`-batchmode`参数）、无头服务器和部分测试运行器中，没有渲染过程——因此这些yield永远不会完成，协程/异步操作会永久挂起。使用`yield return null`（下一帧Update）或`Awaitable.NextFrameAsync()`来实现适用于所有环境的帧推进。

---

## 模式：嵌套协程取消

WHEN：停止启动了子协程的父协程

WRONG（Claude默认写法）：
```csharp
Coroutine _mainRoutine;

void Start()
{
    _mainRoutine = StartCoroutine(MainLoop());
}

IEnumerator MainLoop()
{
    StartCoroutine(SubTaskA()); // 独立启动
    StartCoroutine(SubTaskB()); // 独立启动
    yield return new WaitForSeconds(10f);
}

void Cancel()
{
    StopCoroutine(_mainRoutine);
    // SubTaskA和SubTaskB会继续运行！
}
```

RIGHT：
```csharp
private Coroutine _mainRoutine;
private Coroutine _subA;
private Coroutine _subB;

IEnumerator MainLoop()
{
    _subA = StartCoroutine(SubTaskA());
    _subB = StartCoroutine(SubTaskB());
    yield return new WaitForSeconds(10f);
}

void Cancel()
{
    // 必须单独停止每个协程
    if (_mainRoutine != null) StopCoroutine(_mainRoutine);
    if (_subA != null) StopCoroutine(_subA);
    if (_subB != null) StopCoroutine(_subB);
}

// 更好的方式：yield return子协程（父协程拥有子协程）
IEnumerator MainLoopBetter()
{
    yield return StartCoroutine(SubTaskA()); // 等待A完成后...
    yield return StartCoroutine(SubTaskB()); // 等待B完成
    // 停止MainLoopBetter也会停止当前正在yield的子协程
}
```

GOTCHA：`StartCoroutine(SubTask())`会启动一个**独立**的协程。`StopCoroutine`仅会停止指定的协程。但：`yield return StartCoroutine(SubTask())`会让父协程等待子协程完成，并且停止父协程也会停止正在yield的子协程。关键区别：不带`yield return`的`StartCoroutine` = 即发即弃；带`yield return` = 由父协程拥有。对于复杂的取消树，优先使用带有`CancellationToken`的`Awaitable`。

---

## 模式：async void vs async Awaitable

WHEN：在Unity脚本中声明异步方法

WRONG（Claude默认写法）：
```csharp
// async void：异常会导致应用崩溃且无法捕获
async void DoWork()
{
    await Awaitable.WaitForSecondsAsync(1f);
    throw new Exception("oops"); // 未处理——导致应用崩溃
}

void Start()
{
    DoWork(); // 无法在此处捕获异常
}
```

RIGHT：
```csharp
// async Awaitable：支持异常传播
async Awaitable DoWork()
{
    await Awaitable.WaitForSecondsAsync(1f);
    throw new Exception("oops"); // 异常会传播到调用者
}

async Awaitable Start()
{
    try
    {
        await DoWork(); // 在此处捕获异常
    }
    catch (Exception e)
    {
        Debug.LogError($"工作失败：{e.Message}");
    }
}

// async void仅适用于要求返回void的Unity事件处理器：
// - Button.onClick处理器
// - UnityEvent回调
// 即使如此，也要将方法体包裹在try/catch中：
async void OnButtonClicked()
{
    try
    {
        await SaveGameAsync();
    }
    catch (Exception e)
    {
        Debug.LogError(e);
    }
}
```

GOTCHA：`async void`方法会将异常传播到`SynchronizationContext`，在Unity中会记录异常并可能导致崩溃。`async Awaitable`方法会将异常传播到等待者，允许使用try/catch进行处理。Unity的生命周期方法（`Start`、`OnEnable`等）可以返回`Awaitable`——使用异步时优先选择这种方式而非`void`。

---

## 模式：并发Awaitable竞态条件

WHEN：多个异步操作修改共享状态

WRONG（Claude默认写法）：
```csharp
// 两个异步方法写入同一个字段
async Awaitable OnClickSearch(string query)
{
    var results = await SearchAsync(query); // 用户输入“cat”
    _displayedResults = results;            // 竞态：哪个查询结果会胜出？
}
// 用户快速点击两次：先“cat”后“dog”
// 如果“dog”先返回，“cat”的结果会覆盖“dog”的结果
```

RIGHT：
```csharp
private CancellationTokenSource _searchCts;

async Awaitable OnClickSearch(string query)
{
    // 取消之前的搜索
    _searchCts?.Cancel();
    _searchCts?.Dispose();
    _searchCts = new CancellationTokenSource();
    var token = _searchCts.Token;

    try
    {
        var results = await SearchAsync(query, token);
        token.ThrowIfCancellationRequested(); // 应用结果前检查是否已取消
        _displayedResults = results;          // 仅最新的搜索结果会被应用
    }
    catch (OperationCanceledException)
    {
        // 之前的搜索已取消——预期情况
    }
}

void OnDestroy()
{
    _searchCts?.Cancel();
    _searchCts?.Dispose();
}
```

GOTCHA：与协程（单线程且按帧顺序执行）不同，多个`Awaitable`链可以跨帧交错执行。“取消前一个操作”模式确保只有最新的操作会应用其结果。可以使用`CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken)`将`CancellationTokenSource`令牌与`destroyCancellationToken`关联，以实现对象销毁时的自动清理。

---

## 模式：Addressables AsyncOperationHandle泄漏

WHEN：使用Addressables加载资源但未释放

WRONG（Claude默认写法）：
```csharp
async Awaitable LoadEnemy()
{
    var handle = Addressables.LoadAssetAsync<GameObject>("enemy_prefab");
    var prefab = await handle.Task;
    Instantiate(prefab);
    // 句柄从未释放——内存泄漏！
}
```

RIGHT：
```csharp
private AsyncOperationHandle<GameObject> _enemyHandle;

async Awaitable LoadEnemy()
{
    _enemyHandle = Addressables.LoadAssetAsync<GameObject>("enemy_prefab");
    var prefab = await _enemyHandle.Task;
    Instantiate(prefab);
}

void OnDestroy()
{
    // 不再需要时释放
    if (_enemyHandle.IsValid())
        Addressables.Release(_enemyHandle);
}

// 对于实例化的对象，使用Addressables.InstantiateAsync（自动跟踪）：
async Awaitable SpawnEnemy()
{
    var handle = Addressables.InstantiateAsync("enemy_prefab", spawnPoint.position, Quaternion.identity);
    var instance = await handle.Task;
    // 完成后：使用Addressables.ReleaseInstance(instance)而非Destroy
}
```

GOTCHA：每次调用`Addressables.LoadAssetAsync`都会增加引用计数。如果不调用`Addressables.Release`，资源会永久留在内存中。`Addressables.InstantiateAsync`会自动跟踪实例——使用`Addressables.ReleaseInstance`替代`Destroy`。使用Addressables加载场景（`LoadSceneAsync`）会在场景卸载时自动释放。释放仍有活跃实例的句柄可能导致材质渲染为粉色/丢失。

---

## 模式：UniTask与Awaitable选择

WHEN：为Unity项目选择异步框架

WRONG（Claude默认写法）：
```csharp
// 在同一个方法中混合使用UniTask和Awaitable
async UniTask DoWork()
{
    await Awaitable.NextFrameAsync(); // 类型不匹配：UniTask方法中使用Awaitable
}
```

RIGHT：
```csharp
// 为每个项目选择一种异步框架：

// === 选项A：Awaitable（Unity 6+内置） ===
// 优点：无依赖，与Unity生命周期集成，对象池化（零分配）
// 缺点：工具方法有限（无WhenAll、WhenAny，无通道/队列）
async Awaitable DoWorkAwaitable()
{
    await Awaitable.NextFrameAsync(destroyCancellationToken);
    await Awaitable.WaitForSecondsAsync(1f, destroyCancellationToken);
}

// === 选项B：UniTask（第三方包：com.cysharp.unitask） ===
// 优点：API丰富（WhenAll、WhenAny、通道），PlayerLoop集成，零分配
// 缺点：外部依赖，需要学习UniTask特定模式
async UniTask DoWorkUniTask()
{
    await UniTask.NextFrame(cancellationToken: destroyCancellationToken);
    await UniTask.Delay(1000, cancellationToken: destroyCancellationToken);
    // UniTask额外功能：WhenAll、WhenAny、Channel、AsyncReactiveProperty
}

// 两者间转换（如果必须混合使用）：
// Awaitable -> UniTask：无法直接转换；使用.AsTask()作为桥梁
// UniTask -> Awaitable：无法直接转换；使用.AsTask()作为桥梁
```

GOTCHA：Awaitable是Unity 6+内置功能，无需额外包。UniTask（com.cysharp.unitask）是成熟的第三方库，功能更丰富。不要在同一代码库中混合使用两者，除非有明确的边界——它们的取消模式、对象池行为和PlayerLoop集成方式不同。如果目标是Unity 6+，Awaitable可满足大多数需求。如果需要`WhenAll`、异步LINQ或`IUniTaskAsyncEnumerable`等高级模式，使用UniTask。

---

## 反模式速查

| 反模式 | 问题 | 修复方案 |
|---|---|---|
| `await Task.Delay()` in Unity | 忽略TimeScale，无帧同步 | 使用`Awaitable.WaitForSecondsAsync()` |
| `Task.Run()` for Unity computation | 使用线程池但无法返回主线程 | 使用`Awaitable.BackgroundThreadAsync()` + `MainThreadAsync()` |
| `StopAllCoroutines()` as cleanup | 一刀切方案；会停止非自己启动的协程 | 跟踪并停止特定协程 |
| Ignoring return value of `StartCoroutine` | 后续无法取消 | 存储`Coroutine`引用 |
| `yield return new WaitForSeconds(0)` | 意图不明确，产生分配 | 使用`yield return null`（无分配） |
| `async Task` methods in MonoBehaviour | Task异常丢失，无destroyCancellationToken集成 | 使用`async Awaitable` |

## 相关技能

- **unity-scripting** —— 协程基础、Awaitable API参考、yield类型
- **unity-lifecycle** —— destroyCancellationToken、对象销毁时机
- **unity-performance** —— 异步性能分析、分配跟踪

## 额外资源

- [Awaitable API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Awaitable.html)
- [协程手册](https://docs.unity3d.com/6000.3/Documentation/Manual/Coroutines.html)
- [Addressables AsyncOperationHandle](https://docs.unity3d.com/Packages/com.unity.addressables@2.3/manual/index.html)
- [UniTask GitHub](https://github.com/Cysharp/UniTask)
