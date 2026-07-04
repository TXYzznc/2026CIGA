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
public class TempPlaytestController : MonoBehaviour, IBoardView, IPieceViewFactory, IHUDView
{
    private const float PieceZ = -0.05f;

    /// <summary>根据棋盘半径等比缩放格子大小，使总宽度≈10.15单位保持不变。</summary>
    private float CellSize => 1.45f * 7f / (2 * boardRadius + 1);

    /// <summary>视觉缩放系数（以 radius=3 为基准）。</summary>
    private float LayoutScale => 7f / (2 * boardRadius + 1);

    [Header("=== 棋盘参数 ===")]
    [SerializeField, Range(1, 10)] private int boardRadius = 3;

    [Header("=== 动画速度 ===")]
    [SerializeField, Range(0.1f, 0.5f)] private float moveDuration = 0.18f;
    [SerializeField, Range(0.1f, 0.5f)] private float fxDuration = 0.14f;

    [Header("=== 旋转弹簧 ===")]
    [SerializeField, Range(50, 500)] private float springStiffness = 250f;
    [SerializeField, Range(1f, 30f)] private float springDamping = 15f;

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

    [Header("=== 特效 ===")]
    [SerializeField] private SmackImpactVFX impactVFX;
    [SerializeField] private HUDView hudView;
    [SerializeField] private BoardEdgeGlowEffect boardEdgeGlow;
    [SerializeField] private RotationPreviewRenderer previewRenderer;
    [SerializeField] private PieceTooltip pieceTooltip;
    [SerializeField, Range(0f, 2f)] private float tooltipHoverDelay = 0.5f;
    [SerializeField] private BoardHexPulseEffect comboPulse;

    [Header("Runtime Info（只读）")]
    [SerializeField] private int boardOrientation;

    private readonly Board _board = new Board();
    private readonly Dictionary<Hex, GameObject> _cellObjects = new Dictionary<Hex, GameObject>();
    private readonly Dictionary<Piece, TempPieceView> _pieceViews = new Dictionary<Piece, TempPieceView>();
    private readonly List<GameObject> _previewObjects = new List<GameObject>();
    private Transform _cellsRoot;
    private Transform _piecesRoot;
    private Transform _effectsRoot;
    private SmackResolver _resolver;
    private bool _isResolving;
    private int _comboCount;
    private Hex? _hoveredHex;
    private SpriteRenderer _hoveredCellRenderer;
    private Color _hoveredCellBaseColor;
    private float _hoveredHexSince;
    private float _visualAngle;        // 当前视觉角度（弹簧驱动）
    private float _springVelocity;     // 弹簧速度
    private int _targetOrientation;    // 逻辑目标朝向
    private Board.Snapshot _snapshot;

    private static readonly Color HoverCellTint = new Color(1f, 0.92f, 0.55f, 1f);

    private void Awake()
    {
        EnsureRoots();
        EnsureHoverTooltip();
        _resolver = gameObject.GetComponent<SmackResolver>();
        if (_resolver == null)
            _resolver = gameObject.AddComponent<SmackResolver>();

        _resolver.Init(_board, this, this, this);
        EnsureComboPulse();

        if (hudView != null)
            hudView.OnSmackClicked += OnSmackRequest;

        _targetOrientation = boardOrientation;
        var initGravDir = Hex.OrientationToGravityDir(_targetOrientation);
        var initLocalGrav = HexToLocal(new Hex(0, 0).Neighbor(initGravDir));
        _visualAngle = Vector2.SignedAngle(initLocalGrav, Vector2.down);

        BuildLayout();
    }

    private void OnDestroy()
    {
        if (hudView != null)
            hudView.OnSmackClicked -= OnSmackRequest;

        ClearHoveredCell();
    }

