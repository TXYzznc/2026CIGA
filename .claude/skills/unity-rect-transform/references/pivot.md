# pivot — 轴心

## 是什么

`pivot`（Vector2，取值 [0, 1]）定义 RectTransform **自身**的锚点位置：

- pivot 是**旋转/缩放/位置**的参照点
- (0, 0) = 自身左下角、(0.5, 0.5) = 中心（默认）、(1, 1) = 右上角

## pivot vs anchor 的区别

- **anchor**：相对**父**的坐标；决定"我钉在父的哪里"
- **pivot**：相对**自己**的坐标；决定"我自己的哪个点被 anchor 定位"

两者共同决定最终位置：
```
最终屏幕位置 = 父.位置 + anchor 在父上的插值点 - pivot 在自身上的插值点 * 自身尺寸 + anchoredPosition
```

## 视觉效果

假设 sizeDelta = 200×100：

| pivot | 旋转/缩放行为 | 常见用途 |
|---|---|---|
| (0.5, 0.5) 中心 | 绕自身几何中心旋转 / 缩放 | 默认；大多数按钮、图标 |
| (0.5, 1) 顶部中点 | 绕顶部中点旋转，视觉上"从顶挂下来"缩放 | 从顶部弹下的通知条 |
| (0.5, 0) 底部中点 | 绕底部中点旋转，视觉上"从底部升起来"缩放 | 底部弹出面板、tooltip |
| (0, 0.5) 左中 | 绕左中旋转，"从左边推出"缩放 | 侧边抽屉 |
| (1, 0.5) 右中 | 从右边推出 | 右侧通知条 |

## 何时改 pivot（关键决策）

**默认保持 (0.5, 0.5)**，除非以下情况明确需要改：

1. **动画方向暗示**：弹窗从底部弹出 → pivot=(0.5, 0)，配合 anchoredPosition Y 值动画从负到 0
2. **旋转轴心**：血条从中间开始"缩水"→ pivot=(0, 0.5)（从左侧收缩）
3. **贴齐父边**：某节点严格贴齐父的右下角 → anchor=(1,0), pivot=(1,0)，然后 anchoredPosition=(0,0)
4. **对齐兄弟节点**：多个节点顶部对齐 → 都设 pivot y=1 便于用同一 anchoredPosition.y 参照

## pivot 陷阱

**改 pivot 会导致节点位置视觉跳动**（因为 anchoredPosition 参照点变了）。若要在保持视觉位置不变的前提下改 pivot：

- 手动补偿 anchoredPosition：`anchoredPosition += (newPivot - oldPivot) * sizeDelta`
- 或用 Editor 快捷键 **Shift+点击 Anchor Preset**（会自动改 pivot 且保持视觉位置）

## 在 prefab-layout.md 里的书写约定

pivot 若为默认 (0.5, 0.5)可省略；非默认必须写：

```
NotificationBar (RectTransform:
  anchor: top-center
  pivot: (0.5, 1)               # 非默认，必须写；表示"从顶挂下来"
  sizeDelta: (600, 80)
  anchoredPosition: (0, 0)
)

关键决策：pivot=(0.5,1) 是为了后续 tween 从 Y=80 → Y=0 实现下滑入场
```
