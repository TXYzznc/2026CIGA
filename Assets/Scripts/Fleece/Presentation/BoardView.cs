using UnityEngine;

public sealed class BoardView : MonoBehaviour
{
    [SerializeField] private Transform boardRoot;
    [SerializeField] private Transform cellRoot;
    [SerializeField] private Transform pieceRoot;
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private float hexSize = 1f;
    [SerializeField] private Color fallbackCellColor = new Color(0.18f, 0.2f, 0.22f, 0.35f);
    [SerializeField] private Color fallbackWallColor = new Color(0.08f, 0.08f, 0.08f, 1f);

    public Transform PieceRoot => pieceRoot != null ? pieceRoot : transform;

    public Vector3 HexToWorld(Hex hex)
    {
        var x = hexSize * Mathf.Sqrt(3f) * (hex.q + hex.r * 0.5f);
        var y = -hexSize * 1.5f * hex.r;
        var root = boardRoot != null ? boardRoot : transform;
        return root.TransformPoint(new Vector3(x, y, 0f));
    }

    public void SetOrientation(int boardOrientation)
    {
        var root = boardRoot != null ? boardRoot : transform;
        root.localRotation = Quaternion.Euler(0f, 0f, -60f * WrapOrientation(boardOrientation));
    }

    public void RebuildCells(Board board)
    {
        var parent = cellRoot != null ? cellRoot : transform;
        ClearChildren(parent);
        if (board == null)
        {
            return;
        }

        foreach (var cell in board.AllInsideCells())
        {
            var isWall = board.GetContent(cell) == CellContent.Wall;
            var prefab = isWall && wallPrefab != null ? wallPrefab : cellPrefab;
            var instance = prefab != null
                ? Instantiate(prefab, HexToWorld(cell), Quaternion.identity, parent)
                : CreateFallbackCell(isWall, parent);

            instance.transform.position = HexToWorld(cell);
            instance.name = $"Cell {cell}";
        }
    }

    private GameObject CreateFallbackCell(bool isWall, Transform parent)
    {
        var cell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cell.transform.SetParent(parent, false);
        cell.transform.localScale = new Vector3(hexSize * 0.9f, 0.025f, hexSize * 0.9f);
        cell.transform.localRotation = Quaternion.Euler(90f, 30f, 0f);

        var renderer = cell.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = isWall ? fallbackWallColor : fallbackCellColor;
        }

        return cell;
    }

    private static void ClearChildren(Transform root)
    {
        for (var i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

    private static int WrapOrientation(int orientation) => ((orientation % 6) + 6) % 6;
}

