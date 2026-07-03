---
name: unity-editor-scripting
description: Unity Editor 扩展规范与三大模式示例。触发：Editor 扩展、EditorWindow、CustomEditor、PropertyDrawer、AssetPostprocessor、SerializedProperty、UI Toolkit Editor、IMGUI、MenuItem、Unity 工具开发。
tags: unity, editor, tooling, imgui, ui-toolkit
---

# Unity Editor 扩展规范

## 何时使用
- 给团队做关卡/数值/资源批处理工具。
- 自定义 Inspector 改善策划/美术工作流。
- 资源导入自动化（贴图压缩、模型轴向修正）。
- 编辑器内一键校验、批量替换、生成代码。

## 核心规则
- **三选型口诀**：
  - 独立工具窗口 → **EditorWindow**
  - 改 Inspector 显示 → **CustomEditor** / **Editor**
  - 改单个字段绘制 → **PropertyDrawer**
- **所有 Editor 代码必须在 `Editor/` 子文件夹下** — Unity 才会把它从打包剔除，否则报「UnityEditor 不存在于运行时」。
- **永远用 SerializedProperty 而不是直接读字段** — 自动支持 Undo、Multi-Edit、Prefab Override。
- **修改对象调用 Undo.RecordObject** — 漏调用 = 编辑器内改了不可撤销。
- **UI Toolkit (UIElements) 是 Unity 推荐方向**，但 IMGUI 仍适合简单/动态布局工具；新项目复杂窗口推荐 UI Toolkit，简单 Inspector 微调用 IMGUI 更快。

## 关键流程/模式

### 模式 A：EditorWindow 最小示例
独立窗口，菜单触发，含按钮 + 撤销。
```csharp
// Assets/Editor/MyToolWindow.cs
using UnityEditor;
using UnityEngine;

public class MyToolWindow : EditorWindow
{
    private string searchName = "";
    private Color tintColor = Color.white;

    [MenuItem("Tools/My Tool Window %#t")]   // Ctrl+Shift+T
    public static void Open() => GetWindow<MyToolWindow>("My Tool");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("批量改色工具", EditorStyles.boldLabel);
        searchName = EditorGUILayout.TextField("名称过滤", searchName);
        tintColor = EditorGUILayout.ColorField("Tint", tintColor);

        if (GUILayout.Button("应用到选中"))
        {
            foreach (var go in Selection.gameObjects)
            {
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr == null) continue;
                Undo.RecordObject(sr, "Apply Tint");      // 撤销集成
                sr.color = tintColor;
                EditorUtility.SetDirty(sr);               // 标脏，确保保存
            }
        }
    }
}
```

### 模式 B：CustomEditor 自定义 Inspector
```csharp
// Assets/Scripts/Enemy.cs（运行时）
using UnityEngine;
public class Enemy : MonoBehaviour
{
    public int hp = 100;
    public float speed = 3f;
    public bool isBoss;
    public int bossPhase = 1;
}

// Assets/Editor/EnemyEditor.cs（编辑器）
using UnityEditor;
[CustomEditor(typeof(Enemy)), CanEditMultipleObjects]
public class EnemyEditor : Editor
{
    SerializedProperty hp, speed, isBoss, bossPhase;

    void OnEnable()
    {
        hp = serializedObject.FindProperty("hp");
        speed = serializedObject.FindProperty("speed");
        isBoss = serializedObject.FindProperty("isBoss");
        bossPhase = serializedObject.FindProperty("bossPhase");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(hp);
        EditorGUILayout.PropertyField(speed);
        EditorGUILayout.PropertyField(isBoss);
        if (isBoss.boolValue)                            // 条件显示
            EditorGUILayout.PropertyField(bossPhase);
        serializedObject.ApplyModifiedProperties();      // 写回 + 自动 Undo
    }
}
```

### 模式 C：PropertyDrawer（自定义字段绘制）
```csharp
// 运行时：Attribute 定义
using UnityEngine;
public class MinMaxAttribute : PropertyAttribute
{
    public float min, max;
    public MinMaxAttribute(float min, float max) { this.min = min; this.max = max; }
}

// 运行时使用
public class Spawner : MonoBehaviour
{
    [MinMax(0, 10)] public float spawnRate = 5f;
}

// Editor/MinMaxDrawer.cs
using UnityEditor; using UnityEngine;
[CustomPropertyDrawer(typeof(MinMaxAttribute))]
public class MinMaxDrawer : PropertyDrawer
{
    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        var attr = (MinMaxAttribute)attribute;
        if (prop.propertyType == SerializedPropertyType.Float)
            prop.floatValue = EditorGUI.Slider(pos, label, prop.floatValue, attr.min, attr.max);
        else
            EditorGUI.LabelField(pos, label.text, "MinMax 仅支持 float");
    }
}
```

