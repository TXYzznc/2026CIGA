---
name: unity-skills
description: Unity Editor REST API。优先使用批量操作：处理2个及以上对象时，请使用*_batch接口。
tags: unity-editor-api, batch-operations, gameobject-management, unity-asset-handling,
  ui-automation
tags_cn: Unity Editor API, 批量操作, 游戏对象管理, Unity资源处理, UI自动化
---

# Unity Skills API

> **规则**：处理2个及以上对象时，请使用`*_batch`技能（1次调用替代N次调用）

## 批量技能（优先使用！）

| 技能 | 项格式 |
|-------|--------------|
| `gameobject_create_batch` | `[{name, primitiveType?, x?, y?, z?, parentName?}]` |
| `gameobject_delete_batch` | `[{name?, instanceId?}]` or `["name1","name2"]` |
| `gameobject_set_transform_batch` | `[{name, posX?, posY?, posZ?, rotX?, rotY?, rotZ?, scaleX?, scaleY?, scaleZ?}]` |
| `gameobject_set_active_batch` | `[{name, active}]` |
| `gameobject_set_parent_batch` | `[{childName, parentName}]` |
| `gameobject_duplicate_batch` | `[{name?, instanceId?}]` |
| `gameobject_rename_batch` | `[{instanceId, newName}]` |
| `gameobject_set_layer_batch` | `[{name, layer}]` |
| `gameobject_set_tag_batch` | `[{name, tag}]` |
| `component_add_batch` | `[{name, componentType}]` |
| `component_remove_batch` | `[{name, componentType}]` |
| `component_set_property_batch` | `[{name, componentType, propertyName, value}]` |
| `material_create_batch` | `[{name, shaderName?, savePath?}]` |
| `material_assign_batch` | `[{name, materialPath}]` |
| `material_set_colors_batch` | `[{name?, path?, r, g, b, a?, intensity?}]` |
| `material_set_emission_batch` | `[{name?, path?, r, g, b, intensity?}]` |
| `prefab_instantiate_batch` | `[{prefabPath, x?, y?, z?, rotX?, rotY?, rotZ?, name?, parentName?}]` |
| `asset_import_batch` | `[{sourcePath, destinationPath}]` |
| `asset_delete_batch` | `[{path}]` |
| `asset_move_batch` | `[{sourcePath, destinationPath}]` |
| `ui_create_batch` | `[{type, name, parent?, text?, width?, height?, x?, y?}]` type: Button/Text/Image/Panel/Slider/Toggle/InputField |
| `script_create_batch` | `[{scriptName, folder?, template?, namespace?}]` |
| `light_set_properties_batch` | `[{name, intensity?, r?, g?, b?, range?, shadows?}]` |
| `light_set_enabled_batch` | `[{name, enabled}]` |
| `texture_set_settings_batch` | `[{assetPath, textureType?, maxSize?, filterMode?, compression?, mipmapEnabled?, spritePixelsPerUnit?}]` |
| `audio_set_settings_batch` | `[{assetPath, loadType?, compressionFormat?, quality?, forceToMono?}]` |
| `model_set_settings_batch` | `[{assetPath, animationType?, meshCompression?, importAnimation?}]` |

## 单个技能

### gameobject
| 技能 | 参数 |
|-------|------------|
| `gameobject_create` | name?, primitiveType?, x?, y?, z?, parentName? |
| `gameobject_delete` | name?, instanceId?, path? |
| `gameobject_find` | name?, tag?, layer?, component?, useRegex?, limit? → 返回列表 |
| `gameobject_get_info` | name?, instanceId?, path? |
| `gameobject_set_transform` | name, posX?, posY?, posZ?, rotX?, rotY?, rotZ?, scaleX?, scaleY?, scaleZ? |
| `gameobject_set_parent` | name, parentName |
| `gameobject_set_active` | name, active |
| `gameobject_duplicate` | name?, instanceId? → 返回copyName, copyInstanceId |
| `gameobject_rename` | name?, instanceId?, newName |

primitiveType可选值：Cube、Sphere、Capsule、Cylinder、Plane、Quad、Empty(null)

### component
| 技能 | 参数 |
|-------|------------|
| `component_add` | name, componentType |
| `component_remove` | name, componentType |
| `component_list` | name → 返回components[] |
| `component_set_property` | name, componentType, propertyName, value |
| `component_get_properties` | name, componentType |

