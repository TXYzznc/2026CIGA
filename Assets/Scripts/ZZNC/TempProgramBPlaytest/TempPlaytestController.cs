using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[System.Serializable]
public struct PieceEntry
{
    public PieceType type;
    public int q;
    public int r;
}

/// <summary>
/// 在 Inspector 里拖 Sprite + 配棋子列表，运行时按 Tab 键热重载布局。
/// </summary>
public class TempPlaytestController : MonoBehaviour, IBoardView, IPieceViewFactory, IHUDView
{
    private const float PieceZ = -0.05f;
    private const float VisualBoardAngleOffset = 30f;

    /// <summary>含边缘半格墙圈的总半径（最外圈被大六边形轮廓裁成半格）。</summary>
    private int OuterRadius => boardRadius + 1;

    /// <summary>根据棋盘半径等比缩放格子大小，使总宽度≈10.15单位保持不变。</summary>
    private float CellSize => 1.45f * 7f / (2 * OuterRadius + 1);

    /// <summary>视觉缩放系数（以 radius=3 为基准）。</summary>
    private float LayoutScale => 7f / (2 * OuterRadius + 1);

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

    [Header("=== 当前布局（只添加棋子；墙体由边缘裁切自动生成）===")]
    [SerializeField] private List<PieceEntry> pieces = new List<PieceEntry>();

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
    private SpriteMask _boardMask;

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
        _visualAngle = GetVisualBoardAngle(initLocalGrav);

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
        var targetAngle = GetVisualBoardAngle(targetLocalGrav);

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

        var go = new GameObject($"ScorePopup_{scoreDelta}");
        go.transform.position = worldPos + new Vector3(0f, 0f, -0.5f); // 抬高到棋盘上方
        go.transform.localScale = Vector3.one * 0.8f;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = $"+{scoreDelta}";
        tmp.fontSize = 8f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.yellow;
        tmp.fontStyle = FontStyles.Bold;
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 100;

