# prefab-layout-template — prefab-layout.md 骨架模板

> 复制下方模板到 `openspec/changes/<NN-name>/art/prefab-layout.md`，逐节填充。
>
> **用途**：art-ui 阶段 1 产出，供阶段 2 效果图长宽反哺、阶段 5 client-unity 拼 Prefab 使用。

---

# <ChangeName> UI 结构文档

## 全局约定

- **画布基准分辨率**：1920×1080
- **Canvas Scaler**：Scale With Screen Size, ReferenceResolution 1920×1080, Match 0.5
- **UI 根 Canvas**：Screen Space - Overlay（或 Camera，视需求）
- **通用按钮尺寸**：320×80
- **通用图标尺寸**：64×64
- **通用标题条尺寸**：宽度跟父等宽，高度 96
- **本 change 涉及页面**：MainMenuForm / SettingsForm / ExitConfirmDialog
- **跨页复用规则**（若有）：所有确认弹窗使用同一 `ConfirmDialog` 层级模板

---

## 页面 1：<PageName>Form

### 节点树 + RectTransform 数据

```
<PageName>Form (RectTransform:
  anchor: stretch-all
  pivot: (0.5, 0.5)
  sizeDelta: (0, 0)          # 铺满 Canvas
)
├─ Background (RectTransform:
│    anchor: stretch-all
│    sizeDelta: (0, 0)
│  )
│  components: [
│    Image:
│      source: <阶段 4 拆分产出 Background.png (1920×1080)>
│      preserveAspect: false  # 背景需铺满
│  ]
│
├─ Title (RectTransform:
│    anchor: top-center
│    pivot: (0.5, 1)
│    sizeDelta: (800, 120)
│    anchoredPosition: (0, -60)   # 距离画布顶部 60 像素
│  )
│  components: [
│    TMP_Text:
│      text: "<页面标题>"
│      fontSize: 72
│      align: Center
│  ]
│
├─ ButtonGroup (RectTransform:
│    anchor: middle-center
│    sizeDelta: (400, 320)
│  )
│  components: [
│    VerticalLayoutGroup: spacing=20, padding=(20,20,20,20), childAlignment=MiddleCenter, childForceExpand=(false,false)
│  ]
│  ├─ StartBtn (RectTransform: sizeDelta=(320, 80), Layout 接管位置)
│  │   states: [normal, pressed, disabled]
│  │   components: [
│  │     Image: source=<StartBtn_normal.png>
│  │     Button: onClick → OnStartClicked
│  │     TMP_Text (child): "开始游戏"
│  │   ]
│  ├─ SettingsBtn (RectTransform: sizeDelta=(320, 80))
│  │   states: [normal, pressed, disabled]
│  └─ ExitBtn (RectTransform: sizeDelta=(320, 80))
│      states: [normal, pressed]
```

### 关键决策

- **Background 不开 preserveAspect**：背景本就是 1920×1080 铺满画布，无需保比例
- **Title pivot=(0.5, 1)**：让 anchoredPosition.y 直接对应"距离顶部像素"，读写直观
- **ButtonGroup 用 VerticalLayoutGroup**：按钮堆叠 + 未来若加/减按钮 layout 自动调整
- **StartBtn 有 disabled 态**：新玩家没通过 tutorial 时置灰
- **ExitBtn 没有 disabled 态**：任何时候都可退出，节省一张贴图

### 状态清单（跨节点汇总）

| 节点 | 状态 | 备注 |
|---|---|---|
| StartBtn | normal / pressed / disabled | 3 张贴图，阶段 3 各生一次 |
| SettingsBtn | normal / pressed / disabled | 3 张贴图 |
| ExitBtn | normal / pressed | 2 张贴图 |

---

## 页面 2：<AnotherPageName>Form

（同上结构，节点树 + 关键决策 + 状态清单）

---

## 跨页复用组件（若有）

### ConfirmDialog（通用确认弹窗模板）

```
ConfirmDialog (RectTransform:
  anchor: middle-center
  sizeDelta: (720, 400)
)
├─ Backdrop (stretch-all sizeDelta=(0,0), Image 半透明黑遮罩，raycastTarget=true)
├─ Panel (middle-center, sizeDelta=(720, 400))
│  ├─ Title (top-center, sizeDelta=(600, 60), anchoredPosition=(0,-40))
│  ├─ Content (middle-center, sizeDelta=(600, 200))
│  ├─ ConfirmBtn (bottom-center, sizeDelta=(200, 60), anchoredPosition=(-120,60))
│  │   states: [normal, pressed]
│  └─ CancelBtn (bottom-center, sizeDelta=(200, 60), anchoredPosition=(120,60))
│      states: [normal, pressed]
```

**用法**：ExitConfirmDialog 直接复用此模板，只填 Title/Content 文本 + 按钮回调。

---

## 变更日志

- 2026-07-01：初版，art-ui 起草
- （联调阶段若调整 RectTransform，同步更新此处）
