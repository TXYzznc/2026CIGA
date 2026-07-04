using System.Collections.Generic;
using UnityEngine;

public sealed class GameFlowController : MonoBehaviour
{
    [SerializeField] private LevelConfig[] levels;
    [SerializeField] private BoardView boardView;
    [SerializeField] private PieceViewFactory pieceViewFactory;
    [SerializeField] private MonoBehaviour smackExecutorBehaviour;
    [SerializeField] private HudController hud;
    [SerializeField] private int randomSeed = 20260704;
    [SerializeField] private bool autoStart = true;
    [SerializeField] private bool enableKeyboardInput = true;
    [SerializeField] private bool showDebugControls = true;
    [SerializeField] private bool autoPlaceChoiceForPrototype = true;

    private readonly Board board = new Board();
    private readonly List<Piece> spawnedPieces = new List<Piece>();
    private System.Random random;
    private ISmackExecutor smackExecutor;
    private int levelIndex;
    private int totalScore;
    private int remainingSmacks;
    private int boardOrientation;
    private GameState state = GameState.LevelInit;

    public int BoardOrientation => boardOrientation;

    private void Awake()
    {
        random = new System.Random(randomSeed);
        smackExecutor = smackExecutorBehaviour as ISmackExecutor;
        if (smackExecutor == null)
        {
            smackExecutor = GetComponent<ISmackExecutor>();
        }
    }

    private void OnEnable()
    {
        if (hud == null)
        {
            return;
        }

        if (hud.RotateLeftButton != null) hud.RotateLeftButton.onClick.AddListener(RotateCounterClockwise);
        if (hud.RotateRightButton != null) hud.RotateRightButton.onClick.AddListener(RotateClockwise);
        if (hud.SmackButton != null) hud.SmackButton.onClick.AddListener(Smack);
        if (hud.SkipChoiceButton != null) hud.SkipChoiceButton.onClick.AddListener(SkipChoice);
    }

    private void OnDisable()
    {
        if (hud == null)
        {
            return;
        }

        if (hud.RotateLeftButton != null) hud.RotateLeftButton.onClick.RemoveListener(RotateCounterClockwise);
        if (hud.RotateRightButton != null) hud.RotateRightButton.onClick.RemoveListener(RotateClockwise);
        if (hud.SmackButton != null) hud.SmackButton.onClick.RemoveListener(Smack);
        if (hud.SkipChoiceButton != null) hud.SkipChoiceButton.onClick.RemoveListener(SkipChoice);
    }

    private void Start()
    {
        if (autoStart)
        {
            StartLevel(0);
        }
    }

