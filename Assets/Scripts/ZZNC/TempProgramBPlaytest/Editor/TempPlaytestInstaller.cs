using UnityEditor;
using UnityEngine;

public static class TempPlaytestInstaller
{
    private const string ControllerName = "ZZNCTempProgramBPlaytest";

    [MenuItem("Tools/ZZNC/Install Temp Program-B Playtest")]
    public static void Install()
    {
        var old = GameObject.Find(ControllerName);
        var oldController = old != null ? old.GetComponent<TempPlaytestController>() : null;
        var hexCellPrefab = oldController != null ? GetObjectReference<GameObject>(oldController, "hexCellPrefab") : null;
        var hexClippedWallPrefab = oldController != null ? GetObjectReference<GameObject>(oldController, "hexClippedWallPrefab") : null;
        var hexWallPrefab = oldController != null ? GetObjectReference<GameObject>(oldController, "hexWallPrefab") : null;
        if (hexClippedWallPrefab == null)
            hexClippedWallPrefab = hexWallPrefab;
        if (old != null)
        {
            Object.DestroyImmediate(old);
        }

        var staticPreview = GameObject.Find("ZZNCPreview");
        if (staticPreview != null)
        {
            staticPreview.SetActive(false);
        }

        var root = new GameObject("ZZNCTempProgramBPlaytest");
        var controller = root.AddComponent<TempPlaytestController>();
        root.AddComponent<SmackResolver>();

        Assign(controller, "hexCellPrefab", hexCellPrefab);
        Assign(controller, "hexClippedWallPrefab", hexClippedWallPrefab);
        Assign(controller, "hexWallPrefab", hexWallPrefab);
        Assign(controller, "normalPieceSprite", Load<Sprite>("Assets/Resources/Sprite/ZZNC/Piece_Normal.png"));
        Assign(controller, "scorePieceSprite", Load<Sprite>("Assets/Resources/Sprite/ZZNC/Piece_Score.png"));
        Assign(controller, "explosionPieceSprite", Load<Sprite>("Assets/Resources/Sprite/ZZNC/Piece_Explosion.png"));
        Assign(controller, "splitPieceSprite", Load<Sprite>("Assets/Resources/Sprite/ZZNC/Piece_Split.png"));
        Assign(controller, "pieceMaterial", Load<Material>("Assets/Resources/Material/ZZNC/M_ZZNC_Piece.mat"));

        var camera = Camera.main;
        if (camera != null)
        {
            camera.orthographic = true;
            camera.orthographicSize = 7.2f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
            camera.backgroundColor = Color.black;
            EditorUtility.SetDirty(camera);
        }

        Selection.activeGameObject = root;
        EditorUtility.SetDirty(root);
        Debug.Log("[ZZNC.TempProgramB] Installed temporary playtest controller. Press Play, then use A/D, Space, R, 1-4, or mouse drag.");
    }

    [MenuItem("Tools/ZZNC/Migrate Wall Prefab References")]
    public static void MigrateWallPrefabReferences()
    {
        var controller = GameObject.Find(ControllerName)?.GetComponent<TempPlaytestController>();
        if (controller == null)
        {
            Debug.LogWarning($"[ZZNC.TempProgramB] Cannot find {ControllerName}.");
            return;
        }

        var clippedWallPrefab = GetObjectReference<GameObject>(controller, "hexClippedWallPrefab");
        var fullWallPrefab = GetObjectReference<GameObject>(controller, "hexWallPrefab");
        if (clippedWallPrefab == null && fullWallPrefab != null)
            Assign(controller, "hexClippedWallPrefab", fullWallPrefab);

        EditorUtility.SetDirty(controller);
        Debug.Log("[ZZNC.TempProgramB] Wall prefab references migrated. Replace hexClippedWallPrefab with clipped-wall art when ready.");
    }

    private static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            Debug.LogWarning($"[ZZNC.TempProgramB] Missing asset: {path}");
        }

        return asset;
    }

    private static T GetObjectReference<T>(Object target, string fieldName) where T : Object
    {
        var serializedObject = new SerializedObject(target);
        var property = serializedObject.FindProperty(fieldName);
        return property != null ? property.objectReferenceValue as T : null;
    }

    private static void Assign(Object target, string fieldName, Object value)
    {
        var serializedObject = new SerializedObject(target);
        var property = serializedObject.FindProperty(fieldName);
        if (property == null)
        {
            Debug.LogWarning($"[ZZNC.TempProgramB] Missing serialized field: {fieldName}");
            return;
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
