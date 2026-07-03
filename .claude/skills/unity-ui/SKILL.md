---
name: unity-ui
description: Unity 6 UI开发指南。适用于构建用户界面、菜单、HUD、按钮或任何UI元素场景。涵盖UI Toolkit（推荐用于新项目——包含USS、UXML、UI
  Builder、数据绑定）、uGUI/Canvas（传统运行时UI）以及IMGUI。基于Unity 6.3 LTS文档编写。
tags: unity-ui-development, ui-toolkit, ugui, imgui, unity-6
tags_cn: Unity UI开发, UI Toolkit, uGUI, IMGUI, Unity 6 指南
---

# Unity UI系统

Unity提供三种UI框架。**UI Toolkit是新项目推荐使用的系统**。uGUI仍支持传统项目及特定运行时场景。IMGUI严格用于编辑器工具开发与调试。

## UI系统对比

| 特性 | UI Toolkit | uGUI (Canvas) | IMGUI |
|---|---|---|---|
| 推荐用于新项目 | 是 | 否（传统） | 否 |
| 游戏运行时UI | 是 | 是 | 不推荐 |
| 编辑器扩展 | 是 | 否 | 是 |
| 实现方式 | 类Web技术（UXML + USS + C#） | 游戏对象+组件 | 纯代码（OnGUI） |
| 布局系统 | Flexbox（Yoga） | RectTransform + 锚点 | 即时模式 |
| 样式设置 | USS样式表 | 组件独立属性 | GUIStyle / GUISkin |
| 可视化创作 | UI Builder | 场景视图 | 无 |
| 性能 | 优化的保留模式 | Canvas批处理 | 每帧重绘 |
| 数据绑定 | SerializedObject + 运行时绑定 | 代码手动实现 | 代码手动实现 |
| 世界空间UI | 支持 | Canvas世界空间模式 | 不支持 |
| 输入集成 | 指针/键盘事件 | EventSystem + 射线检测 | Event.current |

**选择指南：**
- 新的运行时UI（菜单、HUD、背包）→ **UI Toolkit**
- 新的编辑器窗口/检视面板 → **UI Toolkit**
- 已有uGUI的项目 → 继续使用**uGUI**，逐步迁移
- 编辑器中快速调试覆盖层 → **IMGUI**
- 3D对象上的世界空间UI → **UI Toolkit** 或 **uGUI世界空间Canvas**

---

## UI Toolkit

UI Toolkit是Unity受Web技术启发的现代UI框架。它使用UXML定义结构，USS设置样式，C#编写逻辑。

### 核心架构

```
UIDocument (MonoBehaviour on GameObject)
  --> VisualTreeAsset (.uxml)  -- 定义结构
  --> StyleSheet (.uss)         -- 定义外观
  --> C# script                 -- 定义行为
```

所有UI元素都继承自`VisualElement`。可通过`rootVisualElement`访问根元素。

### UXML结构

UXML声明式定义UI层级：

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
    <ui:Style src="MainMenu.uss" />
    <ui:VisualElement name="root-container" class="container">
        <ui:Label text="Game Menu" class="title" />
        <ui:Button text="Play" name="play-button" class="menu-btn" />
        <ui:Button text="Settings" name="settings-button" class="menu-btn" />
        <ui:Toggle label="Fullscreen" name="fullscreen-toggle" />
        <ui:Slider label="Volume" low-value="0" high-value="100" name="volume-slider" />
        <ui:TextField label="Player Name" name="player-name" />
    </ui:VisualElement>
</ui:UXML>
```

关键点：
- `xmlns:ui="UnityEngine.UIElements"`是标准命名空间
- 使用`<ui:Style src="..." />`引用USS文件
- `name`属性用于C#查询，`class`用于USS样式设置
- 可导入模板：`<ui:Template src="other.uxml" name="other" />`

### USS样式

USS使用类CSS语法，并带有Unity特定扩展。所有Unity专属特性的USS属性均使用`-unity-`前缀。

```css
/* Type selector */
Button {
    background-color: #2D2D2D;
    border-radius: 4px;
    padding: 8px 16px;
    -unity-font-style: bold;
}

/* Class selector */
.menu-btn {
    width: 200px;
    height: 40px;
    margin: 4px 0;
    font-size: 16px;
    color: #FFFFFF;
}

/* Name selector */
#play-button {
    background-color: #4CAF50;
}

/* Pseudo-class */
.menu-btn:hover {
    background-color: #555555;
    scale: 1.05 1.05;
}

