using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct PieceEntry
{
    public PieceType type;
    public int q;
    public int r;
}

[System.Serializable]
public struct WallEntry
{
    public int q;
    public int r;
}

/// <summary>
/// 在 Inspector 里拖 Sprite + 配棋子列表，运行时按 Tab 键热重载布局。
/// </summary>
public class ZZNCTempProgramBPlaytestController : MonoBehaviour, IBoardView, IPieceViewFactory, IHUDView
{
    private const float CellSize = 1.45f;
    private const float PieceZ = -0.05f;
    private const int BoardRadius = 3;

    [Header("=== 棋盘 Prefab（拖一次）===")]
    [SerializeField] private GameObject hexCellPrefab;
    [SerializeField] private GameObject hexWallPrefab;
    [SerializeField] private GameObject previewDotPrefab;
    [SerializeField] private Material pieceMaterial;

    [Header("=== 棋子精灵（10种，拖上去）===")]
    [SerializeField] private Sprite spriteNormal;
    [SerializeField] private Sprite spriteScore;
    [SerializeField] private Sprite spriteExplosion;
    [SerializeField] private Sprite spriteSplit;
    [SerializeField] private Sprite spriteBounce;
    [SerializeField] private Sprite spriteStomach;
    [SerializeField] private Sprite spriteDevour;
    [SerializeField] private Sprite spriteTurn;
    [SerializeField] private Sprite spriteSwap;
    [SerializeField] private Sprite spriteWhirlwind;

    [Header("=== 当前布局（在下面列表加点）===")]
    [SerializeField] private List<PieceEntry> pieces = new List<PieceEntry>();
    [SerializeField] private List<WallEntry> walls = new List<WallEntry>();

    [Header("Runtime Info（只读）")]
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
    private Board.Snapshot _snapshot;

    private void Awake()
    {
        EnsureRoots();
        _resolver = gameObject.GetComponent<SmackResolver>();
        if (_resolver == null)
            _resolver = gameObject.AddComponent<SmackResolver>();

        _resolver.Init(_board, this, this, this);
        BuildLayout();
    }

    private void Update()
    {
        if (_isResolving) return;

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

        if (Input.GetKeyDown(KeyCode.Q))
        {
            UndoLastSmack();
        }

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            BuildLayout();
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

    private void BuildLayout()
    {
        _isResolving = false;
        ClearPreview();
        ClearChildren(_cellsRoot);
        ClearChildren(_piecesRoot);
        ClearChildren(_effectsRoot);
        _cellObjects.Clear();
        _pieceViews.Clear();
        _board.Clear();
        _board.SetShape(MakeHexagonShape(BoardRadius));

        var wallSet = new HashSet<Hex>();
        foreach (var w in walls)
        {
            var hex = new Hex(w.q, w.r);
            wallSet.Add(hex);
            _board.PlaceWall(hex);
        }

        foreach (var cell in _board.AllInsideCells())
        {
            var isWall = wallSet.Contains(cell);
            var prefab = isWall ? hexWallPrefab : hexCellPrefab;
            var obj = Instantiate(prefab, HexToWorld(cell), Quaternion.identity, _cellsRoot);
            obj.name = isWall ? $"Wall_{cell.q}_{cell.r}" : $"Cell_{cell.q}_{cell.r}";
            _cellObjects[cell] = obj;
        }

        foreach (var pe in pieces)
        {
            var pos = new Hex(pe.q, pe.r);
            if (_board.GetContent(pos) != CellContent.Empty)
            {
                Debug.LogWarning($"[Layout] {pe.type} @ ({pe.q},{pe.r}) 被占用，跳过");
                continue;
            }
            var piece = new Piece { Type = pe.type };
            var view = CreatePieceView(pe.type, pos);
            piece.View = view;
            _board.PlacePiece(piece, pos);
            _pieceViews[piece] = view;
        }

        ApplyBoardRotation();
        RefreshPreview();
        Debug.Log($"[Layout] 已加载 {pieces.Count} 枚棋子, {walls.Count} 面墙. 空格=拍击, Q=撤销, Tab=重载布局");
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

    private Sprite GetPieceSprite(PieceType type) => type switch
    {
        PieceType.Normal     => spriteNormal,
        PieceType.Score      => spriteScore,
        PieceType.Explosion  => spriteExplosion,
        PieceType.Split      => spriteSplit,
        PieceType.Bounce     => spriteBounce,
        PieceType.Stomach    => spriteStomach,
        PieceType.Devour     => spriteDevour,
        PieceType.Turn       => spriteTurn,
        PieceType.Swap       => spriteSwap,
        PieceType.Whirlwind  => spriteWhirlwind,
        _                    => spriteNormal,
    };

    private void ExecuteCurrentSmack()
    {
        // 保存快照（可在拍击过程中按 Q 回到此状态）
        _snapshot = _board.Capture();

        _isResolving = true;
        ClearPreview();
        var gravityDir = Hex.OrientationToGravityDir(boardOrientation);
        Debug.Log($"[ZZNC.TempProgramB] Smack start. Orientation={boardOrientation}, GravityDir=D{gravityDir}");

        _resolver.ExecuteSmack(boardOrientation, SmackRules.Default, result =>
        {
            _isResolving = false;
            RemoveDeadViewEntries();
            RefreshPreview();
            Debug.Log($"[ZZNC.TempProgramB] Smack stable. Score={result.ScoreGained}, Overflow={result.EventOverflow}");
        });
    }

    private void UndoLastSmack()
    {
        if (_snapshot == null) return;

        // 停止正在播放的动画协程
        _resolver.StopAllCoroutines();

        _isResolving = false;
        ClearPreview();

        // 销毁当前所有棋子视图
        ClearChildren(_piecesRoot);
        _pieceViews.Clear();

        // 恢复棋盘数据（保留墙体/形状）
        _board.Restore(_snapshot);

        // 为恢复后的棋子重建视图
        foreach (var p in _board.AllPieces())
        {
            var view = CreatePieceView(p.Type, p.Position);
            p.View = view;
            _pieceViews[p] = view;
        }

        // 把布局快照设为 null，避免连续 Undo
        _snapshot = null;

        RefreshPreview();
        Debug.Log($"[ZZNC.TempProgramB] Undo restored {_board.AllPieces().Count} pieces.");
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