    private void Update()
    {
        if (_isResolving)
        {
            // 结算期间持续把流光速度归零，让 SmoothDamp 正常淡出
            boardEdgeGlow?.SetSpeed(0f);
            pieceTooltip?.Hide();
            ClearHoveredCell();
            return;
        }

        // Accept any number of rotations,累加到 _targetOrientation
        if (Input.GetKeyDown(KeyCode.A))
            RotateTarget(1);
        if (Input.GetKeyDown(KeyCode.D))
            RotateTarget(-1);

        // 弹簧模拟：反方向蓄力 → 加速 → 减速 → 过冲 → 回弹归位
        var targetGravDir = Hex.OrientationToGravityDir(_targetOrientation);
        var targetLocalGrav = HexToLocal(new Hex(0, 0).Neighbor(targetGravDir));
        var targetAngle = Vector2.SignedAngle(targetLocalGrav, Vector2.down);

        float dt = Time.deltaTime;
        float displacement = Mathf.DeltaAngle(_visualAngle, targetAngle);
        _springVelocity += displacement * springStiffness * dt;
        _springVelocity *= Mathf.Exp(-springDamping * dt);
        _visualAngle += _springVelocity * dt;

        transform.rotation = Quaternion.Euler(0f, 0f, _visualAngle);

        // 用弹簧速度驱动边缘流光亮度
        boardEdgeGlow?.SetSpeed(Mathf.Abs(_springVelocity));

        // 弹簧稳定后归位并刷新预览
        if (Mathf.Abs(displacement) < 0.5f && Mathf.Abs(_springVelocity) < 1f)
        {
            _visualAngle = targetAngle;
            _springVelocity = 0f;
            RefreshPreview();
        }

        if (Input.GetKeyDown(KeyCode.Space))
            OnSmackRequest();

        if (Input.GetKeyDown(KeyCode.Q))
            UndoLastSmack();

        UpdateHoverTooltip();
        if (Input.GetKeyDown(KeyCode.Tab))
            BuildLayout();
    }

    private void RotateTarget(int direction)
    {
        _targetOrientation = Hex.RotateDir(_targetOrientation, direction);
        boardOrientation = _targetOrientation; // 立即更新重力方向，不等动画结束
        // 反方向蓄力一脚，产生"先回拉再弹出"的物理感
        _springVelocity -= direction * 10f;
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
        _comboCount++;
        comboPulse?.Pulse(_comboCount);
        Debug.Log($"[ZZNC.TempProgramB] Score +{scoreDelta}, Combo {_comboCount}, At {worldPos}");
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
        pieceTooltip?.Hide();
        ClearHoveredCell();
        ClearPreview();
        ClearChildren(_cellsRoot);
        ClearChildren(_piecesRoot);
        ClearChildren(_effectsRoot);
        _cellObjects.Clear();
        _pieceViews.Clear();
        _board.Clear();
        _board.SetShape(MakeHexagonShape(boardRadius));

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
            obj.transform.localScale = Vector3.one * LayoutScale;
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

        // 初始化旋转特效
        boardEdgeGlow?.Setup(boardRadius, CellSize);
        EnsureComboPulse();
        comboPulse?.Setup(boardRadius, CellSize);
        previewRenderer?.Setup(previewDotPrefab, 0.78f * LayoutScale, _effectsRoot);

        RefreshPreview();
        Debug.Log($"[Layout] 已加载 {pieces.Count} 枚棋子, {walls.Count} 面墙. 空格=拍击, Q=撤销, Tab=重载布局");
    }

