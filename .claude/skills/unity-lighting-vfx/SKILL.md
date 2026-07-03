---
name: unity-lighting-vfx
description: Unity 6 灯光与视觉效果指南。适用于处理灯光、烘焙/实时/混合光照、光照探针、反射探针、自适应探针体积（APV）、全局光照、Particle
  System、VFX Graph或后期处理效果的场景。基于Unity 6.3 LTS文档编写。
tags: unity-lighting, visual-effects, particle-system, vfx-graph, light-probes
tags_cn: Unity灯光系统, 视觉效果, Particle System, VFX Graph, 光照探针
---

# Unity 灯光与视觉效果

## 光照模式

Unity提供三种光照模式，用于控制光照的计算方式。

### 实时灯光
- **运行时每帧计算**光照
- 允许动态调整强度、颜色、位置
- 可投射阴影，阴影范围受Shadow Distance限制
- 默认仅贡献直接光照（无反弹光）
- 运行时成本较高，尤其是在复杂场景或低端硬件中
- 最佳适用场景：动态物体、闪烁效果、移动光源

### 烘焙灯光
- 在Unity编辑器中完成计算，并将光照数据保存到磁盘
- 运行时Unity加载预计算数据，而非动态计算
- 将直接光照和间接光照烘焙到光照贴图中
- 为移动物体存储光照探针信息
- 运行时无法修改灯光属性；不会照亮动态GameObject
- 无高光贡献
- 最佳适用场景：静态场景、复杂间接光照、性能优先的场景

### 混合灯光
- 结合**烘焙间接光照**与**实时直接光照**
- 行为取决于Lighting窗口中的Lighting Mode设置
- 投射实时阴影（非烘焙软阴影）
- 运行时可修改属性（仅影响实时部分）
- 成本始终高于完全烘焙的光照
- 最佳适用场景：需要动态阴影且背景光照为烘焙的场景

**重要提示：**所有烘焙/混合模式都需要启用**Baked Global Illumination**。未启用时，混合灯光和烘焙灯光会自动切换为实时灯光模式。

## 灯光类型

### 方向光
- 位于无限远处，向单一方向发射光线
- 光线平行，强度不随距离衰减
- 模拟太阳/月亮；新建场景默认包含一个方向光
- 与程序化天空系统关联；旋转可实现昼夜变化效果

### 点光源
- 位于某一点，向所有方向均匀发射光线
- 强度遵循平方反比定律（随距离平方衰减）
- 适用于灯具、爆炸效果、局部照明

### 聚光灯
- 位于某一点，以锥形范围发射光线
- 可调整锥角；光线在边缘衰减（半影区）
- 角度越宽，衰减区域越大
- 适用于手电筒、车头灯、探照灯

### 面光源
- 由矩形或圆形定义，从表面均匀发射光线
- 遵循平方反比定律；产生柔和、细腻的阴影
- **仅支持烘焙**——运行时不可用
- 适用于路灯、真实室内照明

## 全局光照

全局光照（GI）模拟光线在表面间的反弹，生成真实的间接光照效果。

### 烘焙GI
- 使用**Progressive Lightmapper**（CPU或GPU）将间接光照预计算到光照贴图中
- 结果存储在光照贴图纹理和光照探针中
- 在Lighting窗口的**Bake**设置中配置
- GPU Progressive Lightmapper支持更快的烘焙速度，且可配置 tile 大小

### 环境光照
在Lighting窗口的Environment标签中配置三种环境光源：
- **Skybox**：使用天空盒材质颜色，从不同角度提供环境光
- **Gradient**：分别设置天空、地平线和地面颜色，实现平滑过渡
- **Color**：为场景提供统一的环境光

Intensity Multiplier（范围0-8，默认1）控制环境光亮度。

### 环境反射
- 来源：Skybox或自定义Cubemap/RenderTexture
- 可配置分辨率、压缩方式、强度乘数
- Bounces设置控制物体间反射计算的迭代次数

## 光照探针与自适应探针体积

### 光照探针
- 捕获场景中空旷区域的光照信息
- 运行时，动态GameObject的间接光照通过最近的探针进行近似计算
- 为移动物体和LOD系统提供间接反弹光支持
- 需使用Light Probe Groups手动放置
- 存储烘焙光照信息；支持直接光照和间接光照

### 自适应探针体积（APV）
APV是URP中手动放置光照探针的现代化替代方案：
- **自动探针放置**——无需手动定位Light Probe Group
- **逐像素光照**——相比逐物体方案，质量更优
- **场景烘焙**——使用Baking Sets可同时烘焙多个场景
- **运行时调整**——支持Lighting Scenarios和天空遮挡，实现动态变化
- **大世界支持**——为大型开放世界环境提供数据流
- **数据灵活性**——可从AssetBundles或Addressables加载
- 可配置探针密度和体积大小，并提供可视化工具

