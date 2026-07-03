# common-pitfalls — RectTransform 常见坑

## 1. Canvas Scaler 选型

Canvas 根节点上必挂 Canvas Scaler，决定 UI 的分辨率适配策略：

| UI Scale Mode | 适用 | 代价 |
|---|---|---|
| **Constant Pixel Size** | 编辑器工具 UI / Dev Console | 完全不适配分辨率，只按像素 |
| **Scale With Screen Size**（推荐） | 游戏 UI 主战场 | 需要设 Reference Resolution + Match（0=match width, 1=match height, 0.5=平衡） |
| **Constant Physical Size** | 移动端"物理尺寸一致"的诉求 | 需要 DPI 数据，实际很少用 |

**本项目默认**：Scale With Screen Size, Reference Resolution = 1920×1080, Match = 0.5。

**layout 里必须声明**：
```markdown
## 全局约定
- Canvas Scaler: Scale With Screen Size, ReferenceResolution 1920×1080, Match 0.5
```

## 2. 父子 anchor 冲突

**情景**：父节点 anchor=stretch-all（跟屏幕拉伸），子节点 anchor=middle-center → 子节点相对父的中心定位。若父在竖屏下高度变化，子节点的绝对屏幕位置会跟着变。

**决策**：设计 layout 时**自顶向下推演**：
- 先定 Canvas 根 → 定各页面根节点 anchor → 再定子节点 anchor
- 每一层 anchor 都是相对上一层的，一层错整颗子树错

## 3. Layout Group（VerticalLayoutGroup / HorizontalLayoutGroup / GridLayoutGroup）

Layout Group 组件会**接管子节点的 anchoredPosition**（有时也接管 sizeDelta）：

- 在 Layout Group 下的子节点，手动改 anchoredPosition 会被覆盖
- 若子节点想固定尺寸 → 配合 LayoutElement 组件的 `preferredWidth/Height`
- Layout Group 通常配合 Content Size Fitter（父自适应子大小）使用

**layout 里的书写约定**：若某容器挂了 Layout Group，明确声明：

```
ButtonGroup (
  anchor: middle-center, sizeDelta: (400, 320)
  components: [
    VerticalLayoutGroup: spacing=20, padding=(20,20,20,20), childAlignment=MiddleCenter, childForceExpand=(false,false)
  ]
  children:
    StartBtn (LayoutElement: preferredWidth=320, preferredHeight=80)
    SettingsBtn (LayoutElement: preferredWidth=320, preferredHeight=80)
```

**关键**：Layout Group 下的子节点**不需要**再单独设 anchoredPosition（会被 Layout Group 强制覆盖）。

## 4. Content Size Fitter

让容器自适应子节点：
- **Horizontal Fit / Vertical Fit** = `Preferred Size`（推荐） / `Min Size` / `Unconstrained`
- 常与 Layout Group 联用：Layout Group 决定子节点排布，Content Size Fitter 让父节点尺寸跟随

**陷阱**：Content Size Fitter 在多层嵌套时可能导致布局振荡（父跟子、子又跟父），一层就够。

## 5. Aspect Ratio Fitter

让节点自身按长宽比自适应：
- **Aspect Mode** = `Width Controls Height` / `Height Controls Width` / `Fit In Parent` / `Envelope Parent`

用途：让头像框、缩略图在不同分辨率下保持固定长宽比。

## 6. Rotation / Scale 与 anchor 的关系

- 节点的 rotation / scale 是绕 **pivot** 转动/缩放的（不是绕 anchor）
- 缩放为 0 时，pivot 位置不变，其他部分被"塌陷"到 pivot 点
- 若要"从中心缩放" → pivot=(0.5, 0.5)
- 若要"从底部弹起"缩放（弹窗从 0 缩放到 1）→ pivot=(0.5, 0) + `LocalScale 0→1` tween

## 7. UI 层级顺序 vs Sibling Index

UGUI 的绘制顺序 = **Hierarchy 中的顺序**（Sibling Index 大的在后画，视觉在上层）。**没有 Z-Order 概念**（3D 空间里除外）。

- `Transform.SetAsFirstSibling()` / `SetAsLastSibling()` / `SetSiblingIndex(i)`
- **弹窗必须在最上层** → 打开时 SetAsLastSibling()
- **UI 与 3D 场景交互**：Camera Space Canvas 或 World Space Canvas，普通 Screen Space Overlay 永远盖住 3D

## 8. Raycast Target

Image / Text 默认开 raycastTarget=true，参与点击命中检测。

- **不需要点击的元素**关掉 raycast → 减少每帧 raycast 遍历成本
- **透明图但要点击**（如按钮的透明扩展区）→ 保持 raycast 开
- **纯装饰元素**（背景、标题字、图标）→ 关

**layout 里的书写约定**：默认不写；若某节点明确需要关 raycast，写上 `raycastTarget: false`。

## 9. Mask 与 RectMask2D

两种裁切子节点的方式：

| | Mask | RectMask2D |
|---|---|---|
| 裁切形状 | 任意 Image 形状（含九宫格） | 矩形（更快） |
| 性能 | 较重（每子节点+一个 draw call） | 轻 |
| 常见用途 | 圆形头像框 / 异形卡牌 | ScrollRect Content 裁切 |

**优先用 RectMask2D**，除非确实需要异形裁切。

## 10. 常见 UGUI 布局振荡

**症状**：Prefab 打开时布局看起来对，Play 后瞬间跳一次。

**原因**：
- Content Size Fitter 与 Layout Group 嵌套过深
- LayoutRebuilder 在 Awake / OnEnable 触发的重排布

**解决**：
- 在 OnEnable 里手动 `LayoutRebuilder.ForceRebuildLayoutImmediate(rt)`
- 或者简化层级，少用 Content Size Fitter

## 11. 缩放动画的常见坑

- 弹窗从 0 缩放到 1 时若 pivot=(0.5, 0.5)，视觉是"从中心炸开"
- 若要"从按钮位置弹出"→ 打开弹窗前先把 pivot 挪到按钮所在方向（或用 UI Particle 特效）
- **切忌用 sizeDelta 做缩放动画**（性能差、易触发重排）→ 用 `Transform.localScale`