    private TempPieceView CreatePieceView(PieceType type, Hex pos)
    {
        var go = new GameObject($"Piece_{type}_{pos.q}_{pos.r}");
        go.transform.SetParent(_piecesRoot);
        go.transform.position = HexToWorld(pos) + new Vector3(0f, 0f, PieceZ);
        go.transform.localScale = Vector3.one * 0.78f * LayoutScale;

        var view = go.AddComponent<TempPieceView>();
        view.MoveDuration = moveDuration;
        view.FxDuration = fxDuration;
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

    // 空格键和按钮点击共用的入口，含防重入检查
    private void OnSmackRequest()
    {
        if (_isResolving) return;
        impactVFX?.PlaySmackImpact(HexToWorld(new Hex(0, 0)));
        ExecuteCurrentSmack();
    }

    private void ExecuteCurrentSmack()
    {
        // 保存快照（可在拍击过程中按 Q 回到此状态）
        _snapshot = _board.Capture();

        // 拍击前把棋盘视觉一次拉到位（不让动画冻结在半路）
        var snapGravDir = Hex.OrientationToGravityDir(_targetOrientation);
        var snapLocalGrav = HexToLocal(new Hex(0, 0).Neighbor(snapGravDir));
        _visualAngle = Vector2.SignedAngle(snapLocalGrav, Vector2.down);
        _springVelocity = 0f;
        transform.rotation = Quaternion.Euler(0f, 0f, _visualAngle);
        boardOrientation = _targetOrientation;

        _isResolving = true;
        _comboCount  = 0;
        ClearPreview();
        var gravityDir = Hex.OrientationToGravityDir(boardOrientation);
        Debug.Log($"[ZZNC.TempProgramB] Smack start. Orientation={boardOrientation}, GravityDir=D{gravityDir}");

        _resolver.ExecuteSmack(boardOrientation, SmackRules.Default, result =>
        {
            _isResolving = false;
            SyncSpawnedPieceViews();
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

        if (_resolver == null) return;

        var gravityDir = Hex.OrientationToGravityDir(boardOrientation);
        var preview = _resolver.SimulateSmack(boardOrientation);

        previewRenderer?.Refresh(preview, _board, gravityDir, HexToWorld, GetPieceSprite);
    }

    private void ClearPreview()
    {
        previewRenderer?.Clear();

        foreach (var obj in _previewObjects)
        {
            if (obj != null) Destroy(obj);
        }
        _previewObjects.Clear();
    }

    private void EnsureComboPulse()
    {
        if (comboPulse != null) return;

        if (boardEdgeGlow != null)
            comboPulse = boardEdgeGlow.GetComponent<BoardHexPulseEffect>();

        if (comboPulse == null && boardEdgeGlow != null)
            comboPulse = boardEdgeGlow.gameObject.AddComponent<BoardHexPulseEffect>();

        if (comboPulse == null)
            comboPulse = GetComponentInChildren<BoardHexPulseEffect>();
    }

    /// <summary>把 Split 生成的子棋子（ExecuteSpawn 创建、但未注册进 _pieceViews 的）补注册进来。</summary>
    private void SyncSpawnedPieceViews()
    {
        foreach (var p in _board.AllPieces())
        {
            if (!_pieceViews.ContainsKey(p) && p.View is TempPieceView v)
                _pieceViews[p] = v;
        }
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
            if (_pieceViews.TryGetValue(piece, out var deadView) && deadView != null)
                Destroy(deadView.gameObject);
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

    private Vector2 HexToLocal(Hex hex)
    {
        var x = Mathf.Sqrt(3f) * (hex.q + hex.r * 0.5f) * CellSize;
        var y = -1.5f * hex.r * CellSize;
        return new Vector2(x, y);
    }

    // ── 鼠标悬浮棋子名称提示 ────────────────────────────────────

    private static readonly Dictionary<PieceType, string> PieceDisplayNames = new Dictionary<PieceType, string>
    {
        { PieceType.Normal,    "普通棋" },
        { PieceType.Score,     "得分棋" },
        { PieceType.Explosion, "爆炸棋" },
        { PieceType.Split,     "分裂棋" },
        { PieceType.Bounce,    "弹跳棋" },
        { PieceType.Stomach,   "胃袋棋" },
        { PieceType.Devour,    "吞噬棋" },
        { PieceType.Turn,      "转向棋" },
        { PieceType.Swap,      "交换棋" },
        { PieceType.Whirlwind, "旋风棋" },
    };

    private void EnsureHoverTooltip()
    {
        if (pieceTooltip != null && IsSceneInstance(pieceTooltip))
        {
            pieceTooltip.transform.SetAsLastSibling();
            pieceTooltip.Hide();
            return;
        }

        var tooltipPrefab = pieceTooltip;
        var existingTooltips = FindObjectsByType<PieceTooltip>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var candidate in existingTooltips)
        {
            if (candidate != null && IsSceneInstance(candidate))
            {
                pieceTooltip = candidate;
                pieceTooltip.transform.SetAsLastSibling();
                pieceTooltip.Hide();
                return;
            }
        }

        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[ZZNC.TempProgramB] No Canvas found, hover tooltip disabled.");
            return;
        }

        if (tooltipPrefab == null)
        {
            pieceTooltip = PieceTooltip.CreateRuntime(canvas);
            return;
        }

        pieceTooltip = Instantiate(tooltipPrefab, canvas.transform);
        pieceTooltip.name = tooltipPrefab.name.Trim() + "_Instance";
        pieceTooltip.transform.SetAsLastSibling();
        if (pieceTooltip.transform is RectTransform rect)
        {
            rect.localScale = Vector3.one;
            rect.anchoredPosition = Vector2.zero;
        }
        pieceTooltip.Hide();
    }

    private static bool IsSceneInstance(Component component)
    {
        return component != null && component.gameObject.scene.IsValid() && component.gameObject.scene.isLoaded;
    }

    private static string GetPieceTitle(PieceType type) => type switch
    {
        PieceType.Normal => "普通棋",
        PieceType.Score => "得分棋",
        PieceType.Explosion => "爆炸棋",
        PieceType.Split => "分裂棋",
        PieceType.Bounce => "弹跳棋",
        PieceType.Stomach => "胃袋棋",
        PieceType.Devour => "吞噬棋",
        PieceType.Turn => "转向棋",
        PieceType.Swap => "交换棋",
        PieceType.Whirlwind => "旋风棋",
        _ => "棋子",
    };

    private static string GetPieceDescription(PieceType type) => type switch
    {
        PieceType.Normal => "基础棋子，沿重力方向移动并参与碰撞。",
        PieceType.Score => "碰撞触发得分。",
        PieceType.Explosion => "碰撞后向周围推开棋子。",
        PieceType.Split => "碰撞后尝试生成新的棋子。",
        PieceType.Bounce => "碰撞后反向弹跳。",
        PieceType.Stomach => "吞入前方棋子并保留在棋盘内。",
        PieceType.Devour => "吞噬目标棋子。",
        PieceType.Turn => "碰撞后改变移动方向。",
        PieceType.Swap => "与目标棋子交换位置。",
        PieceType.Whirlwind => "影响周围一圈棋子的站位。",
        _ => null,
    };

    private void UpdateHoverTooltip()
    {
        if (pieceTooltip == null)
        {
            EnsureHoverTooltip();
            if (pieceTooltip == null) return;
        }

        var cam = Camera.main;
        if (cam == null)
        {
            pieceTooltip.Hide();
            ClearHoveredCell();
            return;
        }

        // 屏幕坐标 → 世界坐标 → 棋盘本地坐标
        var worldPos = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -cam.transform.position.z));
        var local = transform.InverseTransformPoint(worldPos);

        // 世界坐标逆推 Hex（HexToLocal 的代数逆）
        // x = sqrt(3) * (q + r/2) * CellSize
        // y = -1.5 * r * CellSize
        float rf = local.y / (-1.5f * CellSize);
        float qf = local.x / (Mathf.Sqrt(3f) * CellSize) - rf * 0.5f;
        var hex = HexRound(qf, rf);

        if (!_board.IsInside(hex))
        {
            ClearHoveredCell();
            pieceTooltip.Hide();
            return;
        }

        SetHoveredCell(hex);

        var content = _board.GetContent(hex);
        if (content == CellContent.Empty)
        {
            pieceTooltip.Hide();
            return;
        }

        if (Time.unscaledTime - _hoveredHexSince < tooltipHoverDelay)
        {
            pieceTooltip.Hide();
            return;
        }

        if (content == CellContent.Piece)
        {
            var piece = _board.GetPiece(hex);
            if (piece != null)
            {
                pieceTooltip.Show(GetPieceTitle(piece.Type), GetPieceDescription(piece.Type));
                return;
            }
        }

        if (content == CellContent.Wall)
        {
            pieceTooltip.Show("墙体", "阻挡棋子移动和生成。");
            return;
        }

    }

