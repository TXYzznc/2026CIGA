using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Temporary Program-B replacement for validating Program-A round rules in an actual scene.
/// Delete the whole TempProgramBPlaytest folder after the real Program-B implementation is ready.
/// </summary>
public class ZZNCTempProgramBPlaytestController : MonoBehaviour, IBoardView, IPieceViewFactory, IHUDView
{
    private const float CellSize = 1.45f;
    private const float PieceZ = -0.05f;
    private const int BoardRadius = 3;

    [Header("Generated Prototype Prefabs")]
    [SerializeField] private GameObject hexCellPrefab;
    [SerializeField] private GameObject hexWallPrefab;
    [SerializeField] private GameObject previewDotPrefab;

    [Header("Generated Prototype Sprites")]
    [SerializeField] private Sprite normalPieceSprite;
    [SerializeField] private Sprite scorePieceSprite;
    [SerializeField] private Sprite explosionPieceSprite;
    [SerializeField] private Sprite splitPieceSprite;
    [SerializeField] private Material pieceMaterial;

    [Header("Runtime")]
    [SerializeField] private int layoutIndex;
    [SerializeField] private int boardOrientation;

    private readonly Board _board = new Board();
    private readonly Dictionary<Hex, GameObject> _cellObjects = new Dictionary<Hex, GameObject>();
    private readonly Dictionary<Piece, ZZNCTempProgramBPieceView> _pieceViews = new Dictionary<Piece, ZZNCTempProgramBPieceView>();
    private readonly List<GameObject> _previewObjects = new List<GameObject>();
    private Transform _cellsRoot;
    private Transform _piecesRoot;
    private Transform _effectsRoot;
    private SmackResolver _resolver;
    private bool _isResolving;

    private void Awake()
    {
        EnsureRoots();
        _resolver = gameObject.GetComponent<SmackResolver>();
        if (_resolver == null)
        {
            _resolver = gameObject.AddComponent<SmackResolver>();
        }

        _resolver.Init(_board, this, this, this);
        BuildLayout(layoutIndex);
    }

    private void Update()
    {
        if (_isResolving)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            boardOrientation = Hex.RotateDir(boardOrientation, 1);
            ApplyBoardRotation();
        }

