---
name: unity-input-correctness
description: Unity新输入系统正确使用模式。涵盖动作读取（triggered vs IsPressed vs WasPressedThisFrame）、动作映射切换、重绑定持久化、InputValue生命周期、PassThrough与Value模式、本地多人设备分配以及控制方案自动切换中的常见错误。模式格式：WHEN/WRONG/RIGHT/GOTCHA。基于Unity
  6.3 LTS版本。
globs:
- '**/*.cs'
- '**/*.inputactions'
tags: unity-input-system, input-correctness-patterns, multiplayer-input, control-scheme-management,
  unity-development
tags_cn: Unity新输入系统, 输入正确使用模式, 本地多人输入, 控制方案切换, Unity开发
---

# 新输入系统（Input System）——正确使用模式

> **必备技能**：`unity-input`（Input System API、动作、绑定、PlayerInput组件）

这些模式针对输入系统最常见的Bug：动作类型读取方法错误、新旧API混用、重绑定丢失、多人设备分配处理不当等。

---

## 模式：输入读取——triggered vs IsPressed vs WasPressedThisFrame

WHEN：运行时读取按钮/动作状态

WRONG（Claude默认写法）：
```csharp
// Using .triggered for continuous input (only fires once per press)
if (fireAction.triggered)
    rb.AddForce(Vector3.forward * force); // Only fires one frame, not while held

// Using .IsPressed() for one-shot actions (fires every frame while held)
if (jumpAction.IsPressed())
    Jump(); // Jumps every frame the button is held!
```

RIGHT：
```csharp
// One-shot actions (jump, interact, fire single bullet):
if (jumpAction.WasPressedThisFrame())  // True for exactly ONE frame
    Jump();

// Or use .triggered (same as WasPressedThisFrame for Button actions with default interaction)
if (jumpAction.triggered)
    Jump();

// Continuous actions (sprint, aim, hold to charge):
if (sprintAction.IsPressed())  // True every frame while held
    moveSpeed = sprintSpeed;

// Value reading (stick, mouse delta):
Vector2 moveInput = moveAction.ReadValue<Vector2>();  // Continuous value
```

GOTCHA：`.triggered`会遵循Interactions（按住、点击等）规则——仅在交互完成时触发。`.WasPressedThisFrame()`会在原始按下操作发生时触发，不受Interactions影响。`.IsPressed()`在操作超过按压阈值的每帧都会返回true。对于未设置Interactions的Button类型动作，`.triggered`与`.WasPressedThisFrame()`效果一致。对于Value类型动作，`.triggered`会在值从0变为非0时触发。

---

## 模式：动作映射切换

WHEN：在动作映射间切换（例如：游戏玩法→UI→载具）

WRONG（Claude默认写法）：
```csharp
// Forgetting that SwitchCurrentActionMap disables the previous map
playerInput.SwitchCurrentActionMap("UI");
// All "Gameplay" actions are now DISABLED -- callbacks won't fire
// If you cached Gameplay actions, they silently stop working
```

RIGHT：
```csharp
// Option 1: Via PlayerInput (handles enable/disable automatically)
playerInput.SwitchCurrentActionMap("UI");
// Previous map disabled, new map enabled

// Option 2: Manual enable/disable (more control)
gameplayActions.Disable();
uiActions.Enable();

// Option 3: Keep both maps active simultaneously
// (useful for universal actions like Pause)
gameplayActions.Enable();
pauseActions.Enable(); // Both active at once
```

GOTCHA：使用`PlayerInput.SwitchCurrentActionMap`时，之前的映射会被完全禁用。从之前映射缓存的所有`InputAction`引用会停止触发回调，直到重新启用。如果需要某些动作（如暂停）在所有映射中都能生效，可将其放在单独的映射中保持启用状态，或使用手动启用/禁用的方式。

---

## 模式：Processor与Interaction使用混淆

WHEN：应用死区或修改输入值

WRONG（Claude默认写法）：
```csharp
// Adding a deadzone as an Interaction (Interactions modify TIMING, not values)
// In .inputactions: Action > Interactions > "Deadzone" -- this doesn't exist as an interaction
```

RIGHT：
```csharp
// Deadzones are PROCESSORS -- they modify the input VALUE
// Set in .inputactions: Binding > Processors > "Stick Deadzone" or "Axis Deadzone"

// Processors modify the value stream: Raw Input -> Processor Chain -> Final Value
// Common processors:
//   StickDeadzone   -- applies radial deadzone to Vector2 (sticks)
//   AxisDeadzone    -- applies linear deadzone to float (triggers)
//   Normalize       -- normalizes Vector2 to 0-1 range
//   Invert          -- negates the value
//   Scale           -- multiplies by a factor
//   Clamp           -- clamps to min/max range

// Runtime processor override (if needed):
moveAction.ApplyBindingOverride(new InputBinding { overrideProcessors = "StickDeadzone(min=0.2,max=0.9)" });
```

