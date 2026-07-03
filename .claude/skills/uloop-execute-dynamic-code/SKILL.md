---
name: uloop-execute-dynamic-code
description: 通过uloop CLI在Unity Editor中动态执行C#代码。用于编辑器自动化场景：(1) 预制件/材质关联及AddComponent操作，(2)
  借助SerializedObject进行引用关联，(3) 场景/层级编辑及批量操作。不支持文件I/O或脚本编写操作。
tags: unity-editor-automation, uloop-cli, dynamic-csharp-execution, prefab-operations,
  scene-editing
tags_cn: Unity编辑器自动化, uloop CLI工具, 动态C#代码执行, 预制件操作, 场景编辑
---

# uloop execute-dynamic-code

在Unity Editor中动态执行C#代码。

## 使用方法

```bash
uloop execute-dynamic-code --code '<c# code>'
```

## 参数

| 参数 | 类型 | 说明 |
|-----------|------|-------------|
| `--code` | string | 要执行的C#代码（直接写语句，无需类包装） |
| `--compile-only` | boolean | 仅编译不执行 |
| `--auto-qualify-unity-types-once` | boolean | 自动限定Unity类型 |

## 代码格式

只需编写直接执行的语句（无需类/命名空间/方法）。返回值可选。

```csharp
// 顶部的using指令会被提升
using UnityEngine;
var x = Mathf.PI;
return x;
```

## 字符串字面量（Shell相关）

| Shell环境 | 写法 |
|-------|--------|
| bash/zsh/MINGW64/Git Bash | `'Debug.Log("Hello!");'` |
| PowerShell | `'Debug.Log(""Hello!"");'` |

## 允许的操作

- 预制件/材质关联（PrefabUtility）
- 添加组件 + 引用关联（SerializedObject）
- 场景/层级编辑
- 检视面板修改

## 禁止的操作

- System.IO.*（文件/目录/路径相关操作）
- AssetDatabase.CreateFolder / 文件写入
- 创建/编辑.cs/.asmdef文件

## 示例

### bash / zsh / MINGW64 / Git Bash

```bash
uloop execute-dynamic-code --code 'return Selection.activeGameObject?.name;'
uloop execute-dynamic-code --code 'new GameObject("MyObject");'
uloop execute-dynamic-code --code 'UnityEngine.Debug.Log("Hello from CLI!");'
```

### PowerShell

```powershell
uloop execute-dynamic-code --code 'return Selection.activeGameObject?.name;'
uloop execute-dynamic-code --code 'new GameObject(""MyObject"");'
uloop execute-dynamic-code --code 'UnityEngine.Debug.Log(""Hello from CLI!"");'
```

## 输出

返回包含执行结果或编译错误的JSON数据。

## 注意事项

如需进行文件/目录操作，请使用终端命令。

## 按分类划分的代码示例

如需查看详细代码示例，请参考以下文件：

- **预制件操作**：查看 [examples/prefab-operations.md](examples/prefab-operations.md)
  - 创建预制件、实例化、添加组件、修改属性
- **材质操作**：查看 [examples/material-operations.md](examples/material-operations.md)
  - 创建材质、设置着色器/纹理、修改属性
- **资源操作**：查看 [examples/asset-operations.md](examples/asset-operations.md)
  - 查找/搜索资源、复制、移动、重命名、加载
- **ScriptableObject**：查看 [examples/scriptableobject.md](examples/scriptableobject.md)
  - 创建ScriptableObject、借助SerializedObject修改
- **场景操作**：查看 [examples/scene-operations.md](examples/scene-operations.md)
  - 创建/修改GameObject、设置父对象、关联引用、加载场景