---
name: blender-mcp
description: Blender MCP 专家，可提供场景检查、Python 脚本编写、GLTF 导出以及材质/动画提取相关支持。适用于以下场景：(1) 使用
  Blender MCP 工具（get_scene_info、execute_python、screenshot 等）时；(2) 编写用于提取或操作内容的 Blender
  Python 脚本时；(3) 将场景导出为适用于网页的 GLTF/GLB 格式（Three.js、R3F）时；(4) 调试材质或纹理导出丢失问题时；(5) 使用
  gltf-transform 优化 GLB 文件时；(6) 使用资产集成工具（PolyHaven、Sketchfab、Hyper3D Rodin、Hunyuan3D）时。涵盖关键导出注意事项、材质映射保留方法、纹理优化流程、无头
  CLI 模式以及已知故障模式。
tags: blender-mcp, gltf-export, python-scripting, texture-optimization, asset-integration
tags_cn: Blender MCP使用, GLTF导出优化, Blender Python脚本, 纹理优化流程, 资产集成工具
---

# Blender MCP

## 工具选择

使用**结构化 MCP 工具**（`get_scene_info`、`screenshot`）进行快速检查。

对于非简单操作（如层级遍历、材质提取、动画烘焙、批量操作），请使用**`execute_python`**。它可提供完整的 `bpy` API 访问权限，避免工具架构限制。

GLTF 导出请使用**无头 CLI**——MCP 服务器在导出操作时会超时。

## 健康检查（始终优先执行）

1. `get_scene_info` —— 验证连接（默认端口 9876）
2. 执行 `execute_python` 并运行 `print("ok")` —— 验证 Python 功能正常
3. `screenshot` —— 验证视口捕获功能正常

如果 MCP 无响应，请检查 Blender MCP 插件是否已启用，以及套接字服务器是否正在运行。

## 关键规则

### 1. MCP 服务器导出时会超时

Blender MCP 服务器无法处理 GLTF 导出——会超出超时时间。请始终使用无头 CLI：

```bash
/Applications/Blender.app/Contents/MacOS/Blender --background "scene.blend" --python-expr "
import bpy, os
export_path = 'output.glb'
os.makedirs(os.path.dirname(export_path), exist_ok=True)
bpy.ops.export_scene.gltf(
    filepath=export_path,
    export_format='GLB',
    export_apply=False,
    export_animations=True,
    export_nla_strips=True,
    export_cameras=True,
    export_lights=False,
    export_draco_mesh_compression_enable=False,
)
print(f'Size: {os.path.getsize(export_path)/1024/1024:.1f} MB')
"
```

### 2. 导出时请勿应用修改器

设置 `export_apply=False`。Array 修改器（环形图案、线性重复）在烘焙后会大幅增加文件大小，应改为在运行时复现效果。

示例：通过 Array 修改器实现 16 个滚轮实例 → 约 1 MB GLB 文件。烘焙后 → 约 56 MB GLB 文件。

### 3. 先导出未压缩的版本再进行后续操作

如果计划使用 `gltf-transform` 优化文件，请先导出未启用 Draco 压缩的版本。对已有的 Draco 压缩文件重新编码会损坏网格，应将 Draco 压缩作为最后一步操作。

### 4. 程序化纹理无法导出到 GLTF

以下 Blender 节点设置在导出时会**丢失**：

| 节点设置 | 丢失内容 | 解决方法 |
|------------|-------------|------------|
| Noise Texture → roughness | 整个程序化节点链 | 烘焙为纹理，或在运行时修补着色器 |
| Color Ramp on roughness texture | 值重映射范围 | 使用手动粗糙度值，或在运行时重映射 |
| Procedural bump (Noise → Bump) | 凹凸细节 | 在 Blender 中烘焙法线贴图 |
| Mix Shader with complex factor | 混合逻辑 | 导出前简化为单个 BSDF |

**可正常导出的内容**：平面粗糙度/金属度值、图像纹理（无 Color Ramp 重映射）、烘焙法线贴图、PBR 纹理集（baseColor、metallicRoughness、normal）。

### 5. GLTF 名称映射规则

Blender 中的名称会在 GLTF 中被转换：
- 空格 → 下划线
- 点号 → 移除
- 末尾空格 → 末尾下划线

| Blender | GLTF |
|---------|------|
| `RINGS ball L` | `RINGS_ball_L` |
| `Sphere.003` | `Sphere003` |
| `RINGS L.001` | `RINGS_L001` |
| `RINGS S `（末尾空格） | `RINGS_S_` |

在代码中引用网格时，请始终检查导出的 GLB 文件中的名称，而非 Blender 中的名称。

### 6. 请勿使用 gltf-transform 的 `optimize` 命令

`optimize` 命令包含的 `simplify` 功能会破坏网格几何结构，请改用单独的步骤：

```bash
# 调整纹理大小（最大 1024x1024）
npx @gltf-transform/cli resize input.glb resized.glb --width 1024 --height 1024

# WebP 纹理压缩
npx @gltf-transform/cli webp resized.glb webp.glb --quality 90

# Draco 网格压缩（最后一步）
npx @gltf-transform/cli draco webp.glb output.glb
```

### 7. 为包含空格的路径添加引号

Blender 项目路径通常包含空格，请始终使用双引号包裹：
```bash
/Applications/Blender.app/Contents/MacOS/Blender --background "$HOME/Downloads/blend 3/scene.blend" ...
```

## 场景提取模板

包含材质、变换和修改器的完整层级结构：