GOTCHA：**Processors**用于转换输入值（死区、归一化、缩放、反转）。**Interactions**用于改变`started`/`performed`/`canceled`事件的触发时机（按压、按住、点击、慢点击、多次点击）。混淆两者会导致死区不生效（缺少Processor）或回调时机错误（错误添加了Interaction）。

---

## 模式：SendMessages/BroadcastMessages中的InputValue生命周期

WHEN：在SendMessages或BroadcastMessages行为模式下使用`PlayerInput`

WRONG（Claude默认写法）：
```csharp
private InputValue _cachedInput; // Storing the reference

void OnMove(InputValue value)
{
    _cachedInput = value; // WRONG: InputValue is pooled and recycled
}

void Update()
{
    Vector2 dir = _cachedInput.Get<Vector2>(); // May return stale or corrupt data
}
```

RIGHT：
```csharp
private Vector2 _moveInput;

void OnMove(InputValue value)
{
    // Copy the value immediately -- InputValue is only valid during the callback
    _moveInput = value.Get<Vector2>();
}

void Update()
{
    transform.Translate(_moveInput * speed * Time.deltaTime);
}
```

GOTCHA：`InputValue`是一个会被复用的包装类，其内部数据仅在回调执行期间有效。务必在回调中使用`.Get<T>()`复制值并保存结果。这一规则适用于SendMessages和BroadcastMessages模式。UnityEvents和C# Events模式不使用`InputValue`——它们传递的`InputAction.CallbackContext`同样有生命周期限制。

---

## 模式：多源输入的PassThrough vs Value模式

WHEN：处理多个同时输入源（多点触控、多个游戏手柄）

WRONG（Claude默认写法）：
```csharp
// Using "Value" action type for multi-touch
// Value type performs disambiguation -- picks the input with highest magnitude
// You only see ONE touch, even if multiple fingers are on screen
```

RIGHT：
```csharp
// Use "PassThrough" action type for all-source input
// PassThrough does NOT disambiguate -- every input source triggers the action

// In .inputactions file: Set Action Type = "Pass Through"
// This is essential for:
//   - Multi-touch (each finger fires separately)
//   - Multiple gamepads sending the same action
//   - Combining keyboard + mouse simultaneously

// Read which device triggered it:
void OnAction(InputAction.CallbackContext ctx)
{
    var device = ctx.control.device;
    var value = ctx.ReadValue<float>();
}
```

GOTCHA：**Button**：按下/释放时触发，返回0或1的浮点数。**Value**：值变化时触发，选择幅度最大的输入源（消歧）。**PassThrough**：每个输入源的每次变化都会触发，不消歧。对于大多数游戏玩法输入，`Value`模式是正确的。仅当需要跟踪每个设备或每个手指的输入时，才使用`PassThrough`模式。

---

## 模式：动作启用/禁用范围

WHEN：启用/禁用单个动作与整个动作映射

WRONG（Claude默认写法）：
```csharp
// Enabling an action without enabling its map
fireAction.Enable(); // Works, BUT...
// If the map was disabled, this implicitly enables JUST this action
// Other actions in the same map remain disabled
```

RIGHT：
```csharp
// Preferred: Enable/disable at the MAP level
playerActions.Enable();  // Enables all actions in the map
playerActions.Disable(); // Disables all actions

// Individual action enable/disable (advanced use only):
fireAction.Enable();  // Enables this action even if map is disabled
fireAction.Disable(); // Disables only this action

// Check state:
bool mapEnabled = playerActions.enabled;
bool actionEnabled = fireAction.enabled;
```

GOTCHA：动作可以在其所属映射“禁用”的状态下单独启用——该动作仍可正常工作。但这会造成混淆的状态：`map.enabled`返回false，而`action.enabled`返回true。最佳实践：始终在映射级别进行启用/禁用操作。仅在特殊场景下使用单个动作的启用/禁用，例如换弹时临时禁用开火动作。

---

## 模式：设备特定按钮提示

WHEN：向玩家显示控制提示（例如：“Press X to interact”）

WRONG（Claude默认写法）：
```csharp
// Hardcoded button names
promptText.text = "Press A to Jump";
// Wrong on keyboard (should be "Space"), PS5 (should be "Cross"), etc.
```

RIGHT：
```csharp
// Get the display name for the current binding
InputAction jumpAction = inputActions.FindAction("Jump");

// Get display string for the active control scheme
string displayName = jumpAction.GetBindingDisplayString(
    InputBinding.DisplayStringOptions.DontOmitDevice);
promptText.text = $"Press {displayName} to Jump";

// For a specific control scheme:
int bindingIndex = jumpAction.GetBindingIndex(
    InputBinding.MaskByGroup("Gamepad"));
if (bindingIndex >= 0)
{
    string gamepadPrompt = jumpAction.GetBindingDisplayString(bindingIndex);
    // Returns "Button South" or device-specific name
}
```

GOTCHA：`GetBindingDisplayString()`返回人类可读的名称。不带参数时，返回第一个绑定的字符串。使用绑定掩码或索引可针对特定控制方案。如需完整图标支持，需自定义`InputBindingComposite`或使用能将控制路径映射到精灵/图标的资源——Unity未提供内置图标映射功能。

