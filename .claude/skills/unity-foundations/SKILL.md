---
name: unity-foundations
description: Unity 6 核心概念入门。覆盖 GameObject/Component/Transform/Scene/Prefab/ScriptableObject、实体组件架构、对象层级、Tag/Layer、项目结构规范。触发：Unity 入门、GameObject、Component、Prefab、Scene、ScriptableObject、Tag、Layer。❌ 不适用：Editor REST API / DataTable / UI 全流程，请用 unity-dev。
tags: unity6, unity-development, csharp-scripting, game-development, unity-architecture
tags_cn: Unity 6, Unity开发, C#脚本, 游戏开发, Unity架构
---

# Unity 基础

## 核心概念

### GameObjects

GameObjects是Unity中的基础构建块。场景中的每一个对象——角色、道具、场景元素、相机、灯光——都是GameObject。GameObject属于容器类对象：它们无法独立运作，需要挂载**Components**来获得功能。每个GameObject都会自动包含一个不可移除的**Transform**组件。

### Components

Components是每个GameObject的功能模块。Unity采用**组合优于继承**的架构：你可以通过为GameObject挂载多个组件来构建所需行为，而非从深层类继承。每个GameObject必须且只能有一个Transform组件，其他附加组件（Rigidbody、Collider、MeshRenderer、自定义MonoBehaviours）则定义了对象的具体功能。

来自官方文档的约束：
- 组件必须与其目标GameObject存放在同一个项目中
- 组件不能来自其他项目、未挂载的脚本或未安装的包

### Transforms

Transform组件存储**位置**、**旋转**和**缩放**信息——这些数值可以是相对于父对象的（局部坐标），也可以是相对于世界原点的（世界坐标）。官方文档给出的核心要点：

- 子物体的Transform显示的是相对于父物体的数值
- 根GameObject（无父对象）显示的是世界坐标
- 物理引擎默认1单位 = 1米
- 添加子物体前先将父对象位置设为`(0,0,0)`，这样局部坐标会和全局坐标保持一致
- 避免在运行时调整Transform缩放，建议在建模时就按照真实比例制作资源。非等比缩放会导致Collider、灯光、音频源出现异常

### Scenes

Scenes是包含游戏或应用全部/部分内容的资源。默认新建的场景会包含一个相机和一个方向光。项目可以使用单场景或多场景（比如每个关卡对应一个场景），场景模板可以作为创建新场景的蓝图使用。

### Prefabs

Prefabs是可复用的资源模板，存储了完整的GameObject配置（所有组件、属性值和子GameObject）。核心特性：
- **嵌套Prefabs**：可以在其他Prefabs中嵌入Prefab实例
- **Prefab变体**：可以创建预定义的变体，同时保留与基础Prefab的关联关系
- **覆盖**：可以修改特定实例的组件/数据，且不会影响模板本身
- **解包**：将Prefab实例转换为独立的GameObject

### ScriptableObjects

ScriptableObject是继承自`UnityEngine.Object`的可序列化Unity类型，作为独立于GameObject的数据容器使用。和MonoBehaviours不同，ScriptableObject是以项目级`.asset`文件的形式存在的。主要使用场景：
- 共享数据容器（通过引用同一个资源减少内存占用，避免在多个Prefab中重复存储相同数据）
- 编辑器工具基础（EditorTool、EditorWindow都继承自ScriptableObject）
- 运行时配置存储

**重要提示**：在编辑模式下通过脚本修改ScriptableObject时，Unity不会自动保存变更，你需要在修改后调用`EditorUtility.SetDirty()`。

### 标签（Tags）

标签是分配给GameObject的参考标识符，用于脚本逻辑编写。每个GameObject只能有一个标签，但多个GameObject可以共用同一个标签。内置标签包括：`Untagged`、`Respawn`、`Finish`、`EditorOnly`、`MainCamera`、`Player`、`GameController`。

- `MainCamera`：编辑器会对该标签的对象做缓存，`Camera.main`会返回第一个符合条件的结果
- `EditorOnly`：带有该标签的GameObject会在构建版本时被销毁
- 标签名称一旦创建就无法重命名

### 图层（Layers）

图层用于对GameObject做分类，实现选择性处理，包括相机渲染、光照、物理碰撞和自定义代码逻辑。Unity最多支持32个图层，LayerMask可以定义API调用需要交互的图层范围。

---

## 常用模式

### 组件的创建和访问

```csharp
using UnityEngine;

public class ComponentAccess : MonoBehaviour
{
    void Start()
    {
        // 获取当前GameObject上的组件
        Rigidbody rb = GetComponent<Rigidbody>();

        // 获取子GameObject上的组件
        Collider childCollider = GetComponentInChildren<Collider>();

        // 获取当前对象及其子对象上所有对应类型的组件
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();

        // 运行时添加组件
        BoxCollider box = gameObject.AddComponent<BoxCollider>();

        // 移除组件（销毁组件）
        Destroy(box);
    }
}
```

### 查找GameObjects