### 模式 D：AssetPostprocessor 导入钩子
```csharp
// Assets/Editor/TextureImportRules.cs
using UnityEditor; using UnityEngine;
public class TextureImportRules : AssetPostprocessor
{
    // 贴图导入前：路径含 /UI/ 自动设为 Sprite + Point + 无压缩
    void OnPreprocessTexture()
    {
        var imp = (TextureImporter)assetImporter;
        if (!assetPath.Contains("/UI/")) return;

        imp.textureType = TextureImporterType.Sprite;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        // 像素艺术常用
        imp.spritePixelsPerUnit = 16;
    }

    // 模型导入：自动禁用动画
    void OnPreprocessModel()
    {
        var imp = (ModelImporter)assetImporter;
        if (assetPath.Contains("/Props/"))
            imp.animationType = ModelImporterAnimationType.None;
    }
}
```

## EditorPrefs 持久化
```csharp
// 持久化跨会话设置（与项目无关：用 EditorPrefs；与项目相关：用 ScriptableObject）
const string KEY = "MyTool.LastPath";
string lastPath = EditorPrefs.GetString(KEY, Application.dataPath);
// ...
EditorPrefs.SetString(KEY, lastPath);
```

## UI Toolkit Editor 速写（复杂窗口推荐）
```csharp
using UnityEditor; using UnityEngine.UIElements;
public class UTKWindow : EditorWindow
{
    [MenuItem("Tools/UTK Window")]
    public static void Open() => GetWindow<UTKWindow>();

    public void CreateGUI()
    {
        var root = rootVisualElement;
        root.Add(new Label("UI Toolkit Window"));
        var btn = new Button(() => Debug.Log("clicked")) { text = "点我" };
        root.Add(btn);
        // 或加载 .uxml: var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("...");
        // tree.CloneTree(root);
    }
}
```

## 常见坑
- **「Build 失败 找不到 UnityEditor」**：Editor 代码没放在 `Editor/` 文件夹下，被编入运行时程序集。修：移到 `Editor/` 或加 asmdef 限定 Editor 平台。
- **「改了字段不保存」**：漏 `EditorUtility.SetDirty()` 或 `serializedObject.ApplyModifiedProperties()`。Prefab 还需 `PrefabUtility.RecordPrefabInstancePropertyModifications`。
- **「OnInspectorGUI 中 List 编辑乱跳」**：直接改 `target.list[i]` 而非用 SerializedProperty。修：全程走 SerializedProperty，自带数组支持。
- **「PropertyDrawer 高度不对」**：默认行高 18。多行需 override `GetPropertyHeight`。
- **「AssetPostprocessor 一直触发」**：在 OnPreprocess 里改了 import 设置但条件没收敛，导致死循环。修：先判断当前值是否已是目标值。
- **「Multi-Edit 异常」**：自定义 Editor 没加 `[CanEditMultipleObjects]` 或直接访问 `target` 而非 `targets`。
- **MenuItem 快捷键冲突**：`%` Ctrl/Cmd `#` Shift `&` Alt，避免与 Unity 默认（如 Ctrl+S）冲突。

## 代码/命令示例
```csharp
// MenuItem 验证函数：未选中物体时灰显
[MenuItem("Tools/Selected Only", true)]
static bool ValidateSelected() => Selection.activeGameObject != null;

[MenuItem("Tools/Selected Only")]
static void DoSelected() { /* ... */ }
```

```csharp
// 批量改 Prefab（带 Undo）
[MenuItem("Tools/Batch Rename Selected")]
static void BatchRename()
{
    Undo.SetCurrentGroupName("Batch Rename");
    int group = Undo.GetCurrentGroup();
    int i = 0;
    foreach (var go in Selection.gameObjects)
    {
        Undo.RecordObject(go, "Rename");
        go.name = $"Enemy_{i++:D3}";
        EditorUtility.SetDirty(go);
    }
    Undo.CollapseUndoOperations(group);
}
```

## 文件夹规范
```
Assets/
  Scripts/          # 运行时
    Enemy.cs
  Editor/           # 编辑器扩展（自动剔除出 Build）
    EnemyEditor.cs
    MyToolWindow.cs
    MinMaxDrawer.cs
  EditorResources/  # 编辑器用资源（图标等），放 Editor Default Resources
```