## 反射探针

捕获周围环境的球形视图，保存为立方体贴图，用于反射材质。

### 类型
| 类型 | 描述 |
|------|-------------|
| **Baked** | 仅捕获静态GameObject；性能最佳 |
| **Custom** | 允许使用自定义纹理捕获动态物体 |
| **Realtime** | 游戏运行时更新；可配置刷新模式 |

### 关键属性
- **Importance**：多个探针重叠时的渲染优先级
- **Intensity**：着色器计算中的纹理亮度
- **Box Projection**：启用室内投影映射（需URP配置）
- **Box Size/Offset**：反射贡献的世界空间边界框
- **Blend Distance**：延迟探针的混合距离

### 实时选项
- **Refresh Mode**：唤醒时刷新、每帧刷新或通过脚本刷新
- **Time Slicing**：一次性渲染所有面、逐个渲染面、不使用时间切片

## Particle System vs VFX Graph

| 特性 | Particle System | VFX Graph |
|---------|----------------|-----------|
| 模拟方式 | 基于CPU | 基于GPU |
| 粒子数量 | 数千级 | 数百万级 |
| 渲染管线 | 支持所有管线 | 仅支持URP/HDRP |
| 创建方式 | 检查器模块 | 基于节点的图形编辑器 |
| 物理系统 | 内置碰撞 | 自定义碰撞模块 |
| 脚本控制 | 完整C# API | 基于事件的C# API |
| 子发射器 | 原生支持 | GPU Event上下文 |
| 最佳场景 | 中小型效果、移动端 | 大规模效果、高端平台 |

**选择指南：**
- 当目标为移动端、需要简单效果、需要完整CPU端脚本控制，或使用Built-in Render Pipeline时，选择**Particle System**
- 当需要海量粒子、GPU驱动模拟、复杂节点式创建，或使用URP/HDRP的高端平台时，选择**VFX Graph**

## Particle System模块

| 模块 | 用途 |
|--------|---------|
| **Main** | 初始状态设置：生命周期、速度、大小、重力、模拟空间 |
| **Emission** | 粒子生成的速率和时间控制 |
| **Shape** | 粒子发射的体积/表面及初始速度方向 |
| **Velocity over Lifetime** | 随粒子生命周期修改运动状态 |
| **Noise** | 为粒子添加湍流，实现自然、混乱的运动 |
| **Limit Velocity over Lifetime** | 模拟自然减速效果 |
| **Force over Lifetime** | 模拟物理作用力 |
| **Inherit Velocity** | 子发射器粒子继承父物体速度 |
| **Lifetime by Emitter Speed** | 根据发射器速度调整粒子生命周期 |
| **Color over Lifetime / by Speed** | 根据粒子年龄或速度改变颜色 |
| **Size over Lifetime / by Speed** | 根据时间或速度改变粒子尺寸 |
| **Rotation over Lifetime / by Speed** | 改变粒子朝向 |
| **Collision** | 粒子与场景几何体的碰撞 |
| **Triggers** | 将粒子标记为碰撞触发器 |
| **Sub Emitters** | 可发射其他粒子的粒子 |
| **Texture Sheet Animation** | 纹理网格动画帧 |
| **Trails** | 运动轨迹渲染 |
| **Lights** | 粒子上的实时灯光 |
| **External Forces** | 风区和力场 |
| **Renderer** | 图像/网格变换、着色、过绘制 |
| **Custom Data** | 为粒子附加自定义数据 |

## VFX Graph基础

### 系统
1. **Spawn System**：管理粒子发射的单个Spawn Context
2. **Particle System**：初始化 -> 更新 -> 输出的流程
3. **Mesh Output System**：单个Mesh Output Context

### 上下文
- **Spawn**：每帧执行以计算粒子生成数量。状态：Running、Idle、Waiting。可配置循环时长、数量和延迟
- **Initialize**：粒子生成时运行，设置初始状态。处理新生成粒子的Blocks。可配置边界和容量
- **Update**：每帧对所有存活粒子执行。自动处理：位置整合、旋转整合、老化、销毁
- **Output**：渲染粒子（Quad、Mesh等）。无输出端口。可自定义渲染Blocks

### 图形元素
- **Blocks**：上下文内可堆叠的节点；每个节点处理一项操作；按从上到下顺序执行
- **Operators**：连接Block/Context端口的底层属性工作流节点
- **Properties**：可通过属性工作流连接
- **Settings**：每个上下文的不可连接可编辑值

### GPU Events
实验性功能，由GPU计算事件（常规事件由CPU计算）。无法用Blocks自定义。

## 常见模式（C#）

