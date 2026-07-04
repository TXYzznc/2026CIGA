using UnityEditor;
using UnityEngine;

public static class ZZNCTempProgramBPlaytestInstaller
{
    [MenuItem("Tools/ZZNC/Install Temp Program-B Playtest")]
    public static void Install()
    {
        var old = GameObject.Find("ZZNCTempProgramBPlaytest");
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
        var controller = root.AddComponent<ZZNCTempProgramBPlaytestController>();
        root.AddComponent<SmackResolver>();

        Assign(controller, "hexCellPrefab", Load<GameObject>("Assets/Prefabs/ZZNC/ZZNC_HexCell.prefab"));
        Assign(controller, "hexWallPrefab", Load<GameObject>("Assets/Prefabs/ZZNC/ZZNC_HexWall.prefab"));
        Assign(controller, "previewDotPrefab", Load<GameObject>("Assets/Prefabs/ZZNC/ZZNC_PreviewDot.prefab"));
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

    private static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            Debug.LogWarning($"[ZZNC.TempProgramB] Missing asset: {path}");
        }

        return asset;
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
