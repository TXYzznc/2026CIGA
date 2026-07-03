---
name: unity-rect-transform
description: "UGUI 空间语言词典。覆盖 RectTransform 的 anchor / pivot / sizeDelta / anchoredPosition 四大字段与视觉效果映射、Preserve Aspect 用法、Canvas Scaler 选型、Layout Group 交互、常见坑（父子 anchor 冲突 / stretch 下 sizeDelta 语义反转）。触发：RectTransform、anchor、pivot、sizeDelta、anchoredPosition、锚点、轴心、UGUI 布局、prefab-layout、UI 结构、preserveAspect、Canvas Scaler、多分辨率适配。适用于 art-ui 阶段 1 出 prefab-layout.md 与 client-unity 阶段 5 拼 Prefab。"
---

# unity-rect-transform — UGUI 空间语言词典

## 谁在用

- **art-ui**（阶段 1 出 `art/prefab-layout.md`）
- **client-unity**（阶段 5 拼 Prefab + 写脚本）

两个 agent 必须共用**同一套 RectTransform 语言**，layout 里怎么描述，Prefab 里就怎么设。

## 子技能列表

| 子技能 | 内容 | 参考文件 |
|--------|-----|---------|
| **anchors** | anchorMin/anchorMax 9 宫格 + stretch 组合的视觉效果表；Anchor Preset 快捷键 | `references/anchors.md` |
| **pivot** | pivot 的意义 + 旋转/缩放锚点原理 + 与 anchor 的关系 | `references/pivot.md` |
| **sizeDelta** | fixed vs stretch anchor 下的语义差异；常见陷阱 | `references/sizeDelta.md` |
| **anchoredPosition** | 与 localPosition 的区别；相对 anchor 的坐标系 | `references/anchored-position.md` |
| **preserveAspect** | Image.preserveAspect 适用场景；图源尺寸建议 | `references/preserve-aspect.md` |
| **common-pitfalls** | Canvas Scaler 选型 / 父子 anchor 冲突 / Layout Group 交互 | `references/common-pitfalls.md` |
| **prefab-layout-template** | `prefab-layout.md` 完整骨架，供 art-ui 复制填充 | `references/prefab-layout-template.md` |

## 使用流程

### art-ui 阶段 1

1. 先读 `prefab-layout-template.md`，复制骨架到 `openspec/changes/<change>/art/prefab-layout.md`
2. 对每个页面：
   - 用 `anchors.md` 决定每个节点的 anchorMin/anchorMax（是 fixed 还是 stretch）
   - 用 `pivot.md` 决定 pivot（默认 0.5,0.5，明确变动时说明原因）
   - 用 `sizeDelta.md` 决定 sizeDelta（注意 stretch 语义反转）
   - 用 `anchoredPosition.md` 决定 anchoredPosition
   - 用 `preserveAspect.md` 判断哪些 Image 需要开 preserveAspect
3. 关键决策（显著偏离默认值的）在 layout 里写「关键决策」小节说明原因

### client-unity 阶段 5

1. 读 `art/prefab-layout.md`，把 layout 的节点树 1:1 建到 Prefab 里
2. 每节点的 4 项 RectTransform 数据（anchor / pivot / sizeDelta / anchoredPosition）**必须与 layout 完全一致**，不许凭效果图像素估算
3. 遇到 layout 未指定的字段（如 rotation / scale） → 用默认值（rotation=0,0,0；scale=1,1,1）
4. 若阶段 6 联调时需要调整任何 RectTransform 值 → **必须同步回写 layout**（保持三者一致）

## 关键约束

- **layout 是 source of truth**：Prefab 与效果图冲突时以 layout 为准
- **stretch 语义陷阱**：anchor 为 stretch（如 anchorMin=(0,0), anchorMax=(1,1)）时，sizeDelta 的值等于"相对父节点的偏移量"，**不**是"实际尺寸"。这是 UGUI 最常见的踩坑点，详见 `sizeDelta.md`
- **preserveAspect 不是万能**：只在图源尺寸与 target sizeDelta 长宽比**不一致**时开；一致时开了反而无效果（浪费一次判断）
- **父子 anchor 冲突**：子节点的 anchor 是相对父节点 RectTransform 的，父节点位置/尺寸变化会级联影响子节点最终位置。设计 layout 时应从外到内自顶向下推演

## 与其他 SKILL 的边界

- **不含**：Text/TMP 排版规则（走 `art-font` / `typeset` SKILL）
- **不含**：Image 组件的贴图选择 / 九宫格切图（走 `art-ui` / `ui-asset-splitting`）
- **不含**：Button / Toggle 等交互逻辑（走 `unity-ui`）
- **只关心**：UGUI 空间/尺寸/锚点的**结构语言**