componentType可选值：Rigidbody、BoxCollider、SphereCollider、CapsuleCollider、MeshCollider、CharacterController、AudioSource、Light、Camera、Animator等

### material
| 技能 | 参数 |
|-------|------------|
| `material_create` | name, shaderName?, savePath? |
| `material_assign` | name?, instanceId?, path?, materialPath |
| `material_set_color` | name?, path?, r, g, b, a?, propertyName?, intensity? |
| `material_set_emission` | name?, path?, r, g, b, intensity?, enableEmission? |
| `material_set_texture` | name?, path?, texturePath, propertyName? |
| `material_set_float` | name?, path?, propertyName, value |
| `material_set_int` | name?, path?, propertyName, value |
| `material_set_vector` | name?, path?, propertyName, x, y, z?, w? |
| `material_set_keyword` | name?, path?, keyword, enable? |
| `material_set_render_queue` | name?, path?, renderQueue |
| `material_set_shader` | name?, path?, shaderName |
| `material_set_texture_offset` | name?, path?, propertyName?, x, y |
| `material_set_texture_scale` | name?, path?, propertyName?, x, y |
| `material_get_properties` | name?, path? |
| `material_get_keywords` | name?, path? |
| `material_duplicate` | sourcePath, newName, savePath? |

### scene
| 技能 | 参数 |
|-------|------------|
| `scene_create` | scenePath |
| `scene_load` | scenePath, additive? |
| `scene_save` | scenePath? |
| `scene_get_info` | 无参数 → 返回name、path、isDirty、rootObjects |
| `scene_get_hierarchy` | maxDepth? → 返回层级树 |
| `scene_screenshot` | filename?, width?, height? |

### light
| 技能 | 参数 |
|-------|------------|
| `light_create` | name?, lightType?, x?, y?, z?, r?, g?, b?, intensity?, range?, spotAngle?, shadows? |
| `light_set_properties` | name, r?, g?, b?, intensity?, range?, shadows? |
| `light_get_info` | name |
| `light_find_all` | lightType?, limit? → 返回列表 |
| `light_set_enabled` | name, enabled |

lightType可选值：Directional、Point、Spot、Area | shadows可选值：none、hard、soft

### prefab
| 技能 | 参数 |
|-------|------------|
| `prefab_create` | gameObjectName?, instanceId?, savePath |
| `prefab_instantiate` | prefabPath, x?, y?, z?, name?, parentName? |
| `prefab_apply` | gameObjectName?, instanceId? |
| `prefab_unpack` | gameObjectName?, instanceId?, completely? |

### asset
| 技能 | 参数 |
|-------|------------|
| `asset_import` | sourcePath, destinationPath |
| `asset_delete` | assetPath |
| `asset_move` | sourcePath, destinationPath |
| `asset_duplicate` | assetPath |
| `asset_find` | searchFilter, searchInFolders?, limit? → 返回路径列表 |
| `asset_create_folder` | folderPath |
| `asset_refresh` | 无参数 |
| `asset_get_info` | assetPath |

searchFilter可选值：t:Texture2D、t:Material、t:Prefab、t:AudioClip、t:Script、name、l:Label

### ui
| 技能 | 参数 |
|-------|------------|
| `ui_create_canvas` | name?, renderMode? |
| `ui_create_panel` | name?, parent?, r?, g?, b?, a?, width?, height? |
| `ui_create_button` | name?, parent?, text?, width?, height?, x?, y? |
| `ui_create_text` | name?, parent?, text?, fontSize?, r?, g?, b?, a?, width?, height? |
| `ui_create_image` | name?, parent?, spritePath?, r?, g?, b?, a?, width?, height? |
| `ui_create_inputfield` | name?, parent?, placeholder?, width?, height? |
| `ui_create_slider` | name?, parent?, minValue?, maxValue?, value?, width?, height? |
| `ui_create_toggle` | name?, parent?, label?, isOn? |
| `ui_set_text` | name, text |
| `ui_find_all` | uiType?, limit? |

renderMode可选值：ScreenSpaceOverlay、ScreenSpaceCamera、WorldSpace | uiType可选值：Button、Text、Image、Panel、Slider、Toggle、InputField

### script
| 技能 | 参数 |
|-------|------------|
| `script_create` | scriptName, folder?, template?, namespace? |
| `script_read` | scriptPath → 返回内容 |
| `script_delete` | scriptPath |
| `script_find_in_file` | pattern, folder?, isRegex?, limit? → 返回匹配结果 |
| `script_append` | scriptPath, content, atLine? |