.menu-btn:active {
    background-color: #333333;
}

.menu-btn:disabled {
    opacity: 0.5;
}

/* Descendant selector */
.container > Label {
    -unity-text-align: middle-center;
}

/* USS variables */
:root {
    --primary-color: #4CAF50;
    --font-large: 24px;
}

.title {
    color: var(--primary-color);
    font-size: var(--font-large);
}
```

**选择器类型：** 类型选择器（`Button`）、名称选择器（`#name`）、类选择器（`.class`）、通用选择器（`*`）、后代选择器（`A B`）、子选择器（`A > B`）、复合选择器（`A.class`）、伪类（`:hover`, `:active`, `:focus`, `:disabled`, `:checked`）。

**布局基于Flexbox：** 使用`flex-direction`、`flex-grow`、`flex-shrink`、`justify-content`、`align-items`、`align-self`、`flex-wrap`。默认方向为`column`。

### C#设置与交互

```csharp
using UnityEngine;
using UnityEngine.UIElements;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private Button playButton;
    private Button settingsButton;
    private Toggle fullscreenToggle;
    private Slider volumeSlider;
    private TextField playerNameField;

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;

        // 通过名称查询单个元素
        playButton = root.Q<Button>("play-button");
        settingsButton = root.Q<Button>("settings-button");
        fullscreenToggle = root.Q<Toggle>("fullscreen-toggle");
        volumeSlider = root.Q<Slider>("volume-slider");
        playerNameField = root.Q<TextField>("player-name");

        // 注册点击回调
        playButton.RegisterCallback<ClickEvent>(OnPlayClicked);
        settingsButton.RegisterCallback<ClickEvent>(OnSettingsClicked);

        // 注册值变更回调
        fullscreenToggle.RegisterValueChangedCallback(OnFullscreenChanged);
        volumeSlider.RegisterValueChangedCallback(OnVolumeChanged);

        // 通过类查询多个元素
        var allButtons = root.Query<Button>(className: "menu-btn").ToList();
    }

    private void OnDisable()
    {
        playButton.UnregisterCallback<ClickEvent>(OnPlayClicked);
        settingsButton.UnregisterCallback<ClickEvent>(OnSettingsClicked);
        fullscreenToggle.UnregisterValueChangedCallback(OnFullscreenChanged);
        volumeSlider.UnregisterValueChangedCallback(OnVolumeChanged);
    }

    private void OnPlayClicked(ClickEvent evt) => Debug.Log("Play clicked");
    private void OnSettingsClicked(ClickEvent evt) => Debug.Log("Settings clicked");

    private void OnFullscreenChanged(ChangeEvent<bool> evt)
    {
        Screen.fullScreen = evt.newValue;
    }

    private void OnVolumeChanged(ChangeEvent<float> evt)
    {
        AudioListener.volume = evt.newValue / 100f;
    }
}
```

**程序化创建UI（无需UXML）：**

```csharp
private void CreateUIFromCode()
{
    var root = uiDocument.rootVisualElement;

    var container = new VisualElement();
    container.AddToClassList("container");
    root.Add(container);

    var label = new Label("Created from C#");
    container.Add(label);

    var button = new Button(() => Debug.Log("Clicked")) { text = "Click Me" };
    button.name = "dynamic-button";
    container.Add(button);
}
```

### 事件系统

UI Toolkit事件分为两个传播阶段：
1. **向下渗透** -- 从根元素到目标元素
2. **向上冒泡** -- 从目标元素返回根元素

```csharp
// 默认：向上冒泡阶段
element.RegisterCallback<PointerDownEvent>(OnPointerDown);

// 向下渗透阶段（父元素先于子元素响应）
element.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);

// 向回调传递自定义数据
element.RegisterCallback<ClickEvent, string>(OnClickWithData, "my-data");

// 设置值但不触发ChangeEvent
myControl.SetValueWithoutNotify(newValue);
```

### 数据绑定

**SerializedObject绑定（编辑器/检视面板UI）：**

```csharp
// 在UXML中：<ui:IntegerField binding-path="m_Health" label="Health" />
// 在C#中：
var healthField = new IntegerField("Health") { bindingPath = "m_Health" };
root.Add(healthField);
root.Bind(new SerializedObject(targetComponent));
```

可绑定对象：MonoBehaviour、ScriptableObject、Unity原生类型、基本数据类型。仅`INotifyValueChanged`元素的`value`属性可被绑定。