```python
import bpy, json

def extract_hierarchy(obj, depth=0):
    data = {
        "name": obj.name,
        "type": obj.type,
        "location": list(obj.location),
        "rotation": list(obj.rotation_euler),
        "scale": list(obj.scale),
        "visible": not obj.hide_viewport,
        "children": [],
    }
    if obj.type == 'MESH' and obj.data:
        data["vertices"] = len(obj.data.vertices)
        data["faces"] = len(obj.data.polygons)
        data["materials"] = [slot.material.name for slot in obj.material_slots if slot.material]
    if obj.type == 'LIGHT':
        data["light_type"] = obj.data.type
        data["energy"] = obj.data.energy
        data["color"] = list(obj.data.color)
        if obj.data.type == 'AREA':
            data["size"] = obj.data.size
            data["size_y"] = obj.data.size_y
    # Array modifiers (important for runtime replication)
    for mod in obj.modifiers:
        if mod.type == 'ARRAY':
            data.setdefault("modifiers", []).append({
                "type": "ARRAY",
                "count": mod.count,
                "offset_object": mod.offset_object.name if mod.offset_object else None,
            })
    for child in obj.children:
        data["children"].append(extract_hierarchy(child, depth + 1))
    return data

scene_data = {
    "name": bpy.context.scene.name,
    "fps": bpy.context.scene.render.fps,
    "frame_start": bpy.context.scene.frame_start,
    "frame_end": bpy.context.scene.frame_end,
    "objects": [],
}

for obj in bpy.context.scene.objects:
    if obj.parent is None:
        scene_data["objects"].append(extract_hierarchy(obj))

print(json.dumps(scene_data, indent=2))
```

## 材质提取模板

```python
import bpy, json

def extract_materials():
    materials = []
    for mat in bpy.data.materials:
        if not mat.use_nodes:
            continue
        info = {"name": mat.name, "nodes": []}
        for node in mat.node_tree.nodes:
            node_data = {"type": node.type, "name": node.name}
            if node.type == 'BSDF_PRINCIPLED':
                for inp in node.inputs:
                    if inp.is_linked:
                        node_data[inp.name] = "linked"
                    elif hasattr(inp, 'default_value'):
                        val = inp.default_value
                        try:
                            node_data[inp.name] = list(val)
                        except TypeError:
                            node_data[inp.name] = float(val)
            if node.type == 'TEX_IMAGE' and node.image:
                node_data["image"] = node.image.filepath
                node_data["size"] = [node.image.size[0], node.image.size[1]]
            info["nodes"].append(node_data)
        materials.append(info)
    return materials

print(json.dumps(extract_materials(), indent=2))
```

## 动画关键帧提取

```python
import bpy, json

def extract_animation(obj):
    if not obj.animation_data or not obj.animation_data.action:
        return None
    tracks = []
    for fc in obj.animation_data.action.fcurves:
        keyframes = []
        for kp in fc.keyframe_points:
            keyframes.append({
                "frame": int(kp.co[0]),
                "value": float(kp.co[1]),
                "interpolation": kp.interpolation,
            })
        tracks.append({
            "data_path": fc.data_path,
            "index": fc.array_index,
            "keyframes": keyframes,
        })
    return {"object": obj.name, "tracks": tracks}

animations = []
for obj in bpy.data.objects:
    anim = extract_animation(obj)
    if anim:
        animations.append(anim)

print(json.dumps(animations, indent=2))
```

## GLTF 导出设置参考

| 设置 | 值 | 原因 |
|---------|-------|-----|
| `export_format` | `'GLB'` | 单二进制文件，便于管理 |
| `export_apply` | `False` | 不烘焙修改器（如 Array） |
| `export_animations` | `True` | 包含动画数据 |
| `export_nla_strips` | `True` | 将 NLA 条带烘焙为动作 |
| `export_cameras` | `True` | 包含相机绑定 |
| `export_lights` | `False` | 在运行时处理灯光（Three.js/R3F） |
| `export_draco_mesh_compression_enable` | `False` | 后续通过 gltf-transform 应用 Draco 压缩 |

## 纹理优化流程

目标：在视觉质量可接受的前提下，生成最小的 GLB 文件。

```
Blender 导出（无 Draco 压缩）→ 调整大小（最大 1K）→ WebP 压缩（q90）→ Draco 压缩
   ~22 MB                    ~3.7 MB           ~3.7 MB      ~1 MB
```

关键要点：
- 4K 纹理（4096x4096）→ 每张纹理占用约 89 MB GPU 内存。1K 纹理 → 约 5.6 MB。**内存占用减少 16 倍**。
- PNG 格式的 metallicRoughness 纹理在 WebP 格式下以 85-90 的质量压缩效果良好。
- 移动 GPU（Adreno、Mali）从纹理降采样中获益最多。
- 可使用以下命令检查文件：`npx @gltf-transform/cli inspect model.glb`

## 资产集成

配置完成后，可通过 Blender MCP 使用以下集成工具：

| 集成工具 | 功能 |
|-------------|-------------|
| **PolyHaven** | 搜索、下载、导入免费 HDRI、纹理和 3D 模型，并自动设置材质 |
| **Sketchfab** | 搜索和下载模型（需要访问令牌） |
| **Hyper3D Rodin** | 根据文本描述或参考图像生成 3D 模型 |
| **Hunyuan3D** | 根据文本提示、图像或两者结合创建 3D 资产 |

## 已知错误与解决方法

完整错误列表请查看 [references/errors.md](references/errors.md)。

## 数据输出

- 小型结果（场景信息、单个对象）使用 `print()` + `json.dumps()` 输出
- 大型提取结果（完整层级结构、动画数据、材质报告）输出到 `/tmp/*.json`
- 始终包含元数据：场景名称、帧率、帧范围、Blender 版本