template可选值：MonoBehaviour、ScriptableObject、Editor、EditorWindow

### editor
| 技能 | 参数 |
|-------|------------|
| `editor_play` | 无参数 |
| `editor_stop` | 无参数 |
| `editor_pause` | 无参数 |
| `editor_select` | gameObjectName?, instanceId? |
| `editor_get_selection` | 无参数 → 返回包含instanceId的选中对象 |
| `editor_get_context` | includeComponents?, includeChildren? → 返回选中内容、资源、场景信息 |
| `editor_undo` | 无参数 |
| `editor_redo` | 无参数 |
| `editor_get_state` | 无参数 → 返回isPlaying、isPaused、isCompiling |
| `editor_execute_menu` | menuPath |
| `editor_get_tags` | 无参数 |
| `editor_get_layers` | 无参数 |

menuPath示例："File/Save"、"Edit/Play"、"GameObject/Create Empty"、"Assets/Refresh"

### animator
| 技能 | 参数 |
|-------|------------|
| `animator_create_controller` | name, folder? |
| `animator_add_parameter` | controllerPath, paramName, paramType, defaultValue? |
| `animator_get_parameters` | controllerPath |
| `animator_set_parameter` | name, paramName, paramType, floatValue?/intValue?/boolValue? |
| `animator_play` | name, stateName, layer?, normalizedTime? |
| `animator_get_info` | name |
| `animator_assign_controller` | name, controllerPath |
| `animator_list_states` | controllerPath, layer? |

paramType可选值：float、int、bool、trigger

### shader
| 技能 | 参数 |
|-------|------------|
| `shader_create` | shaderName, savePath, template? |
| `shader_read` | shaderPath |
| `shader_list` | filter?, limit? |

template可选值：Unlit、Standard、Transparent

### workflow（持久化历史）
| 技能 | 参数 |
|-------|------------|
| `workflow_task_start` | tag, description? |
| `workflow_task_end` | 无参数 |
| `workflow_snapshot_object` | name?, instanceId? |
| `workflow_list` | 无参数 |
| `workflow_revert_task` | taskId |
| `workflow_delete_task` | taskId |

### console
| 技能 | 参数 |
|-------|------------|
| `console_start_capture` | 无参数 |
| `console_stop_capture` | 无参数 |
| `console_get_logs` | filter?, limit? |
| `console_clear` | 无参数 |
| `console_log` | message, type? |

filter/type可选值：Log、Warning、Error

### validation
| 技能 | 参数 |
|-------|------------|
| `validate_scene` | checkMissingScripts?, checkMissingPrefabs?, checkDuplicateNames? |
| `validate_find_missing_scripts` | searchInPrefabs? |
| `validate_fix_missing_scripts` | dryRun?（默认值true） |
| `validate_cleanup_empty_folders` | rootPath?, dryRun?（默认值true） |
| `validate_find_unused_assets` | assetType?, limit? |
| `validate_texture_sizes` | maxRecommendedSize?, limit? |
| `validate_project_structure` | rootPath?, maxDepth? |

### importer
| 技能 | 参数 |
|-------|------------|
| `texture_get_settings` | assetPath |
| `texture_set_settings` | assetPath, textureType?, maxSize?, filterMode?, compression?, mipmapEnabled?, sRGB?, readable?, spritePixelsPerUnit?, wrapMode? |
| `audio_get_settings` | assetPath |
| `audio_set_settings` | assetPath, forceToMono?, loadInBackground?, preloadAudioData?, loadType?, compressionFormat?, quality? |
| `model_get_settings` | assetPath |
| `model_set_settings` | assetPath, globalScale?, meshCompression?, isReadable?, generateSecondaryUV?, importBlendShapes?, importCameras?, importLights?, animationType?, importAnimation?, materialImportMode? |

textureType可选值：Default、NormalMap、Sprite、EditorGUI、Cursor、Cookie、Lightmap、SingleChannel
filterMode可选值：Point、Bilinear、Trilinear | compression可选值：None、LowQuality、Normal、HighQuality
loadType可选值：DecompressOnLoad、CompressedInMemory、Streaming | compressionFormat可选值：PCM、Vorbis、ADPCM
animationType可选值：None、Legacy、Generic、Humanoid | meshCompression可选值：Off、Low、Medium、High

