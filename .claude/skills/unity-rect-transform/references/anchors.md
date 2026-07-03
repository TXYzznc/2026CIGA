# anchors — 锚点系统

## 是什么

`anchorMin` / `anchorMax`（两个 Vector2）定义子 RectTransform 相对**父 RectTransform** 的锚点。取值 [0, 1]，(0,0)=父的左下角、(1,1)=父的右上角。

- **anchorMin == anchorMax** → "fixed anchor"（点锚点），子节点位置随该点等比缩放
- **anchorMin != anchorMax** → "stretch anchor"（线/面锚点），子节点尺寸随该区间等比拉伸

## 9 宫格 + Stretch 12 种常用组合速查

| 语义 | anchorMin | anchorMax | 视觉效果 |
|---|---|---|---|
| top-left | (0, 1) | (0, 1) | 钉在父左上，位置随父左上不缩放 |
| top-center | (0.5, 1) | (0.5, 1) | 钉在父顶部中点 |
| top-right | (1, 1) | (1, 1) | 钉在父右上 |
| middle-left | (0, 0.5) | (0, 0.5) | 钉在父左边中点 |
| middle-center | (0.5, 0.5) | (0.5, 0.5) | 钉在父中心（默认） |
| middle-right | (1, 0.5) | (1, 0.5) | 钉在父右边中点 |
| bottom-left | (0, 0) | (0, 0) | 钉在父左下 |
| bottom-center | (0.5, 0) | (0.5, 0) | 钉在父底部中点 |
| bottom-right | (1, 0) | (1, 0) | 钉在父右下 |
| **top-stretch** | (0, 1) | (1, 1) | 横向拉满父宽，纵向钉在顶 |
| **middle-stretch** | (0, 0.5) | (1, 0.5) | 横向拉满父宽，纵向钉在中 |
| **bottom-stretch** | (0, 0) | (1, 0) | 横向拉满父宽，纵向钉在底 |
| **left-stretch** | (0, 0) | (0, 1) | 纵向拉满父高，横向钉在左 |
| **right-stretch** | (1, 0) | (1, 1) | 纵向拉满父高，横向钉在右 |
| **stretch-all** | (0, 0) | (1, 1) | 横纵都拉满父容器 |

## Anchor Preset 快捷键

- 在 Inspector 点击 RectTransform 左上角的**方框图标** → 打开 Anchor Preset 面板
- 按住 **Alt** 键点击预设 → 同时设置 anchor + **position**
- 按住 **Shift** 键点击预设 → 同时设置 anchor + **pivot**
- 按住 **Alt + Shift** 点击预设 → anchor + position + pivot 一次全设

**推荐**：拼 Prefab 时先按 Alt+Shift 快速定位，再微调 sizeDelta / anchoredPosition。

## 在 prefab-layout.md 里的书写约定

用 `min=(x,y), max=(x,y)` 或直接语义名：

```
Title (RectTransform:
  anchor: top-stretch          # 或 min=(0,1), max=(1,1)
  pivot: (0.5, 1)
  sizeDelta: (0, 96)           # 宽度=0 表示随父拉伸，高度固定 96
  anchoredPosition: (0, 0)
)
```

## 关键决策场景

**什么时候用 fixed，什么时候用 stretch？**

| 场景 | 选择 |
|---|---|
| 按钮 / 图标 / 头像（尺寸固定不受父变化影响） | fixed |
| 横条 / HUD 顶部栏 / 分隔线（宽度要跟父等宽） | top-stretch / bottom-stretch |
| 全屏背景 / 模态遮罩 | stretch-all |
| 侧边栏（高度跟父，宽度固定） | left-stretch / right-stretch |
| 列表项容器（宽度跟父列表，高度固定） | top-stretch（配合 Content Size Fitter） |

**共识**：小图标 / 按钮 / 头像等"独立组件" → fixed；容器 / 背景 / 横条等"跟随父的元素" → stretch。