    private void SetHoveredCell(Hex hex)
    {
        if (_hoveredHex.HasValue && _hoveredHex.Value.Equals(hex) && _hoveredCellRenderer != null)
            return;

        ClearHoveredCell();

        if (!_cellObjects.TryGetValue(hex, out var cellObject) || cellObject == null)
            return;

        _hoveredCellRenderer = cellObject.GetComponent<SpriteRenderer>();
        if (_hoveredCellRenderer == null)
            return;

        _hoveredHex = hex;
        _hoveredHexSince = Time.unscaledTime;
        _hoveredCellBaseColor = _hoveredCellRenderer.color;
        _hoveredCellRenderer.color = Color.Lerp(_hoveredCellBaseColor, HoverCellTint, 0.45f);
    }

    private void ClearHoveredCell()
    {
        if (_hoveredCellRenderer != null)
            _hoveredCellRenderer.color = _hoveredCellBaseColor;

        _hoveredHex = null;
        _hoveredCellRenderer = null;
        _hoveredHexSince = 0f;
    }

    // 六边形坐标取整（cube coordinates round）
    private static Hex HexRound(float q, float r)
    {
        float s = -q - r;
        int rq = Mathf.RoundToInt(q), rr = Mathf.RoundToInt(r), rs = Mathf.RoundToInt(s);
        float dq = Mathf.Abs(rq - q), dr = Mathf.Abs(rr - r), ds = Mathf.Abs(rs - s);
        if (dq > dr && dq > ds) rq = -rr - rs;
        else if (dr > ds)       rr = -rq - rs;
        return new Hex(rq, rr);
    }

    private static void ClearChildren(Transform root)
    {
        for (var i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }
}