    private void Update()
    {
        if (!enableKeyboardInput)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) RotateCounterClockwise();
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) RotateClockwise();
        if (Input.GetKeyDown(KeyCode.Space)) Smack();
        if (Input.GetKeyDown(KeyCode.R)) StartLevel(levelIndex);
    }

    private void OnGUI()
    {
        if (!showDebugControls)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(16f, 16f, 240f, 190f), GUI.skin.box);
        GUILayout.Label($"State: {state}");
        GUILayout.Label($"Score: {totalScore} / >{CurrentTargetScore()}");
        GUILayout.Label($"Smacks: {remainingSmacks}");
        GUILayout.Label($"Orientation: {boardOrientation}");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("A / Left")) RotateCounterClockwise();
        if (GUILayout.Button("D / Right")) RotateClockwise();
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Space / Smack")) Smack();
        if (GUILayout.Button("R / Restart")) StartLevel(levelIndex);
        GUILayout.EndArea();
    }

    public void StartLevel(int index)
    {
        if (levels == null || levels.Length == 0)
        {
            hud?.ShowMessage("缺少 LevelConfig。");
            return;
        }

        levelIndex = Mathf.Clamp(index, 0, levels.Length - 1);
        var level = levels[levelIndex];
        state = GameState.LevelInit;
        ClearViews();
        board.Clear();
        board.SetShape(CreateHexagonCells(level.boardRadius));
        level.TryGetBoardLayout(out var fixedLayout);
        PlaceWalls(level, fixedLayout);
        boardView?.RebuildCells(board);
        totalScore = 0;
        remainingSmacks = level.smackCount;
        boardOrientation = 0;
        boardView?.SetOrientation(boardOrientation);

        if (!SpawnFixedPieces(fixedLayout))
        {
            SpawnInitialPieces(level);
        }

        EnterState(GameState.RotationPreview);
    }

    public void RotateClockwise()
    {
        if (state != GameState.RotationPreview)
        {
            return;
        }

        boardOrientation = WrapOrientation(boardOrientation + 1);
        boardView?.SetOrientation(boardOrientation);
        RefreshHud();
    }

    public void RotateCounterClockwise()
    {
        if (state != GameState.RotationPreview)
        {
            return;
        }

        boardOrientation = WrapOrientation(boardOrientation - 1);
        boardView?.SetOrientation(boardOrientation);
        RefreshHud();
    }

    public void Smack()
    {
        if (state != GameState.RotationPreview)
        {
            return;
        }

        EnterState(GameState.ResolvingEvents);
        var executor = smackExecutor ?? GetComponent<ISmackExecutor>();
        if (executor == null)
        {
            hud?.ShowMessage("缺少拍击结算器，临时按 0 分回调。");
            OnRoundStable(new SmackResult(0, 0, false));
            return;
        }

        executor.ExecuteSmack(boardOrientation, levels[levelIndex].ToSmackRules(), OnRoundStable);
    }

    public void SkipChoice()
    {
        if (state != GameState.PieceChoice)
        {
            return;
        }

        hud?.ShowMessage("跳过本次三选一。");
        EnterState(GameState.RotationPreview);
    }

    public void ChoosePiece(PieceType type)
    {
        if (state != GameState.PieceChoice)
        {
            return;
        }

        TryPlaceRandomPiece(type);
        EnterState(GameState.RotationPreview);
    }

    private void OnRoundStable(SmackResult result)
    {
        if (state != GameState.ResolvingEvents)
        {
            return;
        }

        totalScore += result.ScoreGained;
        remainingSmacks = Mathf.Max(0, remainingSmacks - 1);

        if (result.EventOverflow)
        {
            hud?.ShowMessage("能量过载");
        }

        EnterState(GameState.RoundStable);
        EnterState(remainingSmacks > 0 ? GameState.PieceChoice : GameState.LevelJudge);

        if (state == GameState.LevelJudge)
        {
            JudgeLevel();
        }
    }

    private void JudgeLevel()
    {
        EnterState(totalScore - CurrentTargetScore() > 0 ? GameState.LevelSuccess : GameState.LevelFail);
    }

    private void EnterState(GameState nextState)
    {
        state = nextState;
        RefreshHud();

        switch (state)
        {
            case GameState.PieceChoice:
                if (autoPlaceChoiceForPrototype)
                {
                    AutoOfferChoiceForPrototype();
                }

                break;
            case GameState.LevelSuccess:
                hud?.ShowMessage("过关：分数高于目标。");
                break;
            case GameState.LevelFail:
                hud?.ShowMessage("失败：分数必须高于目标，等于也失败。");
                break;
        }
    }

    private void AutoOfferChoiceForPrototype()
    {
        var chosen = WeightedPicker.Pick(levels[levelIndex].choicePool, random);
        hud?.ShowMessage($"原型阶段自动放置三选一：{chosen}");
        ChoosePiece(chosen);
    }

    private void PlaceWalls(LevelConfig level, BoardLayoutJson fixedLayout)
    {
        var walls = fixedLayout?.walls ?? level.walls;
        if (walls == null)
        {
            return;
        }

        foreach (var wall in walls)
        {
            board.PlaceWall(wall.ToHex());
        }
    }

    private bool SpawnFixedPieces(BoardLayoutJson fixedLayout)
    {
        if (fixedLayout?.pieces == null || fixedLayout.pieces.Length == 0)
        {
            return false;
        }

        foreach (var piece in fixedLayout.pieces)
        {
            if (!TryPlacePiece(piece.type, piece.ToHex()))
            {
                hud?.ShowMessage($"固定棋盘放置失败：{piece.type} {piece.q},{piece.r}");
            }
        }

        return true;
    }

    private void SpawnInitialPieces(LevelConfig level)
    {
        for (var i = 0; i < level.initialPieceCount; i++)
        {
            var type = WeightedPicker.Pick(level.initialPiecePool, random);
            if (!TryPlaceRandomPiece(type))
            {
                break;
            }
        }
    }

    private bool TryPlaceRandomPiece(PieceType type)
    {
        var emptyCells = board.EmptyCells();
        if (emptyCells.Count == 0)
        {
            hud?.ShowMessage("棋盘已满，无法放置");
            return false;
        }

        return TryPlacePiece(type, emptyCells[random.Next(0, emptyCells.Count)]);
    }

    private bool TryPlacePiece(PieceType type, Hex target)
    {
        if (board.GetContent(target) != CellContent.Empty)
        {
            return false;
        }

        var view = pieceViewFactory != null ? pieceViewFactory.CreateView(type, target) : null;
        var piece = new Piece { Type = type, View = view };
        board.PlacePiece(piece, target);
        spawnedPieces.Add(piece);
        return true;
    }

    private void ClearViews()
    {
        foreach (var piece in spawnedPieces)
        {
            if (piece?.View != null && pieceViewFactory != null)
            {
                pieceViewFactory.DestroyView(piece.View);
            }
        }

        spawnedPieces.Clear();
    }

    private void RefreshHud()
    {
        hud?.Refresh(totalScore, CurrentTargetScore(), remainingSmacks, state);
    }

    private int CurrentTargetScore()
    {
        return levels != null && levels.Length > 0 ? levels[levelIndex].targetScore : 0;
    }

    private static IEnumerable<Hex> CreateHexagonCells(int radius)
    {
        var safeRadius = Mathf.Max(0, radius);
        for (var q = -safeRadius; q <= safeRadius; q++)
        {
            var r1 = Mathf.Max(-safeRadius, -q - safeRadius);
            var r2 = Mathf.Min(safeRadius, -q + safeRadius);
            for (var r = r1; r <= r2; r++)
            {
                yield return new Hex(q, r);
            }
        }
    }

    private static int WrapOrientation(int orientation) => ((orientation % 6) + 6) % 6;
}