**运行时绑定**将普通C#对象与UI控件关联，在编辑器和运行时环境均有效。可在元素上设置数据源，并定义绑定模式以指定同步方向。

参考：[references/ui-data-binding.md](references/ui-data-binding.md)

### 操作器（Manipulators）

操作器封装了事件处理逻辑，将交互与UI代码分离：

```csharp
public class DragManipulator : PointerManipulator
{
    private Vector3 startPosition;
    private bool isDragging;

    public DragManipulator(VisualElement target)
    {
        this.target = target;
    }

    protected override void RegisterCallbacksOnTarget()
    {
        target.RegisterCallback<PointerDownEvent>(OnPointerDown);
        target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        target.RegisterCallback<PointerUpEvent>(OnPointerUp);
    }

    protected override void UnregisterCallbacksFromTarget()
    {
        target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
        target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
        target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        startPosition = evt.position;
        isDragging = true;
        target.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (!isDragging) return;
        var delta = evt.position - startPosition;
        target.transform.position += (Vector3)delta;
        startPosition = evt.position;
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        isDragging = false;
        target.ReleasePointer(evt.pointerId);
        evt.StopPropagation();
    }
}

// 使用方式：
myElement.AddManipulator(new DragManipulator(myElement));
```

**内置操作器类：** `Manipulator`（基类）、`PointerManipulator`、`MouseManipulator`、`Clickable`、`ContextualMenuManipulator`、`KeyboardNavigationManipulator`。

### 自定义控件

```csharp
// Unity 6+推荐模式：[UxmlElement]属性（替代已弃用的UxmlFactory/UxmlTraits）
[UxmlElement]
public partial class HealthBar : VisualElement
{
    [UxmlAttribute]
    public float MaxHealth { get; set; } = 100f;

    private VisualElement fillBar;
    private float currentHealth;

    public float CurrentHealth
    {
        get => currentHealth;
        set
        {
            currentHealth = Mathf.Clamp(value, 0, MaxHealth);
            fillBar.style.width = Length.Percent(currentHealth / MaxHealth * 100f);
        }
    }

    public HealthBar()
    {
        AddToClassList("health-bar");
        fillBar = new VisualElement();
        fillBar.AddToClassList("health-bar__fill");
        Add(fillBar);
    }
}
```

---

## uGUI / Canvas系统（传统）

uGUI是Unity基于游戏对象的旧版UI系统。它使用Canvas、RectTransform和EventSystem。

### Canvas渲染模式

| 模式 | 描述 | 使用场景 |
|---|---|---|
| **屏幕空间 - 覆盖** | 渲染在所有内容上方，随屏幕缩放 | 标准HUD、菜单 |
| **屏幕空间 - 相机** | 由指定相机渲染，受透视影响 | 带有深度效果的UI |
| **世界空间** | Canvas作为场景中的3D对象 | 场景内显示屏、VR UI |

### 核心组件

**视觉类：** Text、Image、RawImage
**交互类：** Button、Toggle、ToggleGroup、Slider、Scrollbar、Dropdown、InputField、ScrollRect
**布局类：** HorizontalLayoutGroup、VerticalLayoutGroup、GridLayoutGroup、ContentSizeFitter、AspectRatioFitter、LayoutElement

### RectTransform与锚点

所有uGUI元素使用RectTransform而非Transform。锚点定义元素相对于父元素的定位方式：
- Anchor Min/Max为比例值（0.0=左/下，1.0=右/上）
- 锚点重合：固定位置（Pos X、Pos Y、宽度、高度）
- 锚点分离：拉伸模式（左、右、上、下内边距）
- Pivot：旋转和缩放的中心点

### uGUI示例

```csharp
using UnityEngine;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Toggle muteToggle;

    private void OnEnable()
    {
        playButton.onClick.AddListener(OnPlayClicked);
        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        muteToggle.onValueChanged.AddListener(OnMuteToggled);
    }

    private void OnDisable()
    {
        playButton.onClick.RemoveListener(OnPlayClicked);
        volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
        muteToggle.onValueChanged.RemoveListener(OnMuteToggled);
    }

    private void OnPlayClicked() => Debug.Log("Play");
    private void OnVolumeChanged(float value) => AudioListener.volume = value;
    private void OnMuteToggled(bool muted) => AudioListener.pause = muted;
}
```

### 绘制顺序

