# preserveAspect — 图像长宽比保持

## 是什么

`Image.preserveAspect`（bool）：开启后，Image 会**在 RectTransform 尺寸的范围内**按图源的原始长宽比缩放，保持不变形。多余空间留白（不填色，视为透明）。

对应字段：Unity Inspector 上 Image 组件的 "Preserve Aspect" 复选框；代码 `image.preserveAspect = true`。

## 适用场景（必开）

1. **矩形图源塞到方形容器**（如 16:9 头像塞到 512×512 容器）→ 关掉会拉伸变形
2. **同一 Image 组件切换不同长宽比的图源**（角色立绘 vs 头像）→ 用 preserveAspect 兜底
3. **图源尺寸 vs 容器尺寸不一致的所有情况**

## 不适用场景（不开或无效）

1. **图源尺寸与容器长宽比完全一致** → 开了没效果，但也不出错（可留着但不必）
2. **需要图像铺满容器（拉伸也无所谓）** → 关（如全屏纯色背景）
3. **需要图像超出容器裁切** → 关（这是 Sprite Renderer 的 Overflow 模式，Image 不支持）

## 与图源尺寸的配合

### 推荐做法：图源尺寸 ≈ 容器尺寸的长宽比

设计 layout 时，如果打算给某个节点开 preserveAspect，**尽量让图源长宽比接近容器 sizeDelta 的长宽比**，减少留白：

```
Portrait (
  anchor: middle-center, sizeDelta: (400, 600)   # 容器 2:3
  components: [Image: preserveAspect=true, source=Character.png (1024x1536)]  # 图源 2:3
)
```

若图源 1024×1024（1:1）塞进 400×600（2:3）容器 + preserveAspect → 会上下留白（图像水平居中显示为 400×400，上下各留 100 空白）。

### 反例：不该开

```
Background (
  anchor: stretch-all, sizeDelta: (0, 0)         # 容器 = 父尺寸（比如 1920×1080）
  components: [Image: preserveAspect=false, source=BG_1920x1080.png]
)
```

背景本来就应该铺满，图源与容器长宽比一致 → 不需要 preserveAspect。

## 在 prefab-layout.md 里的书写约定

Image 组件描述里明确写：

```
Avatar (
  anchor: top-left, pivot: (0, 1)
  sizeDelta: (128, 128)
  anchoredPosition: (20, -20)
  components: [
    Image:
      source: <阶段 4 会拆出 Avatar.png>
      preserveAspect: true             # 因为头像图源可能 1:1 或 3:4，用户上传可能不规则
  ]
)
```

## 常见错误

❌ **忘开 preserveAspect** 导致角色立绘被压扁：
```
Portrait sizeDelta=(300, 400)，图源 1024×1024 (1:1)，未开 preserveAspect
→ 立绘水平被压缩，视觉扭曲
```
✅ 加 `preserveAspect=true`

❌ **对全屏背景开 preserveAspect** 导致背景不铺满：
```
FullBG sizeDelta=(0,0) with stretch-all, 图源 1920×1080，开 preserveAspect
→ 在竖屏（1080×1920）下，背景会缩到 1080×608 并居中，上下大片空白
```
✅ 关 preserveAspect，让背景强制拉伸铺满

## 快速判断

问自己两个问题：

1. 图源长宽比和容器长宽比一致吗？
   - 一致 → 不需要开
2. 若不一致，我希望留白还是拉伸？
   - 留白（保比例）→ 开
   - 拉伸（铺满）→ 不开