```csharp
using UnityEngine;

public class FindingObjects : MonoBehaviour
{
    void Start()
    {
        // 通过标签查找（返回第一个匹配结果）
        GameObject player = GameObject.FindWithTag("Player");

        // 查找所有带有对应标签的对象
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

        // 通过名称查找（性能差，避免在Update中使用）
        GameObject manager = GameObject.Find("GameManager");

        // 高效的标签对比（无GC分配）
        if (gameObject.CompareTag("Player"))
        {
            Debug.Log("This is the player");
        }
    }
}
```

### 实例化Prefabs

来自Unity官方文档的示例：

```csharp
using UnityEngine;

public class InstantiationExample : MonoBehaviour
{
    // Prefab的引用，在Inspector面板中拖拽Prefab到该字段赋值
    public GameObject myPrefab;

    void Start()
    {
        // 在(0, 0, 0)位置、零旋转角度实例化对象
        Instantiate(myPrefab, new Vector3(0, 0, 0), Quaternion.identity);
    }
}
```

### 带父Transform的实例化

```csharp
using UnityEngine;

public class SpawnWithParent : MonoBehaviour
{
    public GameObject prefab;
    public Transform parentTransform;

    void SpawnChild()
    {
        // 实例化为父Transform的子对象
        GameObject instance = Instantiate(prefab, parentTransform);

        // 在父对象下的指定世界位置实例化
        GameObject positioned = Instantiate(
            prefab,
            new Vector3(5, 0, 0),
            Quaternion.identity,
            parentTransform
        );
    }
}
```

### ScriptableObject数据容器

来自Unity官方文档：

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/SpawnData", order = 1)]
public class SpawnDataScriptableObject : ScriptableObject
{
    public GameObject prefab;
    public int count;
    public Vector3[] positions;
}
```

```csharp
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public SpawnDataScriptableObject spawnData;

    void Start()
    {
        for (int i = 0; i < spawnData.count; i++)
        {
            Instantiate(spawnData.prefab, spawnData.positions[i], Quaternion.identity);
        }
    }
}
```

### 场景管理

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    void Start()
    {
        // 订阅场景加载完成事件
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log("Loaded: " + scene.name);
    }

    public void LoadLevel(string sceneName)
    {
        // 通过名称加载场景（替换当前场景）
        SceneManager.LoadScene(sceneName);
    }

    public void LoadAdditive(string sceneName)
    {
        // 叠加式加载场景（保留当前已加载的场景）
        SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
    }

    public void UnloadLevel(string sceneName)
    {
        SceneManager.UnloadSceneAsync(sceneName);
    }

    public void GetSceneInfo()
    {
        Scene active = SceneManager.GetActiveScene();
        Debug.Log("Active scene: " + active.name);
        Debug.Log("Loaded scene count: " + SceneManager.loadedSceneCount);
    }
}
```

### 基于标签的生成

来自Unity官方文档示例：

```csharp
using UnityEngine;

public class RespawnSystem : MonoBehaviour
{
    public GameObject respawnPrefab;
    private GameObject respawn;

    void Update()
    {
        if (respawn == null)
            respawn = GameObject.FindWithTag("Respawn");

        if (respawn != null)
        {
            Instantiate(respawnPrefab, respawn.transform.position,
                respawn.transform.rotation);
        }
    }
}
```

### 激活和停用GameObjects

```csharp
using UnityEngine;

public class ToggleVisibility : MonoBehaviour
{
    public GameObject target;

    public void Toggle()
    {
        // SetActive控制GameObject是否处于激活状态
        target.SetActive(!target.activeSelf);

        // activeSelf：对象自身的激活状态
        // activeInHierarchy：实际生效的状态（考虑父对象链的状态）
        Debug.Log("Self: " + target.activeSelf);
        Debug.Log("InHierarchy: " + target.activeInHierarchy);
    }
}
```

### 射线检测的Layer Mask使用

```csharp
using UnityEngine;

public class LayerRaycast : MonoBehaviour
{
    void Update()
    {
        // 为名为"Ground"的图层创建layer mask
        int groundLayer = LayerMask.NameToLayer("Ground");
        int layerMask = 1 << groundLayer;

        // 仅对Ground图层进行射线检测
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 100f, layerMask))
        {
            Debug.Log("Hit ground at: " + hit.point);
        }

        // 使用GetMask处理多个图层
        int combinedMask = LayerMask.GetMask("Ground", "Water");
        Physics.Raycast(transform.position, Vector3.forward, 50f, combinedMask);
    }
}
```

---

## 反模式

### 1. 在Update中使用GameObject.Find

```csharp
// 错误写法：Find性能开销大，每帧执行字符串查找
void Update()
{
    GameObject player = GameObject.Find("Player"); // 避免这么写！
}

// 正确写法：缓存引用
private GameObject player;
void Start()
{
    player = GameObject.FindWithTag("Player");
}
```

### 2. 非等比Transform缩放

来自官方文档：“不要在Transform组件中调整GameObject的缩放”。非等比缩放（比如2,4,2）会导致：
- Collider、灯光、音频源运行异常
- 旋转后的子对象出现变形
- 实例化缩放对象时性能下降

