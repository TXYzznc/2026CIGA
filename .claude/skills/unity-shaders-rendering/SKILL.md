---
name: unity-shaders-rendering
description: 'Unity 着色器、材质与渲染管线（URP/HDRP/内置管线）。

  适用场景：(1) 使用Shader Graph、HLSL或ShaderLab编写着色器；(2) URP与HDRP着色器创作；(3) 自定义渲染管线（SRP）开发；(4)
  光照设置（烘焙 vs 实时、光照贴图、Global Illumination）；(5) 后处理栈；(6) 反射探针与光照探针；(7) 自定义渲染功能与全屏通道；(8)
  着色器剥离与变体管理；(9) 计算着色器；(10) HDRP中的光线追踪。

  提供内容：Shader Graph模板、HLSL代码片段、URP/HDRP差异对比、光照设置方案、渲染功能示例以及着色器变体指导。'
tags: unity-shaders, rendering-pipelines, shader-graph, hlsl, lighting-setup
tags_cn: Unity着色器, 渲染管线, Shader Graph, HLSL开发, 光照设置
---

# Unity 着色器与渲染

## 概述

Unity渲染系统、着色器开发、光照配置及视觉效果参考指南。涵盖三种渲染管线、Shader Graph、手写着色器及VFX Graph。

## 渲染管线对比

| 特性 | 内置管线（Built-in RP） | URP | HDRP |
|---------|------------|-----|------|
| 目标场景 | 遗留项目 | 移动端、VR及广泛场景 | 高端PC/主机 |
| 着色器语言 | Surface着色器 + HLSL | HLSL（无Surface着色器） | HLSL |
| Shader Graph支持 | 是 | 是 | 是 |
| SRP批处理 | 否 | 是 | 是 |
| 渲染功能 | 否 | 是（ScriptableRendererFeature） | 自定义通道（Custom Pass） |
| 后处理 | Post Processing Stack v2 | 内置Volume系统 | 内置Volume系统 |
| 光线追踪 | 否 | 否（基于探针） | 是（DXR） |
| 性能表现 | 中等 | 针对规模化场景优化 | 最高画质 |

**建议：** 新项目优先使用URP，除非专门针对高端视觉效果选择HDRP。内置管线已属遗留技术，建议尽可能迁移。

## Shader Graph

### 入门指南

1. 在项目窗口右键：创建 > Shader Graph > URP > 光照着色器图（Lit Shader Graph）
2. 双击打开Shader Graph编辑器
3. 构建节点网络并连接至主栈输出
4. 使用该着色器创建材质，分配给渲染器

### URP光照主栈输出

| 输出项 | 类型 | 用途 |
|--------|------|---------|
| 基础颜色（Base Color） | 颜色（RGB） | 反照率/漫反射颜色 |
| 法线（Normal） | 向量3（Vector3） | 切线空间法线贴图 |
| 金属度（Metallic） | 浮点数（0-1） | 金属材质 vs 绝缘材质 |
| 光滑度（Smoothness） | 浮点数（0-1） | 粗糙度的倒数 |
| 自发光（Emission） | 颜色（RGB） | 自发光效果 |
| 透明度（Alpha） | 浮点数（0-1） | 透明度 |
| Alpha裁剪阈值（Alpha Clip Threshold） | 浮点数 | Alpha测试的截断值 |

### 常见节点模式

| 效果 | 核心节点 |
|--------|-----------|
| **溶解效果** | 噪声（Noise）> 阶跃（Step）> Alpha裁剪 + 边缘自发光 |
| **UV滚动** | 时间（Time）> 乘法（Multiply）> 叠加至UV |
| **菲涅尔发光** | 菲涅尔效果（Fresnel Effect）> 颜色乘法 > 自发光 |
| **三平面映射** | 三平面节点（避免UV拉伸） |
| **颜色偏移** | 使用参数或时间在颜色间插值（Lerp） |
| **顶点位移** | 噪声（Noise）> 乘法（Multiply）> 叠加至位置（Position） |
| **轮廓线** | 双通道：在自定义渲染功能中使用反转外壳 |

### Shader Graph子图

将可复用的节点组提取为子图（创建 > Shader Graph > 子图）。适用于共享噪声函数、UV变换或自定义光照模型。

