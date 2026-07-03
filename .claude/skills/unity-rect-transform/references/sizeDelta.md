# sizeDelta — 尺寸增量（UGUI 最大陷阱）

## 是什么

`sizeDelta`（Vector2）在 UGUI 里**不是"尺寸"**，而是"**相对锚点区域的增量**"。语义会随 anchor 是 fixed 还是 stretch 完全变化：

## 双语义

### 语义 A：fixed anchor（anchorMin == anchorMax）

`sizeDelta` == **实际尺寸**（width, height）。

```
按钮 (anchor=middle-center, sizeDelta=(320, 80))
→ 实际尺寸 320×80
```

这是大多数人对 sizeDelta 的直觉。

### 语义 B：stretch anchor（anchorMin != anchorMax）

`sizeDelta` == **相对锚点框的偏移量**（在拉伸方向上）。**实际尺寸 = 父在该方向上的尺寸 × anchor 跨度 + sizeDelta**。

```
顶部横条 (anchor=top-stretch [min=(0,1),max=(1,1)], sizeDelta=(0, 96))
→ 横向：sizeDelta.x=0 → 实际宽度 = 父宽度 × (1-0) + 0 = 父宽度
→ 纵向：因为 anchorMin.y == anchorMax.y (都=1)，此方向仍是 fixed → 高度 = 96

全屏遮罩 (anchor=stretch-all [min=(0,0),max=(1,1)], sizeDelta=(0, 0))
→ 横纵都 stretch，sizeDelta=(0,0) → 完全等于父尺寸

带内边距的横条 (anchor=top-stretch, sizeDelta=(-40, 96))
→ sizeDelta.x=-40 → 实际宽度 = 父宽度 - 40（左右各留 20 内边距）
```

## 陷阱：混合方向

anchor 可以在 X / Y 两个方向上分别是 fixed 或 stretch：

| anchor | X 方向 | Y 方向 | sizeDelta 语义 |
|---|---|---|---|
| middle-center | fixed | fixed | (宽, 高) |
| top-stretch | stretch | fixed | (相对父宽偏移, 高) |
| left-stretch | fixed | stretch | (宽, 相对父高偏移) |
| stretch-all | stretch | stretch | (相对父宽偏移, 相对父高偏移) |

**书写 layout 时必须注明 anchor 组合**，否则 client-unity 看到 `sizeDelta: (0, 96)` 会不知道是"宽 0 高 96" 还是"横向跟父宽 + 高 96"。

## 在 prefab-layout.md 里的书写约定

推荐两种写法二选一：

**A. anchor 名 + sizeDelta 原值**（简洁，需要读者懂语义）：
```
TitleBar (anchor: top-stretch, sizeDelta: (0, 96))
```

**B. anchor 名 + 计算后语义**（冗余但清晰，推荐给 layout 新手）：
```
TitleBar (anchor: top-stretch, sizeDelta: (0, 96)
  # 语义：宽度跟父等宽，高度固定 96）
```

### 内边距的推荐写法

```
InnerPanel (anchor: stretch-all, sizeDelta: (-80, -60)
  # 语义：左右各留 40 内边距、上下各留 30 内边距）
```

## 常见错误

❌ `Background (anchor: stretch-all, sizeDelta: (1920, 1080))`
→ 错。stretch-all 下 sizeDelta 是**相对父的偏移**，(1920, 1080) 会让节点比父大 1920×1080 像素，导致背景严重超出父范围。
✅ 正确：`sizeDelta: (0, 0)`

❌ `FullscreenMask (anchor: middle-center, sizeDelta: (1920, 1080))`
→ 部分正确，但 mask 应该跟随分辨率变化 → 应改为 `anchor: stretch-all, sizeDelta: (0, 0)`

## 快速自检

拿到一个节点的 anchor + sizeDelta，先问自己：

1. anchorMin.x == anchorMax.x？→ X 方向 fixed，sizeDelta.x = 宽度
2. anchorMin.x != anchorMax.x？→ X 方向 stretch，sizeDelta.x = 相对父宽偏移
3. Y 方向同理

**再问一遍：这个方向 fixed 还是 stretch？**