建议在建模时就按照真实比例制作资源。

### 3. 在多个Prefab中重复存储数据，而不使用ScriptableObjects

```csharp
// 错误写法：每个Prefab实例都会复制一份数据
public class EnemyStats : MonoBehaviour
{
    public int health = 100;
    public float speed = 5f;
    public string enemyName = "Goblin";
}

// 正确写法：使用ScriptableObject，一份资源多处引用
[CreateAssetMenu(menuName = "ScriptableObjects/EnemyConfig")]
public class EnemyConfig : ScriptableObject
{
    public int health = 100;
    public float speed = 5f;
    public string enemyName = "Goblin";
}

public class Enemy : MonoBehaviour
{
    public EnemyConfig config; // 所有实例共用同一份资源
}
```

### 4. 修改ScriptableObject后忘记调用EditorUtility.SetDirty

```csharp
// 错误写法：编辑模式下的修改不会持久化保存
settings.value += 10;

// 正确写法：标记资源为已修改，让Unity执行保存
settings.value += 10;
EditorUtility.SetDirty(settings);
```

### 5. 基于字符串的标签对比

```csharp
// 错误写法：对比时会分配新字符串，带来GC压力
if (gameObject.tag == "Player") { }

// 正确写法：CompareTag不会产生内存分配
if (gameObject.CompareTag("Player")) { }
```

### 6. 忽略父子Transform关系

添加子物体前先将父对象位置设置为`(0,0,0)`，否则子物体的局部坐标会和全局坐标不匹配，导致定位对象时出现混乱。

---

## 核心API速查

| API | 描述 | 注意事项 |
|-----|-------------|-------|
| `GetComponent<T>()` | 获取当前GameObject上的对应组件 | 未找到时返回null |
| `GetComponentInChildren<T>()` | 获取自身或子对象上的对应组件 | 深度优先搜索 |
| `GetComponentsInChildren<T>()` | 获取层级中所有匹配的组件 | 返回数组 |
| `AddComponent<T>()` | 运行时挂载新组件 | 返回新创建的组件 |
| `Destroy(obj)` | 销毁GameObject或组件 | 延迟到帧末尾执行 |
| `Instantiate(prefab, pos, rot)` | 在指定位置/旋转角度克隆Prefab | 返回克隆后的对象 |
| `GameObject.FindWithTag(tag)` | 查找第一个带有对应标签的激活GO | 未找到返回null |
| `GameObject.FindGameObjectsWithTag(tag)` | 查找所有带有对应标签的激活GO | 返回数组 |
| `GameObject.Find(name)` | 通过名称查找（性能开销大） | 避免在Update循环中使用 |
| `gameObject.SetActive(bool)` | 激活/停用对象 | 会禁用所有组件 |
| `gameObject.CompareTag(tag)` | 无GC分配的标签对比 | 优先使用该方式替代`== tag` |
| `SceneManager.LoadScene(name)` | 加载场景（替换当前场景） | 场景必须在构建设置中 |
| `SceneManager.LoadSceneAsync(name, mode)` | 异步加载场景 | 支持叠加或单例模式 |
| `SceneManager.UnloadSceneAsync(name)` | 卸载已加载的场景 | 返回AsyncOperation |
| `SceneManager.GetActiveScene()` | 获取当前激活的场景 | 返回Scene结构体 |
| `LayerMask.NameToLayer(name)` | 通过图层名称获取图层索引 | 返回整数 |
| `LayerMask.GetMask(names)` | 通过多个图层名称获取组合mask | 支持字符串数组参数 |
| `Camera.main` | 获取带MainCamera标签的相机 | Unity会做缓存 |

---

## 相关技能

- **unity-scripting**——C#脚本模式、MonoBehaviour生命周期、协程、事件
- **unity-physics**——Rigidbody、Collider、物理图层、射线检测、触发器
- **unity-editor-tools**——自定义Inspector、编辑器窗口、Gizmos、构建流水线

---

## 额外资源

- [GameObjects](https://docs.unity3d.com/6000.3/Documentation/Manual/GameObjects.html)
- [Components](https://docs.unity3d.com/6000.3/Documentation/Manual/Components.html)
- [Transform](https://docs.unity3d.com/6000.3/Documentation/Manual/class-Transform.html)
- [Prefabs](https://docs.unity3d.com/6000.3/Documentation/Manual/Prefabs.html)
- [Scenes](https://docs.unity3d.com/6000.3/Documentation/Manual/CreatingScenes.html)
- [ScriptableObject](https://docs.unity3d.com/6000.3/Documentation/Manual/class-ScriptableObject.html)
- [Tags](https://docs.unity3d.com/6000.3/Documentation/Manual/Tags.html)
- [Layers](https://docs.unity3d.com/6000.3/Documentation/Manual/Layers.html)
- [Execution Order](https://docs.unity3d.com/6000.3/Documentation/Manual/execution-order.html)
- [SceneManager API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SceneManagement.SceneManager.html)
