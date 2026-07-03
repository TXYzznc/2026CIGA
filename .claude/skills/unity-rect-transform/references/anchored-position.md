# anchoredPosition — 锚点相对位置

## 是什么

`anchoredPosition`（Vector2）表示节点的 **pivot 点** 相对于 **父节点上 anchor 点** 的偏移量（单位：像素）。

**不是** `transform.localPosition`，也**不是**屏幕坐标。它是 UGUI 专用的相对坐标系。

## 计算公式（帮助理解）

```
最终屏幕位置.x = 父.位置.x + 父.宽度 × anchor.x - pivot.x × 自身.宽度 + anchoredPosition.x
最终屏幕位置.y = 父.位置.y + 父.高度 × anchor.y - pivot.y × 自身.高度 + anchoredPosition.y
```

（此处 anchor 为 fixed 时，anchorMin == anchorMax，公式两边合并为一个点）

## 与 localPosition 的关系

- 若 anchor == pivot（比如都是 middle-center） → anchoredPosition == localPosition
- 若不等 → anchoredPosition 与 localPosition 数值不同

**规则**：UGUI 里**永远用 anchoredPosition**，不用 localPosition。localPosition 会跟随 anchor 变化而"跳"。

## 典型示例

### 例 1：钉在父右下角，距离右下 20 像素

```
Icon (
  anchor: bottom-right (min=(1,0), max=(1,0))
  pivot: (1, 0)                # pivot 也放右下，让偏移量计算最直观
  sizeDelta: (64, 64)
  anchoredPosition: (-20, 20)  # X 负值 = 向左；Y 正值 = 向上
)
```

### 例 2：从父顶部向下 120 像素、水平居中

```
Title (
  anchor: top-center (min=(0.5,1), max=(0.5,1))
  pivot: (0.5, 1)              # pivot 顶中，让 Y 偏移直接等于"离顶部距离"
  sizeDelta: (800, 120)
  anchoredPosition: (0, -120)  # Y 负值 = 向下
)
```

### 例 3：全屏面板，无需 anchoredPosition

```
FullscreenPanel (
  anchor: stretch-all (min=(0,0), max=(1,1))
  pivot: (0.5, 0.5)
  sizeDelta: (0, 0)
  anchoredPosition: (0, 0)     # stretch-all 下不需要偏移
)
```

## 关键陷阱

### 陷阱 1：换 anchor 后 anchoredPosition 数值会变

如果把 anchor 从 middle-center 改成 top-left，视觉位置不变时 anchoredPosition 数值一定会变（因为参照点变了）。

**解决**：改 anchor 时按住 Alt 键点击 Anchor Preset，Unity 会自动调 anchoredPosition 保视觉位置不变。

### 陷阱 2：stretch anchor 下 anchoredPosition 变成 anchoredPositionOffset

anchor 是 stretch 时，anchoredPosition 表示的是 pivot 相对于 anchor 中点的偏移（此时 anchor 是个区间，中点默认参照）。**大多数 stretch 用例应把 anchoredPosition 保持 (0, 0)**，用 sizeDelta 控偏移。

### 陷阱 3：Y 轴方向

UGUI 里 **Y 向上为正**，与屏幕坐标系（Y 向下为正）相反。写 layout 时"距离顶部 120 像素"应该是 `anchoredPosition: (0, -120)`（因为 pivot 在顶时，Y 负值 = 向下走）。

## 在 prefab-layout.md 里的书写约定

- 若 anchoredPosition == (0, 0) 可省略
- 非 (0, 0) 必须写，并**在注释里写清楚参照方向**：

```
Icon (
  anchor: bottom-right, pivot: (1, 0)
  sizeDelta: (64, 64)
  anchoredPosition: (-20, 20)   # X=-20 向左；Y=20 向上
)
```