## 手写着色器（ShaderLab + HLSL）

### URP着色器结构

```hlsl
Shader "Custom/SimpleUnlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                return tex * _Color;
            }
            ENDHLSL
        }
    }
}
```

**与内置着色器的主要差异：**
- 使用`HLSLPROGRAM`/`ENDHLSL`（而非`CGPROGRAM`/`ENDCG`）
- 引入URP着色器库，而非UnityCG.cginc
- 使用`TEXTURE2D`/`SAMPLER`宏（而非`sampler2D`）
- 将属性包裹在`CBUFFER_START(UnityPerMaterial)`中，以兼容SRP批处理

## 光照

### 光照类型

| 类型 | 适用场景 | 阴影开销 |
|------|---------|-------------|
| 方向光 | 太阳、全局光照 | 低（级联阴影贴图） |
| 点光源 | 火把、灯具 | 中 |
| 聚光灯 | 手电筒、舞台灯光 | 中 |
| 区域光（仅烘焙） | 柔和窗户光、面板光 | 高（仅烘焙） |

### 光照模式

| 模式 | 描述 | 最佳适用场景 |
|------|-------------|----------|
| 实时 | 每帧计算 | 动态物体、少量光源 |
| 烘焙 | 预计算至光照贴图 | 静态场景 |
| 混合 | 烘焙间接光照 + 实时直接光照 | 最佳平衡方案 |

### 光照贴图烘焙技巧

- 根据场景比例设置光照贴图分辨率（室内场景建议10-40 texels/单位）
- 在烘焙场景中为动态物体使用光照探针（Light Probes）
- 为金属/反光表面使用反射探针（Reflection Probes）
- 启用GPU光照贴图器以加快烘焙速度
- 在静态标记中设置物体为"贡献GI（Contribute GI）"

## 后处理（Volume系统）

```text
设置步骤：
1. 向场景添加Volume（全局或局部）
2. 创建Volume Profile资源
3. 添加覆盖效果：Bloom、颜色调整、色调映射等
4. 相机需启用后处理（URP相机设置）
```

| 效果 | 性能开销 | 说明 |
|--------|-------------|-------|
| Bloom | 低 | 使用阈值控制强度 |
| 颜色调整 | 极低 | 饱和度、对比度、颜色滤镜 |
| 色调映射 | 极低 | 使用ACES实现电影级效果 |
| 暗角 | 极低 | 画面边缘暗化 |
| 环境光遮蔽（SSAO） | 中 | 移动端建议禁用 |
| 景深 | 高 | 仅在影视化场景中使用散景效果 |
| 运动模糊 | 中 | VR场景中可能引发晕动症 |

## VFX Graph vs 粒子系统

| 特性 | 粒子系统（Shuriken） | VFX Graph |
|---------|---------------------------|-----------|
| 执行方式 | CPU | GPU（计算着色器） |
| 粒子数量 | 数千级 | 数百万级 |
| 复杂度 | 组件化、简单 | 节点化、复杂 |
| 支持平台 | 所有平台 | 仅支持具备计算着色器能力的平台 |
| 集成度 | 物理、碰撞 | 物理集成有限 |

粒子系统适用于与游戏玩法集成的效果（物理碰撞、少量粒子）。VFX Graph适用于视觉奇观（雨、火、魔法、环境粒子）。

## URP渲染功能

通过自定义ScriptableRendererFeature扩展URP渲染：

```csharp
public class OutlineFeature : ScriptableRendererFeature
{
    OutlinePass _pass;

    public override void Create()
    {
        _pass = new OutlinePass();
        _pass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        renderer.EnqueuePass(_pass);
    }
}
```

常见用途：自定义轮廓线、屏幕空间效果、渲染纹理生成、基于模板的效果。

## 额外资源

### 参考文件
- **`references/shader-recipes.md`** -- 完整着色器实现：卡通着色、水效果、溶解、全息图、力场、程序化天空盒、模板传送门、顶点动画、自定义光照模型
- **`references/lighting-vfx-detail.md`** -- 高级光照设置、GI故障排查、VFX Graph食谱（火焰、烟雾、电流、传送门）、可脚本化渲染管线定制、自定义渲染通道