### 创建并配置灯光
```csharp
using UnityEngine;

public class LightSetup : MonoBehaviour
{
    void Start()
    {
        GameObject lightObj = new GameObject("Dynamic Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = Color.yellow;
        light.intensity = 2.0f;
        light.range = 15f;
        light.shadows = LightShadows.Soft;
        light.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Medium;
    }
}
```

### 创建实时反射探针
```csharp
using UnityEngine;
using UnityEngine.Rendering;

public class ProbeSetup : MonoBehaviour
{
    void Start()
    {
        GameObject probeObj = new GameObject("Realtime Reflection Probe");
        ReflectionProbe probe = probeObj.AddComponent<ReflectionProbe>();
        probe.size = new Vector3(10, 10, 10);
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.EveryFrame;
        probe.resolution = 256;
        probe.hdr = true;
    }
}
```

### 运行时控制Particle System
```csharp
using UnityEngine;

public class ParticleController : MonoBehaviour
{
    ParticleSystem ps;

    void Start()
    {
        ps = GetComponent<ParticleSystem>();

        // 修改发射速率——先将模块缓存到局部变量
        var emission = ps.emission;
        emission.rateOverTimeMultiplier = 50f;

        // 修改主模块
        var main = ps.main;
        main.startLifetime = 3f;
        main.startSpeed = 5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
    }

    void OnTriggerEnter(Collider other)
    {
        // 爆发式生成粒子
        ps.Emit(100);
    }

    void OnDisable()
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
```

### VFX Graph运行时控制
```csharp
using UnityEngine;
using UnityEngine.VFX;

public class VFXController : MonoBehaviour
{
    VisualEffect vfx;
    VFXEventAttribute eventAttr;

    void Start()
    {
        vfx = GetComponent<VisualEffect>();
        eventAttr = vfx.CreateVFXEventAttribute();

        // 设置暴露属性
        vfx.SetFloat("SpawnRate", 100f);
        vfx.SetVector3("Direction", Vector3.up);
        vfx.playRate = 1.5f;
    }

    void OnTriggerEnter(Collider other)
    {
        // 发送带属性的自定义事件
        eventAttr.SetVector3("position", other.transform.position);
        vfx.SendEvent("OnHit", eventAttr);
    }

    public void StopEffect()
    {
        vfx.Stop();
    }
}
```

### 按需刷新反射探针
```csharp
using UnityEngine;

public class ProbeRefresher : MonoBehaviour
{
    ReflectionProbe probe;

    void Start()
    {
        probe = GetComponent<ReflectionProbe>();
        probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
    }

    public void RefreshReflections()
    {
        probe.RenderProbe();
    }

    public bool IsReady()
    {
        return probe.IsFinishedRendering(probe.RenderProbe());
    }
}
```

## 反模式

### 光照反模式
1. **过多实时灯光**：每个实时灯光都会增加每帧成本。静态灯光应使用烘焙或混合模式
2. **忘记启用Baked Global Illumination**：未启用时，混合/烘焙灯光会自动切换为实时模式
3. **重叠反射探针未设置Importance值**：会导致闪烁；始终需设置Importance来确定优先级
4. **期望面光源在运行时生效**：面光源仅支持烘焙；运行时不会产生光照
5. **忽略Indirect Multiplier**：默认值可能导致反弹光过亮或过暗；需针对每个灯光调整
6. **Shadow Bias设置不当**：值过低会导致自阴影 artifacts（阴影 acne）；值过高会导致阴影脱离物体（peter-panning）
7. **在大型场景中手动放置光照探针**：在URP中应使用自适应探针体积（APV），实现自动放置和逐像素质量

### Particle System反模式
1. **未缓存模块引用**：每次访问模块结构体属性都会立即写入原生代码；需缓存模块变量
2. **对附着效果使用World模拟空间**：粒子会从移动的父物体漂移；附着效果应使用Local空间
3. **CPU上粒子数量过多**：Particle System基于CPU；粒子数量超过10K时，考虑使用VFX Graph
4. **忘记停止/清除粒子系统**：泄漏的粒子系统即使不可见也会消耗CPU

### VFX Graph反模式
1. **在移动端使用VFX Graph实现简单效果**：GPU开销和URP/HDRP要求使其在移动端简单效果场景中过于冗余
2. **未在Initialize中设置Capacity**：默认容量可能分配过多或过少GPU内存
3. **忽略Bounds**：错误的Bounds会导致可见效果被剔除；始终需在Initialize上下文中配置Bounds
4. **无限制地每帧发送事件**：SendEvent存在CPU-GPU同步成本；需批量处理或限制事件发送频率

## 关键API速查