### physics
| 技能 | 参数 |
|-------|------------|
| `physics_raycast` | originX, originY, originZ, dirX, dirY, dirZ, maxDistance?, layerMask? |
| `physics_check_overlap` | x, y, z, radius, layerMask? |
| `physics_get_gravity` | 无参数 |
| `physics_set_gravity` | x, y, z |

### camera
| 技能 | 参数 |
|-------|------------|
| `camera_align_view_to_object` | objectName |
| `camera_get_info` | 无参数 |
| `camera_set_transform` | posX, posY, posZ, rotX, rotY, rotZ, size?, instant? |
| `camera_look_at` | x, y, z |

### navmesh
| 技能 | 参数 |
|-------|------------|
| `navmesh_bake` | 无参数 |
| `navmesh_calculate_path` | startX, startY, startZ, endX, endY, endZ, areaMask? |

### timeline
| 技能 | 参数 |
|-------|------------|
| `timeline_create` | name, folder? |
| `timeline_add_audio_track` | directorObjectName, trackName? |
| `timeline_add_animation_track` | directorObjectName, trackName?, bindingObjectName? |

### cinemachine
| 技能 | 参数 |
|-------|------------|
| `cinemachine_create_vcam` | name |
| `cinemachine_set_vcam_property` | vcamName, componentType, propertyName, value |
| `cinemachine_set_targets` | vcamName, followName?, lookAtName? |

### event
| 技能 | 参数 |
|-------|------------|
| `event_get_listeners` | objectName, componentName, eventName |
| `event_add_listener` | objectName, componentName, eventName, targetObjectName, ... |
| `event_invoke` | objectName, componentName, eventName |

### project
| 技能 | 参数 |
|-------|------------|
| `project_get_info` | 无参数 |
| `project_list_shaders` | filter?, limit? |
| `project_get_quality_settings` | 无参数 |

### optimization
| 技能 | 参数 |
|-------|------------|
| `optimize_textures` | maxTextureSize?, enableCrunch?, compressionQuality? |
| `optimize_mesh_compression` | compressionLevel, filter? |

### profiler
| 技能 | 参数 |
|-------|------------|
| `profiler_get_stats` | 无参数 |

### package
| 技能 | 参数 |
|-------|------------|
| `package_list` | 无参数 → 返回已安装包列表 |
| `package_check` | packageId → 返回installed、version |
| `package_install` | packageId, version? |
| `package_remove` | packageId |
| `package_refresh` | 无参数 |
| `package_install_cinemachine` | version?（可选值2或3，默认3） |
| `package_get_cinemachine_status` | 无参数 → 返回cinemachine/splines状态 |

## 注意事项
- 响应格式：`{success: true/false, ...data}` 或 `{success: false, error: "message"}`
- 所有操作失败时会自动回滚
- 执行`script_create`后，请等待3-5秒，待Unity完成重新编译
- 建议使用`instanceId`（来自`editor_get_selection`/`editor_get_context`）确保对象唯一性
- `name?`表示可使用名称、instanceId或路径来识别对象

## 多项目路由（重要）

Unity Editor REST 服务器**动态**分配端口——同时开多个 Unity 项目时端口按启动顺序递增（8090、8091、8092...）。服务端在 `~/.unity_skills/registry.json` 按项目路径登记 `{id, name, path, port, pid, ...}`。

**客户端 (`unity_skills.py`) 按以下优先级链解析目标端口**：

| 优先级 | 来源 | 场景 |
|---|---|---|
| 1 | `--port=<num>` CLI 参数 | 显式指定端口（最高优先级） |
| 2 | `--target=<name-or-id>` CLI 参数 | 按项目名 / ID 反查 registry |
| 3 | `UNITY_SKILLS_TARGET` 环境变量 | 会话级默认目标 |
| 4 | **cwd 匹配**（默认） | 从项目目录（或其子目录）跑，自动落到该项目 |
| 5 | `DEFAULT_PORT = 8090`（兜底 + stderr warning） | 未匹配任何 registry 条目 |

### 排障：health 命令

任何时候不清楚客户端打到哪个端口，跑：

```bash
python .claude/skills/unity-skills/scripts/unity_skills.py health
```

输出示例（当前项目命中）：
```json
{
  "url": "http://localhost:8090",
  "source": "cwd-match(GameDesinger)",
  "ok": true
}
```