元素按层级顺序渲染：第一个子元素最先绘制，最后一个子元素绘制在最上层。可使用`Transform.SetAsFirstSibling()`、`SetAsLastSibling()`、`SetSiblingIndex()`调整顺序。

参考：[references/ugui-legacy.md](references/ugui-legacy.md)

---

## 反模式

| 反模式 | 问题 | 正确做法 |
|---|---|---|
| 随处使用内联样式 | 每个元素都有内存开销 | 使用USS文件管理共享样式 |
| 复杂USS中使用通用选择器（`A * B`） | 大规模场景下选择器性能差 | 使用BEM命名规范、子选择器 |
| 在包含大量子元素的元素上使用`:hover` | 鼠标移动会导致整个层级失效 | 仅在叶子元素上使用`:hover` |
| 在`CreateInspectorGUI()`中调用`Bind()` | 双重绑定，返回后会自动执行绑定 | 让自动绑定处理，或仅在手动创建的UI上调用Bind |
| 每帧重建整个UI | 失去保留模式的优势 | 仅更新发生变化的元素 |
| uGUI中多个Canvas包含动态内容 | 任何子元素变化都会导致Canvas重建批处理 | 将静态和动态UI拆分到不同Canvas |
| 不注销回调 | 内存泄漏、无效引用 | 始终在`OnDisable`或`OnDestroy`中注销回调 |
| 使用IMGUI开发游戏运行时UI | 每帧重绘，性能差 | 使用UI Toolkit或uGUI |
| uGUI场景中缺少EventSystem | 无法处理输入事件 | 确保场景中存在一个EventSystem |

---

## 核心API速查

### UI Toolkit

| API | 用途 |
|---|---|
| `UIDocument` | 承载VisualTreeAsset的MonoBehaviour |
| `rootVisualElement` | 视觉树的根元素 |
| `Q<T>("name")` | 通过名称查询单个元素 |
| `Q<T>(className: "cls")` | 通过类查询单个元素 |
| `Query<T>().ToList()` | 查询多个元素 |
| `RegisterCallback<TEvent>(callback)` | 注册事件处理器 |
| `UnregisterCallback<TEvent>(callback)` | 移除事件处理器 |
| `RegisterValueChangedCallback(callback)` | 监听值变化 |
| `SetValueWithoutNotify(value)` | 静默设置值 |
| `AddToClassList("class")` | 添加USS类 |
| `RemoveFromClassList("class")` | 移除USS类 |
| `AddManipulator(manipulator)` | 附加事件操作器 |
| `style.display = DisplayStyle.None` | 隐藏元素 |
| `style.display = DisplayStyle.Flex` | 显示元素 |
| `VisualTreeAsset.Instantiate()` | 从UXML创建实例 |
| `element.Bind(serializedObject)` | 绑定到SerializedObject |

### uGUI

| API | 用途 |
|---|---|
| `Canvas` | 所有uGUI元素的根容器 |
| `CanvasScaler` | 控制UI在不同分辨率下的缩放 |
| `GraphicRaycaster` | 启用Canvas上的输入检测 |
| `EventSystem` | 中央输入事件调度器 |
| `RectTransform` | 带有锚点和尺寸设置的Transform |
| `Button.onClick` | 点击事件的UnityEvent |
| `Toggle.onValueChanged` | 开关状态变化的UnityEvent |
| `Slider.onValueChanged` | 滑块值变化的UnityEvent |
| `LayoutGroup` | 子元素自动布局 |

---

## 相关技能

- **unity-foundations** -- GameObject、Component、MonoBehaviour生命周期
- **unity-scripting** -- C#模式、SerializeField、事件
- **unity-input** -- 输入系统与UI集成

## TextMeshPro

所有文本渲染请使用**TextMeshPro**（TMP）——而非传统的`UI.Text`。TMP使用SDF渲染，可在任何缩放级别下保持文本清晰。Canvas UI使用`TextMeshProUGUI`，3D世界文本使用`TextMeshPro`。使用`SetText("Score: {0}", value)`实现零内存分配更新。完整API、富文本标签、字体资源及使用模式请参考[references/textmeshpro.md](references/textmeshpro.md)。

## 额外资源

- [UI Toolkit](https://docs.unity3d.com/6000.3/Documentation/Manual/UIToolkits.html) | [USS](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-USS.html) | [UXML](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-UXML.html) | [数据绑定](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-Binding.html) | [操作器](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-manipulators.html)
- [uGUI Canvas](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/UICanvas.html) | [uGUI布局](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/UIBasicLayout.html)