---

## 模式：本地多人设备分配

WHEN：支持同一台机器上多个玩家使用不同控制器

WRONG（Claude默认写法）：
```csharp
// Both players reading from the same static device reference
Vector2 p1Move = Gamepad.current.leftStick.ReadValue();
Vector2 p2Move = Gamepad.current.leftStick.ReadValue(); // Same gamepad!
```

RIGHT：
```csharp
// Use PlayerInputManager for automatic device assignment
// 1. Add PlayerInputManager component to a manager object
// 2. Set Join Behavior (e.g., JoinPlayersWhenButtonIsPressed)
// 3. Set Player Prefab (must have PlayerInput component)
// PlayerInputManager automatically assigns unique devices to each player

// In the player script:
public class PlayerController : MonoBehaviour
{
    private PlayerInput _playerInput;
    private InputAction _moveAction;

    void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
        _moveAction = _playerInput.actions["Move"];
    }

    void Update()
    {
        // Each PlayerInput instance reads from its ASSIGNED device only
        Vector2 move = _moveAction.ReadValue<Vector2>();
        transform.Translate(move * speed * Time.deltaTime);
    }
}

// Listen for join/leave events:
void OnEnable()
{
    PlayerInputManager.instance.onPlayerJoined += OnPlayerJoined;
    PlayerInputManager.instance.onPlayerLeft += OnPlayerLeft;
}
```

GOTCHA：`Gamepad.current`返回最近使用的游戏手柄——并非特定玩家的游戏手柄。对于多人游戏，务必通过`PlayerInput`组件读取输入，该组件会管理设备分配。`PlayerInputManager.instance.maxPlayerCount`用于限制玩家数量。分屏显示通过`PlayerInput.camera`分配处理——每个玩家会获得一个带有不同视口矩形的相机。

---

## 模式：控制方案自动切换

WHEN：玩家在游戏中途在键盘和游戏手柄间切换

WRONG（Claude默认写法）：
```csharp
// Assuming the control scheme is fixed after startup
// UI shows keyboard prompts even after player picks up a gamepad
```

RIGHT：
```csharp
public class ControlSchemeHandler : MonoBehaviour
{
    private PlayerInput _playerInput;

    void OnEnable()
    {
        _playerInput = GetComponent<PlayerInput>();
        _playerInput.controlsChangedEvent.AddListener(OnControlsChanged);
        // Initialize with current scheme
        UpdatePrompts(_playerInput.currentControlScheme);
    }

    void OnDisable()
    {
        _playerInput.controlsChangedEvent.RemoveListener(OnControlsChanged);
    }

    void OnControlsChanged(PlayerInput input)
    {
        UpdatePrompts(input.currentControlScheme);
    }

    void UpdatePrompts(string schemeName)
    {
        bool isGamepad = schemeName == "Gamepad";
        // Update UI prompts, button icons, etc.
        promptIcon.sprite = isGamepad ? gamepadSprite : keyboardSprite;
    }
}
```

GOTCHA：当`PlayerInput`检测到不同设备类型的输入时，会自动切换控制方案。`controlsChangedEvent`会在每次切换时触发。`currentControlScheme`返回与`.inputactions`中控制方案名称匹配的字符串。切换会在下一次输入事件时发生，而非设备连接后立即触发。需同时连接两种设备进行测试。

---

## 反模式快速参考

| 反模式 | 问题 | 修复方案 |
|---|---|---|
| `Input.GetKey`与新输入系统混用 | 新旧API冲突；可能需要同时启用两个后端 | 完全迁移到新输入系统；移除`using UnityEngine.Input` |
| 未调用`action.Enable()` | 动作无响应；无报错信息 | 在读取前启用动作映射或单个动作 |
| 使用错误类型`T`调用`.ReadValue<T>()` | 静默返回默认值 | 确保`T`与动作的Control Type匹配（摇杆用Vector2，按钮用float） |
| 未销毁`PerformInteractiveRebinding` | 内存泄漏 | 在`.Start()`完成或取消后务必调用`.Dispose()` |
| 使用旧版`OnGUI`处理输入 | IMGUI与输入系统混用 | 使用UI Toolkit或输入系统回调 |
| 未保存重绑定覆盖设置 | 玩家重启后丢失自定义绑定 | 使用`SaveBindingOverridesAsJson`保存，在Awake中加载 |

## 相关技能

- **unity-input** —— 输入系统API参考、动作类型、绑定语法、设备访问
- **unity-ui** —— UI Toolkit输入处理、导航事件
- **unity-multiplayer** —— 网络代码输入权限、客户端预测

## 额外资源

- [Input System 官方手册](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/index.html)
- [InputAction API](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/api/UnityEngine.InputSystem.InputAction.html)
- [PlayerInput API](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/api/UnityEngine.InputSystem.PlayerInput.html)
- [PlayerInputManager](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/api/UnityEngine.InputSystem.PlayerInputManager.html)
- [交互式重绑定](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/ActionBindings.html#interactive-rebinding)