        if (Input.GetKeyDown(KeyCode.D))
        {
            boardOrientation = Hex.RotateDir(boardOrientation, -1);
            ApplyBoardRotation();
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            ExecuteCurrentSmack();
        }
    }

    public Vector3 HexToWorld(Hex hex)
    {
        var x = Mathf.Sqrt(3f) * (hex.q + hex.r * 0.5f) * CellSize;
        var y = -1.5f * hex.r * CellSize;
        return transform.TransformPoint(new Vector3(x, y, 0f));
    }

    public IPieceView CreateView(PieceType type, Hex pos)
    {
        var view = CreatePieceView(type, pos);
        return view;
    }

    public void DestroyView(IPieceView view)
    {
        if (view is Component component && component != null)
        {
            Destroy(component.gameObject);
        }
    }

    public void ShowScorePop(int scoreDelta, int combo, Vector3 worldPos)
    {
        Debug.Log($"[ZZNC.TempProgramB] Score +{scoreDelta}, Combo {combo}, At {worldPos}");
    }

    private void EnsureRoots()
    {
        _cellsRoot = EnsureChild("Cells");
        _piecesRoot = EnsureChild("Pieces");
        _effectsRoot = EnsureChild("Effects");
    }

    private Transform EnsureChild(string childName)
    {
        var child = transform.Find(childName);
        if (child != null)
        {
            return child;
        }

        var go = new GameObject(childName);
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    private void BuildLayout(int index)
    {
        layoutIndex = Mathf.Clamp(index, 0, 3);
        _isResolving = false;
        ClearChildren(_cellsRoot);
        ClearChildren(_piecesRoot);
        ClearChildren(_effectsRoot);
        _cellObjects.Clear();
        _pieceViews.Clear();
        _previewObjects.Clear();
        _board.Clear();
        _board.SetShape(MakeHexagonShape(BoardRadius));

        var walls = GetWalls(layoutIndex);
        foreach (var wall in walls)
        {
            _board.PlaceWall(wall);
        }

        foreach (var cell in _board.AllInsideCells())
        {
            var prefab = walls.Contains(cell) ? hexWallPrefab : hexCellPrefab;
            var cellObject = Instantiate(prefab, HexToWorld(cell), Quaternion.identity, _cellsRoot);
            cellObject.name = walls.Contains(cell) ? $"Wall_{cell.q}_{cell.r}" : $"Cell_{cell.q}_{cell.r}";
            _cellObjects[cell] = cellObject;
        }

        BuildPieces(layoutIndex);
        ApplyBoardRotation();
        RefreshPreview();
        Debug.Log($"[ZZNC.TempProgramB] Layout {layoutIndex + 1} ready. A/D rotates the board visually, Space resolves one smack.");
    }

    private void BuildPieces(int index)
    {
        switch (index)
        {
            case 0:
                PlacePiece(PieceType.Normal, new Hex(0, -3));
                PlacePiece(PieceType.Score, new Hex(0, 2));
                PlacePiece(PieceType.Explosion, new Hex(1, 1));
                PlacePiece(PieceType.Split, new Hex(-1, 2));
                PlacePiece(PieceType.Normal, new Hex(1, 2));
                PlacePiece(PieceType.Normal, new Hex(-2, 2));
                break;
            case 1:
                PlacePiece(PieceType.Normal, new Hex(0, -3));
                PlacePiece(PieceType.Explosion, new Hex(0, 1));
                PlacePiece(PieceType.Normal, new Hex(1, 1));
                PlacePiece(PieceType.Score, new Hex(-1, 2));
                PlacePiece(PieceType.Split, new Hex(2, -1));
                break;
            case 2:
                PlacePiece(PieceType.Normal, new Hex(0, -3));
                PlacePiece(PieceType.Split, new Hex(0, 1));
                PlacePiece(PieceType.Normal, new Hex(1, 1));
                PlacePiece(PieceType.Normal, new Hex(2, 1));
                PlacePiece(PieceType.Score, new Hex(-1, 2));
                break;
            case 3:
                PlacePiece(PieceType.Normal, new Hex(-2, -1));
                PlacePiece(PieceType.Score, new Hex(-1, 0));
                PlacePiece(PieceType.Explosion, new Hex(0, 1));
                PlacePiece(PieceType.Split, new Hex(1, 1));
                PlacePiece(PieceType.Normal, new Hex(2, -2));
                PlacePiece(PieceType.Normal, new Hex(-3, 3));
                PlacePiece(PieceType.Score, new Hex(3, -1));
                break;
        }
    }

    private void PlacePiece(PieceType type, Hex pos)
    {
        if (_board.GetContent(pos) != CellContent.Empty)
        {
            Debug.LogWarning($"[ZZNC.TempProgramB] Cannot place {type} at {pos}; cell content is {_board.GetContent(pos)}.");
            return;
        }

        var piece = new Piece { Type = type };
        var view = CreatePieceView(type, pos);
        piece.View = view;
        _board.PlacePiece(piece, pos);
        _pieceViews[piece] = view;
    }

    private ZZNCTempProgramBPieceView CreatePieceView(PieceType type, Hex pos)
    {
        var go = new GameObject($"Piece_{type}_{pos.q}_{pos.r}");
        go.transform.SetParent(_piecesRoot);
        go.transform.position = HexToWorld(pos) + new Vector3(0f, 0f, PieceZ);
        go.transform.localScale = Vector3.one * 0.78f;

        var view = go.AddComponent<ZZNCTempProgramBPieceView>();
        view.Init(GetPieceSprite(type), pieceMaterial, 2);
        return view;
    }

    private Sprite GetPieceSprite(PieceType type)
    {
        switch (type)
        {
            case PieceType.Score:
                return scorePieceSprite;
            case PieceType.Explosion:
                return explosionPieceSprite;
            case PieceType.Split:
                return splitPieceSprite;
            default:
                return normalPieceSprite;
        }
    }

    private void ExecuteCurrentSmack()
    {
        _isResolving = true;
        ClearPreview();
        var gravityDir = Hex.OrientationToGravityDir(boardOrientation);
        Debug.Log($"[ZZNC.TempProgramB] Smack start. Orientation={boardOrientation}, GravityDir=D{gravityDir}");

        _resolver.ExecuteSmack(boardOrientation, SmackRules.Default, result =>
        {
            _isResolving = false;
            RemoveDeadViewEntries();
            RefreshPreview();
            Debug.Log($"[ZZNC.TempProgramB] Smack stable. Score={result.ScoreGained}, MaxCombo={result.MaxCombo}, Overflow={result.EventOverflow}");
        });
    }

    private void ApplyBoardRotation()
    {
        var gravityDir = Hex.OrientationToGravityDir(boardOrientation);
        var localGravity = HexToLocal(new Hex(0, 0).Neighbor(gravityDir));
        var z = Vector2.SignedAngle(localGravity, Vector2.down);
        transform.rotation = Quaternion.Euler(0f, 0f, z);
        RefreshPreview();
        Debug.Log($"[ZZNC.TempProgramB] Board rotated. Orientation={boardOrientation}, rule gravity=D{gravityDir}, visual gravity=screen down.");
    }

    private void RefreshPreview()
    {
        ClearPreview();

        if (previewDotPrefab == null || _resolver == null)
        {
            return;
        }

        var preview = _resolver.SimulateSmack(boardOrientation);
        foreach (var kv in preview.FinalPositions)
        {
            var piece = _board.GetPieceById(kv.Key);
            if (piece == null || piece.Position == kv.Value)
            {
                continue;
            }

            var dot = Instantiate(previewDotPrefab, HexToWorld(kv.Value) + new Vector3(0f, 0f, -0.1f), Quaternion.identity, _effectsRoot);
            dot.name = $"Preview_{kv.Key}_{kv.Value.q}_{kv.Value.r}";
            _previewObjects.Add(dot);
        }
    }

    private void ClearPreview()
    {
        foreach (var obj in _previewObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }

        _previewObjects.Clear();
    }

    private void RemoveDeadViewEntries()
    {
        var dead = new List<Piece>();
        foreach (var kv in _pieceViews)
        {
            if (_board.GetPieceById(kv.Key.ID) == null)
            {
                dead.Add(kv.Key);
            }
        }

        foreach (var piece in dead)
        {
            _pieceViews.Remove(piece);
        }
    }

    private static HashSet<Hex> GetWalls(int index)
    {
        var walls = new HashSet<Hex>();
        switch (index)
        {
            case 0:
                walls.Add(new Hex(-1, 1));
                walls.Add(new Hex(1, -1));
                walls.Add(new Hex(2, -2));
                break;
            case 1:
                walls.Add(new Hex(-1, 1));
                walls.Add(new Hex(1, -2));
                walls.Add(new Hex(2, -2));
                break;
            case 2:
                walls.Add(new Hex(3, -1));
                walls.Add(new Hex(-1, 1));
                walls.Add(new Hex(-2, 2));
                break;
            case 3:
                walls.Add(new Hex(0, 0));
                walls.Add(new Hex(1, -1));
                walls.Add(new Hex(-1, 1));
                walls.Add(new Hex(2, -1));
                break;
        }

        return walls;
    }

    private static IEnumerable<Hex> MakeHexagonShape(int radius)
    {
        for (var q = -radius; q <= radius; q++)
        {
            var r1 = Math.Max(-radius, -q - radius);
            var r2 = Math.Min(radius, -q + radius);
            for (var r = r1; r <= r2; r++)
            {
                yield return new Hex(q, r);
            }
        }
    }

    private static Vector2 HexToLocal(Hex hex)
    {
        var x = Mathf.Sqrt(3f) * (hex.q + hex.r * 0.5f) * CellSize;
        var y = -1.5f * hex.r * CellSize;
        return new Vector2(x, y);
    }

    private static void ClearChildren(Transform root)
    {
        for (var i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }
}