- `source: "cwd-match(<name>)"` → cwd 匹配到 registry 项目
- `source: "cli-target(<x>)"` / `"cli-port(<n>)"` → CLI 参数覆盖
- `source: "env(<x>)"` → 环境变量覆盖
- `source: "default"` → 未匹配，回退 8090（会在 stderr 打 warning）

### 跨项目调用示例

```bash
# 从当前项目目录调其他项目（按项目名）
python .claude/skills/unity-skills/scripts/unity_skills.py editor_get_selection --target=OtherProject

# 显式指定端口
python .claude/skills/unity-skills/scripts/unity_skills.py editor_get_selection --port=8091

# 会话级绑定目标
export UNITY_SKILLS_TARGET=OtherProject
python .claude/skills/unity-skills/scripts/unity_skills.py editor_get_selection
```

### registry.json schema（只读消费）

`~/.unity_skills/registry.json` 由服务端维护，客户端只读：

```json
{
  "D:\\unity\\UnityProject\\GameDesinger": {
    "id": "GameDesinger_A1B2",
    "name": "GameDesinger",
    "path": "D:\\unity\\UnityProject\\GameDesinger",
    "port": 8090,
    "pid": 12345
  }
}
```

- `path` / `name` / `id` 用于匹配；`port` 是目标
- registry 缺失或损坏时静默回退 8090（不抛异常）

## 中文 / CJK 参数调用约定（强制）

**任何参数包含 CJK / Emoji / Latin-1 范围外字符时，必须用 `--stdin-json` 模式调用**，绝不可走 `key=value` argv。

### 背景

Windows cp936 (GBK) 默认 code page 下，agent 胶水层 → Python CLI 之间的 `argv` 路径存在历史性编码隐患：UTF-8 字节流被按 Latin-1 / cp1252 重读会出现 `ȡ` / `ͣ` / `Ƴ` / `Ƴ` / U+FFFD 等残片字符（13 期 Prefab 损坏的根因签名）。即使 Python 3.6+ 已用 `wmain` 接 Unicode argv，下游胶水层、subprocess 包装层、跨版本 Python 仍可能再次损坏。

`--stdin-json` 走 binary pipe，**完全绕开**任何 ANSI / argv / shell 解析层，**字节级 round-trip 100% 干净**。

### 正确调用方式

```bash
# ✅ 正确：CJK / Emoji / 复杂参数走 --stdin-json
echo '{"name":"TitleText","text":"设置"}' | python unity_skills.py ui_set_text --stdin-json

echo '{"name":"纹身工作台","primitiveType":"Cube"}' | python unity_skills.py gameobject_create --stdin-json
```

```python
# ✅ Python subprocess 调用（agent / 测试脚本）
import subprocess, json
params = {"name": "DescLabel", "text": "Settings 设置 ▶ 音量"}
proc = subprocess.run(
    [sys.executable, "unity_skills.py", "ui_set_text", "--stdin-json"],
    input=json.dumps(params, ensure_ascii=False).encode('utf-8'),
    capture_output=True,
)
```

### 错误调用方式

```bash
# ❌ 错误：CJK 走 argv，存在跨版本 / 跨 shell 编码风险
python unity_skills.py ui_set_text name=TitleText text=设置
python unity_skills.py gameobject_create name=纹身工作台 primitiveType=Cube
```

### 何时可以用 argv（key=value）

仅当**所有参数都是 ASCII**（英文 / 数字 / 常规符号）时，可继续用传统 `key=value` 写法：

```bash
# ✅ 纯 ASCII 参数，argv 模式 OK
python unity_skills.py gameobject_create name=Cube primitiveType=Cube x=0 y=0 z=0
```

### MSYS / Cygwin / WSL / 非标准 Python 环境

这些环境下 `ctypes.windll` 不可用，Python 3.14 之前的 wmain 防护可能缺失。**强制走 `--stdin-json`**——这是唯一跨环境 100% 安全的入口。

### 设计依据

- Phase 0 复现日志：[`openspec/changes/14-mcp-encoding-fix/tests/repro/repro-log.md`](../../../openspec/changes/14-mcp-encoding-fix/tests/repro/repro-log.md)
- 设计文档（4 路防御）：[`openspec/changes/14-mcp-encoding-fix/design.md`](../../../openspec/changes/14-mcp-encoding-fix/design.md)
- 13 期 Prefab 残片复盘：`openspec/changes/archive/2026-06-29-13-fix-broken-prefabs/art/raw/mcp-spike-report.md`