        StartCoroutine(AnimateScorePopup(go, tmp));
    }

    private IEnumerator AnimateScorePopup(GameObject go, TMP_Text tmp)
    {
        var startPos = go.transform.position;
        for (var t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            var k = t / 0.6f;
            go.transform.position = startPos + new Vector3(0f, 0.5f * k, 0f);
            tmp.color = new Color(1f, 1f, 0f, 1f - k * 0.3f);
            yield return null;
        }
        Destroy(go);
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
        _board.SetShape(MakeMaskedShape());

        var wallSet = new HashSet<Hex>();

        // 规则：只有被大六边形遮罩裁切的“残缺格”才是墙体。
        // 所有完整的小六边形（哪怕轴向距离超过旧 boardRadius）都是正常可玩格。
        foreach (var cell in _board.AllInsideCells())
        {
            if (IsClippedEdgeCell(cell) && wallSet.Add(cell))
                _board.PlaceWall(cell);
        }

        EnsureBoardMask();

        foreach (var cell in _board.AllInsideCells())
        {
            var isWall = wallSet.Contains(cell);
            var prefab = isWall ? hexWallPrefab : hexCellPrefab;
            var obj = Instantiate(prefab, HexToWorld(cell), Quaternion.identity, _cellsRoot);
            FitCellVisual(obj);
            obj.name = isWall ? $"Wall_{cell.q}_{cell.r}" : $"Cell_{cell.q}_{cell.r}";
            foreach (var sr in obj.GetComponentsInChildren<SpriteRenderer>(true))
                sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
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
        boardEdgeGlow?.Setup(OuterRadius, CellSize);
        EnsureComboPulse();
        comboPulse?.Setup(OuterRadius, CellSize);
        previewRenderer?.Setup(previewDotPrefab, 0.78f * LayoutScale, _effectsRoot);

        RefreshPreview();
        LogBoardShape();
        Debug.Log($"[Layout] 已加载 {pieces.Count} 枚棋子, 自动生成 {wallSet.Count} 个残缺边缘墙体. 空格=拍击, Q=撤销, Tab=重载布局");
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
        _visualAngle = GetVisualBoardAngle(snapLocalGrav);
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
            if (result.ScoreGained > 0)
                hudView?.AddScore(result.ScoreGained);
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
        var z = GetVisualBoardAngle(localGravity);
        transform.rotation = Quaternion.Euler(0f, 0f, z);
        RefreshPreview();
        Debug.Log($"[ZZNC.TempProgramB] Board rotated. Orientation={boardOrientation}, rule gravity=D{gravityDir}, visual gravity=screen down.");
    }

    private static float GetVisualBoardAngle(Vector2 localGravity)
    {
        return Vector2.SignedAngle(localGravity, Vector2.down);
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

    /// <summary>
    /// 让格子贴图与点阵精确无缝贴合：
    /// 六边形长轴 = 2×外接圆半径 = 2×CellSize，据此缩放；
    /// 长轴竖直的贴图是尖顶（转0°），长轴水平的是平顶（转30°变尖顶）。
    /// </summary>
    private void FitCellVisual(GameObject obj)
    {
        var rotation = VisualBoardAngleOffset;
        var scale = LayoutScale;
        var sr = obj.GetComponentInChildren<SpriteRenderer>(true);
        if (sr != null && sr.sprite != null)
        {
            var size = sr.sprite.bounds.size;
            var pointyTop = size.y >= size.x;
            rotation = pointyTop ? 0f : 30f;
            scale = 2f * CellSize / Mathf.Max(size.x, size.y);
        }
        obj.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        obj.transform.localScale = Vector3.one * scale;
    }

    /// <summary>
    /// 大六边形裁剪遮罩：边穿过最外圈格子中心附近，把它们裁成半格。
    /// 外接圆半径 = 2 * OuterRadius * CellSize（本地尖顶朝向）。
    /// </summary>
    private void EnsureBoardMask()
    {
        if (_boardMask == null)
        {
            var go = new GameObject("BoardMask");
            go.transform.SetParent(transform, false);
            _boardMask = go.AddComponent<SpriteMask>();
            _boardMask.sprite = GetHexMaskSprite();
        }
        // 本地坐标转 30° 变尖顶；棋盘根节点自带 ∓30°+k·60° 的重力对齐旋转，屏幕上恰好呈平顶
        var circumradius = 2f * OuterRadius * CellSize;
        _boardMask.transform.localPosition = Vector3.zero;
        _boardMask.transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
        // 遮罩精灵外接圆半径 = 1 世界单位，直接用外接圆半径做缩放
        _boardMask.transform.localScale = Vector3.one * circumradius;
    }

    private static Sprite _hexMaskSprite;

    private static Sprite GetHexMaskSprite()
    {
        if (_hexMaskSprite != null) return _hexMaskSprite;

        const int size = 256;
        const float circumR = size * 0.5f;
        var apothem = circumR * Mathf.Sqrt(3f) * 0.5f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        var half = size * 0.5f;
        for (var py = 0; py < size; py++)
        {
            for (var px = 0; px < size; px++)
            {
                var x = px + 0.5f - half;
                var y = py + 0.5f - half;
                // 平顶六边形内含测试：三条对边轴上的投影都不超过边心距
                var inside = Mathf.Abs(y) <= apothem
                          && Mathf.Abs(Mathf.Sqrt(3f) * x + y) * 0.5f <= apothem
                          && Mathf.Abs(Mathf.Sqrt(3f) * x - y) * 0.5f <= apothem;
                pixels[py * size + px] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        _hexMaskSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), circumR);
        _hexMaskSprite.name = "BoardHexMask";
        return _hexMaskSprite;
    }

    /// <summary>
    /// 生成所有与大六边形遮罩相交/在其内部的格子。
    /// 遮罩在本地坐标是尖顶六边形（左右为竖直边），边心距 = √3 * OuterRadius * CellSize。
    /// 与轮廓相交的格子会被遮罩裁成部分格子（半个/三分之一个），拼出大六边形的直边和尖角。
    /// </summary>
    private IEnumerable<Hex> MakeMaskedShape()
    {
        var apothem = Mathf.Sqrt(3f) * OuterRadius * CellSize;
        var margin = CellSize; // 格子外接圆半径：中心离轮廓不超过它就可能相交
        var bound = OuterRadius * 2;
        const float cos60 = 0.5f;
        var sin60 = Mathf.Sqrt(3f) * 0.5f;
        for (var q = -bound; q <= bound; q++)
        {
            for (var r = -bound; r <= bound; r++)
            {
                var p = HexToLocal(new Hex(q, r));
                // 尖顶六边形 SDF：三组对边法线方向上的投影
                var d = Mathf.Max(Mathf.Abs(p.x),
                        Mathf.Max(Mathf.Abs(cos60 * p.x + sin60 * p.y),
                                  Mathf.Abs(cos60 * p.x - sin60 * p.y)));
                if (d <= apothem + margin)
                    yield return new Hex(q, r);
            }
        }
    }

    /// <summary>
    /// 是否为会被外层大六边形遮罩切掉一部分的边缘格。
    /// 只要小六边形的任一顶点落在遮罩外，就归类为墙体；六个顶点都在内的格子一律为正常格。
    /// </summary>
    private bool IsClippedEdgeCell(Hex cell)
    {
        const float epsilon = 0.0001f;
        const float cos60 = 0.5f;
        var sin60 = Mathf.Sqrt(3f) * 0.5f;
        var apothem = Mathf.Sqrt(3f) * OuterRadius * CellSize;
        var center = HexToLocal(cell);

        // 棋盘格在本地坐标中是尖顶六边形，外接圆半径为 CellSize。
        for (var i = 0; i < 6; i++)
        {
            var angle = (90f + i * 60f) * Mathf.Deg2Rad;
            var vertex = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * CellSize;
            var d = Mathf.Max(
                Mathf.Abs(vertex.x),
                Mathf.Max(
                    Mathf.Abs(cos60 * vertex.x + sin60 * vertex.y),
                    Mathf.Abs(cos60 * vertex.x - sin60 * vertex.y)));

            if (d > apothem + epsilon)
                return true;
        }

        return false;
    }

    private Vector2 HexToLocal(Hex hex)
    {
        var x = Mathf.Sqrt(3f) * (hex.q + hex.r * 0.5f) * CellSize;
        var y = -1.5f * hex.r * CellSize;
        return new Vector2(x, y);
    }

    // ── 鼠标悬浮棋子名称提示 ────────────────────────────────────

    private void LogBoardShape()
    {
        var cells = _board.AllInsideCells();
        if (cells.Count == 0) return;

        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var cell in cells)
        {
            var world = HexToWorld(cell);
            var p = new Vector2(world.x, world.y);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }

        Debug.Log($"[Layout.Shape] cells={cells.Count}, outerRadius={OuterRadius}, cellSize={CellSize:F3}, visualOffset={VisualBoardAngleOffset:F1}, rotationZ={transform.eulerAngles.z:F1}, worldBounds=({min.x:F2},{min.y:F2})..({max.x:F2},{max.y:F2})");
    }

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

    private static readonly Dictionary<PieceType, (string Name, string Description)> PieceTooltipTexts =
        new Dictionary<PieceType, (string Name, string Description)>
        {
            {
                PieceType.Normal,
                (
                    "普通棋",
                    "主动撞到其他棋子时获得2分；被爆炸成功推动或推出棋盘时也获得2分，若推动后再次撞击，可再获得2分。"
                )
            },
            {
                PieceType.Score,
                (
                    "得分棋",
                    "每枚得分棋独立计数：本次拍击内第n次受到碰撞、推动、挤压、反弹、转向、交换或旋风位移时，获得2^n分；不同效果可分别计数，下次拍击重置。"
                )
            },
            {
                PieceType.Explosion,
                (
                    "爆炸棋",
                    "被撞后自身保留，将六个相邻棋子分别向外推动1格；每成功推动或推出1枚获得2分，阻挡时不移动；被推动棋子可继续产生碰撞。"
                )
            },
            {
                PieceType.Split,
                (
                    "分裂棋",
                    "被撞后自身消失，并在撞击方向左右各60°各生成1枚分裂棋；目标被占用时沿生成方向推开整列棋子，挤动可产生碰撞且不能推出棋盘；无法推开则生成在最近空格，无空格时失败；不直接得分。"
                )
            },
            {
                PieceType.Bounce,
                (
                    "反弹棋",
                    "被撞后自身保留，将撞击者沿来路反弹1格；目标格被棋子、墙体或边界阻挡时不移动，反弹成功后可继续产生碰撞；不直接得分。"
                )
            },
            {
                PieceType.Stomach,
                (
                    "胃袋棋",
                    "被撞后沿撞击者原方向持续前进，吞掉路径上的所有棋子，直到墙体或棋盘边缘；每吞1枚获得2分，被吞棋子不触发能力，吞噬过程不产生碰撞，胃袋棋最终保留。"
                )
            },
            {
                PieceType.Devour,
                (
                    "吞噬棋",
                    "被撞后吞掉六个相邻格中的所有棋子，包括撞击者；每吞1枚获得3分，被吞棋子不触发能力，结算后吞噬棋自身消失。"
                )
            },
            {
                PieceType.Turn,
                (
                    "转向棋",
                    "被撞后自身保留，使撞击者顺时针转向60°并移动1格；目标格被棋子、墙体或边界阻挡时不转向并停住，成功移动后可继续产生碰撞；不直接得分。"
                )
            },
            {
                PieceType.Swap,
                (
                    "交换棋",
                    "被撞后与撞击者交换位置；交换完成后，撞击者沿原移动方向继续移动到最远位置，并正常产生后续碰撞；交换棋不直接得分。"
                )
            },
            {
                PieceType.Whirlwind,
                (
                    "旋风棋",
                    "被撞后自身不动，使周围非墙格中的棋子与空位顺时针轮换1格，跳过墙体和棋盘外位置；移动棋子按旧位置到新位置的方向继续检查碰撞；旋风棋不直接得分。"
                )
            },
        };

    private static string GetConfiguredPieceTitle(PieceType type)
    {
        return PieceTooltipTexts.TryGetValue(type, out var text) ? text.Name : "棋子";
    }

    private static string GetConfiguredPieceDescription(PieceType type)
    {
        return PieceTooltipTexts.TryGetValue(type, out var text) ? text.Description : null;
    }

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
                pieceTooltip.Show(GetConfiguredPieceTitle(piece.Type), GetConfiguredPieceDescription(piece.Type));
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