### Light（UnityEngine.Light）
| 成员 | 类型 | 描述 |
|--------|------|-------------|
| `type` | 属性 | LightType（Directional、Point、Spot、Area） |
| `color` | 属性 | 发射光颜色 |
| `intensity` | 属性 | 亮度乘数 |
| `range` | 属性 | 最大距离（点光源/聚光灯） |
| `spotAngle` / `innerSpotAngle` | 属性 | 外/内锥角 |
| `shadows` | 属性 | LightShadows（None、Hard、Soft） |
| `shadowResolution` | 属性 | 阴影贴图质量 |
| `shadowBias` / `shadowNormalBias` | 属性 | 减少阴影 artifacts |
| `bounceIntensity` | 属性 | GI反弹强度 |
| `cookie` | 属性 | 投影纹理遮罩 |
| `cullingMask` | 属性 | 基于层的过滤 |
| `colorTemperature` | 属性 | 色温（开尔文） |
| `lightmapBakeType` | 属性 | 烘焙配置 |
| `bakingOutput` | 属性 | 上次烘焙贡献详情 |
| `AddCommandBuffer()` | 方法 | 在指定阶段执行GPU命令 |

### ParticleSystem（UnityEngine.ParticleSystem）
| 成员 | 类型 | 描述 |
|--------|------|-------------|
| `main` / `emission` / `shape` | 属性 | 模块访问结构体 |
| `particleCount` | 属性 | 当前活跃粒子数 |
| `isPlaying` / `isPaused` / `isStopped` | 属性 | 播放状态 |
| `Play()` / `Pause()` / `Stop()` | 方法 | 播放控制 |
| `Emit(count)` | 方法 | 立即生成粒子 |
| `Simulate(time)` | 方法 | 快进模拟 |
| `GetParticles()` / `SetParticles()` | 方法 | 直接访问粒子数据 |
| `Clear()` | 方法 | 移除所有粒子 |
| `TriggerSubEmitter()` | 方法 | 激活子发射器 |

### VisualEffect（UnityEngine.VFX.VisualEffect）
| 成员 | 类型 | 描述 |
|--------|------|-------------|
| `visualEffectAsset` | 属性 | 分配/更换效果图形 |
| `playRate` | 属性 | 模拟速度 |
| `aliveParticleCount` | 属性 | 活跃粒子数 |
| `Play()` / `Stop()` / `Reinit()` | 方法 | 播放控制 |
| `SendEvent(name, attr)` | 方法 | 触发图形事件 |
| `CreateVFXEventAttribute()` | 方法 | 创建事件负载 |
| `SetFloat()` / `SetVector3()` / etc. | 方法 | 设置暴露属性 |
| `GetFloat()` / `GetVector3()` / etc. | 方法 | 读取暴露属性 |
| `HasFloat()` / `HasVector3()` / etc. | 方法 | 检查属性是否存在 |
| `ResetOverride(property)` | 方法 | 恢复原始值 |

### ReflectionProbe（UnityEngine.ReflectionProbe）
| 成员 | 类型 | 描述 |
|--------|------|-------------|
| `mode` | 属性 | Baked/Custom/Realtime |
| `size` / `center` | 属性 | 边界框配置 |
| `intensity` / `importance` | 属性 | 亮度和优先级 |
| `boxProjection` | 属性 | 启用盒式投影 |
| `refreshMode` / `timeSlicingMode` | 属性 | 实时更新配置 |
| `RenderProbe()` | 方法 | 强制刷新立方体贴图 |
| `IsFinishedRendering()` | 方法 | 检查时间切片渲染是否完成 |
| `BlendCubemap()` | 方法 | 混合两个立方体贴图 |

## 相关技能
- `unity-graphics` -- 渲染管线（URP/HDRP/Built-in）、着色器、材质、相机
- `unity-2d` -- 2D光照（URP 2D Renderer）、精灵渲染
- `unity-platforms` -- 平台特定光照质量层级、移动端优化

## 附加资源
- [光照概述](https://docs.unity3d.com/6000.3/Documentation/Manual/LightingOverview.html)
- [灯光类型](https://docs.unity3d.com/6000.3/Documentation/Manual/LightTypes.html)
- [光照模式](https://docs.unity3d.com/6000.3/Documentation/Manual/LightModes-introduction.html)
- [光照探针](https://docs.unity3d.com/6000.3/Documentation/Manual/LightProbes.html)
- [反射探针](https://docs.unity3d.com/6000.3/Documentation/Manual/ReflectionProbes.html)
- [自适应探针体积（URP）](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/probevolumes.html)
- [Particle System模块](https://docs.unity3d.com/6000.3/Documentation/Manual/configuring-particles.html)
- [VFX Graph包](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.0/manual/index.html)
- [灯光脚本API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Light.html)
- [ParticleSystem脚本API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ParticleSystem.html)
- [ReflectionProbe脚本API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ReflectionProbe.html)
