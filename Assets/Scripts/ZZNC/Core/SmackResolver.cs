using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SmackResolver : MonoBehaviour
{
    private Board _board;
    private IBoardView _boardView;
    private IPieceViewFactory _factory;
    private IHUDView _hud; // 可为 null，B 后补

    // 运行时结算状态
    private SmackRules _rules;
    private int _totalScore;
    private int _totalEventCount;
    private bool _eventOverflow;
    private int _currentGravityDir;
    private int _popSerial;
    private int _splitDepth; // 分裂链深度，越深子棋生成越快

    private readonly Queue<GameEvent> _eventQueue = new Queue<GameEvent>();
    private readonly Queue<GameEvent> _priorityQueue = new Queue<GameEvent>();
    private readonly List<GameEvent> _executedLog = new List<GameEvent>();
    private readonly List<GameEvent> _pendingAnimLog = new List<GameEvent>();
    private float _animSpeedScale = 1f;
    private float _animAmplitudeScale = 1f; // Execute 内部直接添加的动画事件，排在主事件之后 // 播放器消费

    // ── 初始化 ────────────────────────────────────────────────────

    public void Init(Board board, IBoardView boardView, IPieceViewFactory factory, IHUDView hud = null)
    {
        _board = board;
        _boardView = boardView;
        _factory = factory;
        _hud = hud;
    }

    // ── 拍击入口（B 调用）────────────────────────────────────────

    /// <summary>
    /// B 在玩家拍击、状态切到 ResolvingEvents 后调用一次。
    /// 全部结算+动画结束后回调 onRoundStable（恰好一次）。
    /// </summary>
    public void ExecuteSmack(int boardOrientation, SmackRules rules, Action<SmackResult> onRoundStable)
    {
        StartCoroutine(DoSmack(boardOrientation, rules, onRoundStable));
    }

    // ── 只读预览（B 旋转预览调用，可裁剪）───────────────────────

    /// <summary>模拟初始重力移动，无副作用。SimulateSmack 可裁剪。</summary>
    public PreviewResult SimulateSmack(int boardOrientation)
    {
        int gravDir = Hex.OrientationToGravityDir(boardOrientation);
        var result = new PreviewResult
        {
            FinalPositions = new Dictionary<int, Hex>(),
            CollidingPieces = new List<int>(),
            HitTargetIds = new List<int>(),
        };

        // 克隆棋盘状态做模拟
        var simPositions = new Dictionary<int, Hex>(); // pieceId → simPos
        var simGrid = new Dictionary<Hex, int>();       // simPos → pieceId

        foreach (var p in _board.AllPieces())
        {
            simPositions[p.ID] = p.Position;
            simGrid[p.Position] = p.ID;
        }

        // 按轨道顺序处理
        var ordered = BuildGravityOrder(gravDir, simPositions);
        foreach (var pieceId in ordered)
        {
            if (!simPositions.TryGetValue(pieceId, out var from)) continue;
            var to = CalcFarthest(from, gravDir, simGrid, pieceId, _board);
            if (to != from)
            {
                simGrid.Remove(from);
                simGrid[to] = pieceId;
                simPositions[pieceId] = to;
            }
            result.FinalPositions[pieceId] = simPositions[pieceId];

            // 检查是否碰撞
            var front = simPositions[pieceId].Neighbor(gravDir);
            if (simGrid.ContainsKey(front))
            {
                result.CollidingPieces.Add(pieceId);
                result.HitTargetIds.Add(simGrid[front]);
            }
        }
        return result;
    }

    // ── 结算协程 ─────────────────────────────────────────────────

    private IEnumerator DoSmack(int boardOrientation, SmackRules rules, Action<SmackResult> onRoundStable)
    {
        _rules = rules;
        _totalScore = 0;
        _totalEventCount = 0;
        _eventOverflow = false;
        _popSerial = 0;
        _splitDepth = 0;
        _eventQueue.Clear();
        _priorityQueue.Clear();
        _executedLog.Clear();

        // 重置触发计数 + 得分棋独立计数
        foreach (var p in _board.AllPieces())
        {
            p.TriggerCountThisSmack = 0;
            p.ScoreHitCount = 0;
        }

        // A3：生成初始重力事件
        _currentGravityDir = Hex.OrientationToGravityDir(boardOrientation);
        EnqueueGravityEvents(_currentGravityDir);

        // A4：逐事件执行（先算逻辑记录，播放在下方）
        ProcessEventQueue();

        // 队列越长动画越快、越夸张
        int totalEvents = Mathf.Max(1, _executedLog.Count);
        _animSpeedScale = Mathf.Min(1f + Mathf.Log(totalEvents, 2f) * 0.15f, 3f);
        _animAmplitudeScale = 1f + Mathf.Log(totalEvents, 2f) * 0.25f;

        // A9：逐条播放动画
        TempPieceView.GlobalAmplitudeScale = _animAmplitudeScale;
        TempPieceView.GlobalSpeedScale = _animSpeedScale;
        yield return StartCoroutine(PlayEventLog());

        var result = new SmackResult
        {
            ScoreGained = _totalScore,
            EventOverflow = _eventOverflow,
        };
        onRoundStable?.Invoke(result);
    }

    // ── A3 重力轨道与初始事件 ─────────────────────────────────────

    private void EnqueueGravityEvents(int gravDir)
    {
        // 收集所有棋子并按轨道排序
        var ordered = BuildGravityOrder(gravDir, null);
        foreach (var id in ordered)
        {
            var p = FindPieceById(id);
            if (p == null) continue;
            _eventQueue.Enqueue(new GameEvent
            {
                Type = GameEventType.GravityMove,
                TargetPieceId = id,
                Direction = gravDir,
            });
        }
    }

    /// <summary>
    /// 按轨道左→右、轨道内下→上排序，返回棋子 ID 列表。
    /// simPositions 为 null 时使用真实棋盘。
    /// </summary>
    private List<int> BuildGravityOrder(int gravDir, Dictionary<int, Hex> simPositions)
    {
        var pieces = new List<(int id, Hex pos)>();
        if (simPositions != null)
        {
            foreach (var kv in simPositions) pieces.Add((kv.Key, kv.Value));
        }
        else
        {
            foreach (var p in _board.AllPieces()) pieces.Add((p.ID, p.Position));
        }

        // 每个重力方向都有一组平行轨道；这里的表是把屏幕左→右换算到逻辑坐标后的轨道键。
        pieces.Sort((a, b) =>
        {
            int trackA = TrackIndex(a.pos, gravDir);
            int trackB = TrackIndex(b.pos, gravDir);
            if (trackA != trackB) return trackA.CompareTo(trackB); // 轨道左→右
            int depA = DepthIndex(a.pos, gravDir);
            int depB = DepthIndex(b.pos, gravDir);
            if (depA != depB) return depA.CompareTo(depB); // 轨道内下→上（depth 小=靠下）
            if (a.pos.q != b.pos.q) return a.pos.q.CompareTo(b.pos.q);
            if (a.pos.r != b.pos.r) return a.pos.r.CompareTo(b.pos.r);
            return a.id.CompareTo(b.id);
        });

        var ids = new List<int>(pieces.Count);
        foreach (var (id, _) in pieces) ids.Add(id);
        return ids;
    }

    // 轨道横向排序值：垂直于重力方向的分量
    private static int TrackIndex(Hex pos, int gravDir)
    {
        var rotated = RotateToGravityDown(pos, gravDir);
        return rotated.q;
    }

    // 轨道内深度：沿重力方向的分量（值越小=越靠重力终端，即"最下"）
    private static int DepthIndex(Hex pos, int gravDir)
    {
        var rotated = RotateToGravityDown(pos, gravDir);
        return -rotated.r;
    }

    private static Hex RotateToGravityDown(Hex pos, int gravDir)
    {
        switch (Hex.RotateDir(gravDir, 0))
        {
            case 0: return pos;
            case 1: return new Hex(-pos.r, pos.q + pos.r);
            case 2: return new Hex(-pos.q - pos.r, pos.q);
            case 3: return new Hex(-pos.q, -pos.r);
            case 4: return new Hex(pos.r, -pos.q - pos.r);
            case 5: return new Hex(pos.q + pos.r, -pos.q);
            default: return pos;
        }
    }

    // ── A4 事件队列处理（纯逻辑，不播动画）──────────────────────

    private void ProcessEventQueue()
    {
        while (_eventQueue.Count > 0 || _priorityQueue.Count > 0)
        {
            if (_totalEventCount >= _rules.EventLimit)
            {
                _eventQueue.Clear();
                _priorityQueue.Clear();
                _eventOverflow = true;
                Debug.LogError($"[SmackResolver] Event count exceeded {_rules.EventLimit}, queues cleared.");
                break;
            }
            _totalEventCount++;

            // 优先处理 AbilityTrigger 产生的效果事件（插队）
            var ev = _priorityQueue.Count > 0 ? _priorityQueue.Dequeue() : _eventQueue.Dequeue();

            if (IsEventInvalid(ev))
            {
                ev.Skipped = true;
                _executedLog.Add(ev);
                continue;
            }

            Execute(ev);
            // 先加主事件，再加直接产生的动画事件（让动画发生在主事件之后）
            _executedLog.Add(ev);
            foreach (var a in _pendingAnimLog)
                _executedLog.Add(a);
            _pendingAnimLog.Clear();
        }
    }

    private bool IsEventInvalid(GameEvent ev)
    {
        if (ev.Type == GameEventType.Spawn) return false;
        if (ev.Type == GameEventType.Score) return false;
        // Consume 提前保存了 View 的，即使棋子已被抢先移除也能正常播移除动画
        if (ev.Type == GameEventType.Consume && ev.RemovedView != null) return false;
        return FindPieceById(ev.TargetPieceId) == null;
    }

    // ── A5 事件执行逻辑 ───────────────────────────────────────────

    private void Execute(GameEvent ev)
    {
        ev.Executed = true;
        switch (ev.Type)
        {
            case GameEventType.GravityMove: ExecuteMove(ev, false); break;
            case GameEventType.PushMove:    ExecuteMove(ev, true);  break;
            case GameEventType.Collision:   ExecuteCollision(ev);   break;
            case GameEventType.AbilityTrigger: ExecuteAbilityTrigger(ev); break;
            case GameEventType.Explosion:   ExecuteExplosion(ev);   break;
            case GameEventType.Split:       ExecuteSplit(ev);       break;
            case GameEventType.Spawn:       ExecuteSpawn(ev);       break;
            case GameEventType.Remove:      ExecuteRemove(ev);      break;
            case GameEventType.Score:       ExecuteScore(ev);       break;
            case GameEventType.BounceMove:  ExecuteBounceMove(ev);  break;
            case GameEventType.TurnMove:    ExecuteTurnMove(ev);    break;
            case GameEventType.SwapPosition: ExecuteSwapPosition(ev); break;
            case GameEventType.ContinueMove: ExecuteContinueMove(ev); break;
            case GameEventType.StomachMove: ExecuteStomachMove(ev); break;
            case GameEventType.Consume:     ExecuteConsume(ev);     break;
            case GameEventType.AreaConsume: ExecuteAreaConsume(ev); break;
            case GameEventType.RingRotate:  ExecuteRingRotate(ev);  break;
        }
    }

    /// <summary>GravityMove / PushMove 共用移动逻辑。isPush=true 时只移动 1 格且可推出棋盘。</summary>
    private void ExecuteMove(GameEvent ev, bool isPush)
    {
        var piece = FindPieceById(ev.TargetPieceId);
        if (piece == null) { ev.Skipped = true; return; }

        ev.FromPos = piece.Position;
        ev.View = piece.View;
        if (_boardView != null)
            ev.FromWorldPos = _boardView.HexToWorld(piece.Position);

        Hex to;
        if (isPush)
        {
            // 爆炸推动：只移 1 格
            var next = piece.Position.Neighbor(ev.Direction);
            if (_board.GetContent(next) != CellContent.Empty)
            {
                // 推动失败，ToPos=FromPos，动画时播震动
                ev.ToPos = piece.Position;
                return;
            }
            to = next;
        }
        else
        {
            // 重力移动：移到最远合法位置
            to = CalcFarthestOnBoard(piece.Position, ev.Direction);
            if (to == piece.Position)
            {
                ev.Skipped = true;
                return; // 没有位移，不产生碰撞
            }
        }

        ev.ToPos = to;
        if (_boardView != null)
            ev.ToWorldPos = _boardView.HexToWorld(to);
        _board.MovePiece(piece, to);

        // ── 爆炸推动得分 ──────────────────────────────────────────
        if (isPush)
        {
            var evSource = FindPieceById(ev.SourcePieceId);
            // 爆炸棋每成功推动一枚棋子 +10
            if (evSource != null && evSource.Type == PieceType.Explosion)
                RecordScore(10, evSource.Position);

            // 被推动的棋子是得分棋：独立计数 + 指数得分
            if (piece.Type == PieceType.Score)
            {
                piece.ScoreHitCount++;
                int sDelta = (int)Math.Pow(2, piece.ScoreHitCount);
                RecordScore(sDelta, piece.Position, piece.ScoreHitCount);
            }
            // 被推动的棋子是普通棋："被爆炸影响" +2
            else if (piece.Type == PieceType.Normal)
            {
                RecordScore(2, piece.Position);
            }
        }

        // 检查正前方是否有棋子
        var frontCell = to.Neighbor(ev.Direction);
        var frontPiece = _board.GetPiece(frontCell);
        if (frontPiece != null)
        {
            _eventQueue.Enqueue(new GameEvent
            {
                Type = GameEventType.Collision,
                TargetPieceId = frontPiece.ID,
                SourcePieceId = piece.ID,
                Direction = ev.Direction,
            });
        }
    }

    /// <summary>生成 Score 事件（ExecuteScore 执行时累加总分）。</summary>
    private void RecordScore(int delta, Hex originPos, int hitNumber = 1)
    {
        _eventQueue.Enqueue(new GameEvent
        {
            Type = GameEventType.Score,
            ScoreDelta = delta,
            ComboAtTrigger = hitNumber,
            PopSerial = ++_popSerial,
            ScoreOriginPos = originPos,
            ScoreWorldPos = _boardView?.HexToWorld(originPos) ?? Vector3.zero,
        });
    }

    private void ExecuteCollision(GameEvent ev)
    {
        var target = FindPieceById(ev.TargetPieceId);
        if (target == null) { ev.Skipped = true; return; }

        // 保存撞击者的 View，用于动画阶段播放大缩小
        var source = FindPieceById(ev.SourcePieceId);
        if (source != null && source.Type == PieceType.Normal)
        {
            ev.View = source.View;
            RecordScore(2, source.Position);
        }

        // 被撞目标是得分棋：独立计数 → 指数得分 2^n
        if (target.Type == PieceType.Score)
        {
            target.ScoreHitCount++;
            int delta = (int)Math.Pow(2, target.ScoreHitCount);
            RecordScore(delta, target.Position, target.ScoreHitCount);
            return; // 得分棋不经过 AbilityTrigger
        }

        // 普通棋被撞：无能力，不触发
        if (target.Type == PieceType.Normal) return;

        // 其他特殊棋子 → AbilityTrigger
        _eventQueue.Enqueue(new GameEvent
        {
            Type = GameEventType.AbilityTrigger,
            TargetPieceId = ev.TargetPieceId,
            SourcePieceId = ev.SourcePieceId,
            Direction = ev.Direction,
        });
    }

    private void ExecuteAbilityTrigger(GameEvent ev)
    {
        var target = FindPieceById(ev.TargetPieceId);
        if (target == null) { ev.Skipped = true; return; }

        // 保存 View 引用，即使棋子被 Board 移除（如 Split）也能在播放阶段播放缩放动画
        ev.View = target.View;

        if (target.TriggerCountThisSmack >= _rules.PieceTriggerLimit)
        {
            ev.Skipped = true;
            return;
        }
        target.TriggerCountThisSmack++;

        switch (target.Type)
        {
            case PieceType.Explosion:
                _priorityQueue.Enqueue(new GameEvent { Type = GameEventType.Explosion, TargetPieceId = target.ID, Direction = _currentGravityDir });
                break;

            case PieceType.Split:
                _priorityQueue.Enqueue(new GameEvent { Type = GameEventType.Split, TargetPieceId = target.ID, Direction = ev.Direction });
                break;

            // ── 新棋子 ────────────────────────────────────────────

            case PieceType.Bounce:
                RecordScore(1, target.Position);
                _priorityQueue.Enqueue(new GameEvent { Type = GameEventType.BounceMove, TargetPieceId = ev.SourcePieceId, Direction = Hex.Opposite(ev.Direction) });
                break;

            case PieceType.Turn:
                RecordScore(1, target.Position);
                _priorityQueue.Enqueue(new GameEvent { Type = GameEventType.TurnMove, TargetPieceId = ev.SourcePieceId, Direction = Hex.RotateDir(ev.Direction, 1) });
                break;

            case PieceType.Swap:
                RecordScore(1, target.Position);
                _priorityQueue.Enqueue(new GameEvent { Type = GameEventType.SwapPosition, TargetPieceId = target.ID, SourcePieceId = ev.SourcePieceId, Direction = ev.Direction });
                break;

            case PieceType.Stomach:
                // 胃袋沿撞击方向移动吞噬
                _priorityQueue.Enqueue(new GameEvent { Type = GameEventType.StomachMove, TargetPieceId = target.ID, Direction = ev.Direction });
                break;

            case PieceType.Devour:
                // 吞噬棋：清除周围 + 自身
                _priorityQueue.Enqueue(new GameEvent { Type = GameEventType.AreaConsume, TargetPieceId = target.ID, Direction = _currentGravityDir });
                break;

            case PieceType.Whirlwind:
                RecordScore(1, target.Position);
                _priorityQueue.Enqueue(new GameEvent { Type = GameEventType.RingRotate, TargetPieceId = target.ID, SourcePieceId = ev.SourcePieceId, Direction = _currentGravityDir });
                break;

            default: break;
        }
    }

    // ── A6 爆炸棋 ─────────────────────────────────────────────────

    private void ExecuteExplosion(GameEvent ev)
    {
        var center = FindPieceById(ev.TargetPieceId);
        if (center == null) { ev.Skipped = true; return; }

        // 以当前重力方向为第 1 方向，顺时针遍历六个方向
        int gravDir = ev.Direction;
        for (int i = 0; i < 6; i++)
        {
            int dir = Hex.RotateDir(gravDir, i);
            var neighbor = center.Position.Neighbor(dir);
            var neighborPiece = _board.GetPiece(neighbor);
            if (neighborPiece == null) continue;
            if (_board.GetContent(neighbor) == CellContent.Wall) continue;

            // 向远离中心方向推动 1 格
            _eventQueue.Enqueue(new GameEvent
            {
                Type = GameEventType.PushMove,
                TargetPieceId = neighborPiece.ID,
                SourcePieceId = center.ID,
                Direction = dir, // 推动方向 = 远离中心
            });
        }
    }

    // ── A6 分裂棋 ─────────────────────────────────────────────────

    private void ExecuteSplit(GameEvent ev)
    {
        var origin = FindPieceById(ev.TargetPieceId);
        if (origin == null) { ev.Skipped = true; return; }

        int collisionDir = ev.Direction;
        var originPos = origin.Position;
        _splitDepth++; // 越分裂越快，不封顶
        float boost = 1f + _splitDepth * 0.15f;

        // 从 Board 移除原棋子，但不销毁 View（留给动画阶段播放移除动画）
        var originView = origin.View;
        _board.RemovePiece(origin);
        _pendingAnimLog.Add(new GameEvent
        {
            Type = GameEventType.Consume,
            RemovedView = originView,
            Executed = true,
        });

        // 第n次分裂加n分
        RecordScore(_splitDepth, originPos);

        // 两个生成方向：顺时针 60° 和逆时针 60°
        int dirCW  = Hex.RotateDir(collisionDir, 1);
        int dirCCW = Hex.RotateDirCCW(collisionDir, 1);

        _eventQueue.Enqueue(new GameEvent
        {
            Type = GameEventType.Spawn,
            SpawnType = PieceType.Split,
            SpawnPos = originPos,
            Direction = dirCW,
            SourcePieceId = origin.ID,
            SpawnSpeedBoost = boost,
        });
        _eventQueue.Enqueue(new GameEvent
        {
            Type = GameEventType.Spawn,
            SpawnType = PieceType.Split,
            SpawnPos = originPos,
            Direction = dirCCW,
            SourcePieceId = origin.ID,
            SpawnSpeedBoost = boost,
        });
    }

    // ── 新棋子执行逻辑 ─────────────────────────────────────────

    /// <summary>反弹：把撞击者沿来路反推 1 格。</summary>
    private void ExecuteBounceMove(GameEvent ev)
    {
        var piece = FindPieceById(ev.TargetPieceId);
        if (piece == null) { ev.Skipped = true; return; }
        ev.FromPos = piece.Position;
        ev.View = piece.View;

        var next = piece.Position.Neighbor(ev.Direction);
        if (_board.GetContent(next) != CellContent.Empty)
        {
            ev.ToPos = piece.Position; // 推失败→震动
            return;
        }
        ev.ToPos = next;
        _board.MovePiece(piece, next);
        // 碰撞检查
        var front1 = next.Neighbor(ev.Direction);
        var fp1 = _board.GetPiece(front1);
        if (fp1 != null)
            _eventQueue.Enqueue(new GameEvent { Type = GameEventType.Collision, TargetPieceId = fp1.ID, SourcePieceId = piece.ID, Direction = ev.Direction });
    }

    /// <summary>转向：撞击者顺时针转 60° 移动 1 格。</summary>
    private void ExecuteTurnMove(GameEvent ev)
    {
        var piece = FindPieceById(ev.TargetPieceId);
        if (piece == null) { ev.Skipped = true; return; }
        ev.FromPos = piece.Position;
        ev.View = piece.View;

        var next = piece.Position.Neighbor(ev.Direction);
        if (_board.GetContent(next) != CellContent.Empty)
        {
            ev.ToPos = piece.Position;
            return;
        }
        ev.ToPos = next;
        _board.MovePiece(piece, next);
        // 碰撞检查（沿转向后方向）
        var front2 = next.Neighbor(ev.Direction);
        var fp2 = _board.GetPiece(front2);
        if (fp2 != null)
            _eventQueue.Enqueue(new GameEvent { Type = GameEventType.Collision, TargetPieceId = fp2.ID, SourcePieceId = piece.ID, Direction = ev.Direction });
    }

    /// <summary>交换位置：交换棋与撞击者互换位置。</summary>
    private void ExecuteSwapPosition(GameEvent ev)
    {
        var swapPiece = FindPieceById(ev.TargetPieceId);
        var attacker = FindPieceById(ev.SourcePieceId);
        if (swapPiece == null || attacker == null) { ev.Skipped = true; return; }

        var swapPos = swapPiece.Position;
        var attackerPos = attacker.Position;

        // 原子交换，不会丢失任意一方
        _board.SwapPieces(swapPiece, attacker);

        ev.FromPos = attackerPos;
        ev.ToPos = swapPos;
        ev.View = attacker.View;

        // 交换棋也加入动画日志
        _pendingAnimLog.Add(new GameEvent
        {
            Type = GameEventType.PushMove,
            TargetPieceId = swapPiece.ID,
            FromPos = swapPos,
            ToPos = attackerPos,
            View = swapPiece.View,
            Executed = true,
        });
    }

    /// <summary>交换后继续移动：撞击者沿原方向移动到底。</summary>
    private void ExecuteContinueMove(GameEvent ev)
    {
        var piece = FindPieceById(ev.TargetPieceId);
        if (piece == null) { ev.Skipped = true; return; }
        ev.FromPos = piece.Position;
        ev.View = piece.View;

        var to = CalcFarthestOnBoard(piece.Position, ev.Direction);
        if (to == piece.Position) { ev.Skipped = true; return; }

        ev.ToPos = to;
        _board.MovePiece(piece, to);
        var front3 = to.Neighbor(ev.Direction);
        var fp3 = _board.GetPiece(front3);
        if (fp3 != null)
            _eventQueue.Enqueue(new GameEvent { Type = GameEventType.Collision, TargetPieceId = fp3.ID, SourcePieceId = piece.ID, Direction = ev.Direction });
    }

    /// <summary>胃袋：每次前进1格，吃掉该格棋子后自动链式推入下一步。吃越多走越快。</summary>
    private void ExecuteStomachMove(GameEvent ev)
    {
        var stomach = FindPieceById(ev.TargetPieceId);
        if (stomach == null) { ev.Skipped = true; return; }
        ev.FromPos = stomach.Position;
        ev.View = stomach.View;

        int dir = ev.Direction;
        var next = stomach.Position.Neighbor(dir);
        var content = _board.GetContent(next);

        if (content == CellContent.Wall || content == CellContent.OutOfBoard)
        {
            ev.ToPos = stomach.Position; // 到尽头了，不动
            return;
        }

        if (content == CellContent.Piece)
        {
            var target = _board.GetPiece(next);
            if (target != null)
            {
                var eatenView = target.View;
                _board.RemovePiece(target);
                _pendingAnimLog.Add(new GameEvent
                {
                    Type = GameEventType.Consume,
                    RemovedView = eatenView,
                    FromPos = target.Position,
                    ToPos = target.Position,
                    Executed = true,
                });
                // 第x口得分 = Fib(x)（斐波那契数列）
                int eatNum = ev.ConsumeCount + 1;
                RecordScore(Fib(eatNum), next);
                ev.ConsumeCount = eatNum;
            }
        }

        // 胃袋移入该格
        _board.MovePiece(stomach, next);
        ev.ToPos = next;

        // 前方还有棋子：链式推入下一步，吃越多加速度越快
        var nextNext = next.Neighbor(dir);
        var nextContent = _board.GetContent(nextNext);
        if (nextContent == CellContent.Piece || nextContent == CellContent.Empty)
        {
            _eventQueue.Enqueue(new GameEvent
            {
                Type = GameEventType.StomachMove,
                TargetPieceId = stomach.ID,
                Direction = dir,
                ConsumeCount = ev.ConsumeCount,
            });
        }
    }

    /// <summary>单个吞噬删除。</summary>
    private void ExecuteConsume(GameEvent ev)
    {
        var piece = FindPieceById(ev.TargetPieceId);
        if (piece == null)
        {
            // 棋子已被其他事件抢先移除，但 View 可能已由入队方保存
            if (ev.RemovedView == null) ev.Skipped = true;
            return;
        }
        if (ev.RemovedView == null)
            ev.RemovedView = piece.View;
        _board.RemovePiece(piece);
    }

    /// <summary>吞噬棋：清除周围全部棋子（含撞击者）+ 自身。</summary>
    private void ExecuteAreaConsume(GameEvent ev)
    {
        var devour = FindPieceById(ev.TargetPieceId);
        if (devour == null) { ev.Skipped = true; return; }

        int gravDir = ev.Direction;
        int eaten = 0;

        // 遍历 6 个方向
        for (int i = 0; i < 6; i++)
        {
            int dir = Hex.RotateDir(gravDir, i);
            var neighbor = devour.Position.Neighbor(dir);
            var content = _board.GetContent(neighbor);
            if (content == CellContent.Piece)
            {
                var target = _board.GetPiece(neighbor);
                if (target != null)
                {
                    _eventQueue.Enqueue(new GameEvent
                    {
                        Type = GameEventType.Consume,
                        TargetPieceId = target.ID,
                        SourcePieceId = devour.ID,
                        RemovedView = target.View, // 提前保存 View，防止被其他事件抢先移除后找不到
                    });
                    eaten++;
                }
            }
        }

        // 自身删除
        _eventQueue.Enqueue(new GameEvent { Type = GameEventType.Remove, TargetPieceId = devour.ID });

        if (eaten > 0) RecordScore(eaten * 5, devour.Position);
    }

    /// <summary>旋风棋：顺时针旋转相邻棋子一格，跳过墙。</summary>
    private void ExecuteRingRotate(GameEvent ev)
    {
        var center = FindPieceById(ev.TargetPieceId);
        if (center == null) { ev.Skipped = true; return; }

        int gravDir = ev.Direction;

        // 完整环（6个方向全部保留，含墙）
        var ring = new List<Hex>(6);
        for (int i = 0; i < 6; i++)
        {
            int dir = Hex.RotateDirCCW(gravDir, i);
            ring.Add(center.Position.Neighbor(dir));
        }

        var attacker = FindPieceById(ev.SourcePieceId);
        if (attacker == null) return;

        // 找撞击者在环中的索引
        int startIdx = -1;
        for (int i = 0; i < 6; i++)
            if (ring[i] == attacker.Position) { startIdx = i; break; }
        if (startIdx < 0) return;

        // 顺时针找出下一个非墙、非出界的有效位置
        int NextValid(int from)
        {
            for (int offset = 1; offset <= 6; offset++)
            {
                int idx = (from + offset) % 6;
                var c = _board.GetContent(ring[idx]);
                if (c != CellContent.OutOfBoard && c != CellContent.Wall)
                    return idx;
            }
            return -1;
        }

        // 无有效位置可旋转
        int firstValid = NextValid(startIdx);
        if (firstValid < 0) return;

        // 仅一个有效位置：撞击者直接移过去
        bool onlyOneValid = NextValid(firstValid) < 0 || NextValid(firstValid) == firstValid;
        if (onlyOneValid)
        {
            if (_board.GetContent(ring[firstValid]) == CellContent.Empty)
            {
                var fromPos = attacker.Position;
                _board.MovePiece(attacker, ring[firstValid]);
                _pendingAnimLog.Add(new GameEvent { Type = GameEventType.PushMove, TargetPieceId = attacker.ID, SourcePieceId = center.ID, FromPos = fromPos, ToPos = ring[firstValid], View = attacker.View, Executed = true });
            }
            return;
        }

        // 从撞击者之后开始，收集连续有棋子的链（跳过墙）
        var chainIdxs = new List<int>();
        int ci = firstValid;
        var visited = new HashSet<int>();
        while (ci >= 0 && !visited.Contains(ci))
        {
            visited.Add(ci);
            if (_board.GetContent(ring[ci]) != CellContent.Piece) break;
            chainIdxs.Add(ci);
            int next = NextValid(ci);
            if (next < 0 || next == startIdx) break;
            ci = next;
        }

        // Phase 1: 棋盘更新（尾→头） + 存储动画数据
        var animData = new List<(Hex from, Hex to, Piece piece, int toIdx)>();
        for (int i = chainIdxs.Count - 1; i >= 0; i--)
        {
            int fromIdx = chainIdxs[i];
            int toIdx = NextValid(fromIdx);
            if (toIdx < 0) continue;

            var fromPos = ring[fromIdx];
            var toPos = ring[toIdx];
            var piece = _board.GetPiece(fromPos);
            if (piece == null) continue;

            _board.MovePiece(piece, toPos);
            animData.Add((fromPos, toPos, piece, toIdx));
        }

        // Phase 2: 动画记录（头→尾）
        for (int j = 0; j < animData.Count; j++)
        {
            var (fromPos, toPos, piece, toIdx) = animData[j];
            _pendingAnimLog.Add(new GameEvent
            {
                Type = GameEventType.PushMove,
                TargetPieceId = piece.ID,
                SourcePieceId = center.ID,
                FromPos = fromPos,
                ToPos = toPos,
                View = piece.View,
                Executed = true,
            });

            // 碰撞检查：被推棋子的下一个有效位置若有棋子
            int frontIdx = NextValid(toIdx);
            if (frontIdx >= 0)
            {
                var frontPiece = _board.GetPiece(ring[frontIdx]);
                if (frontPiece != null)
                {
                    int actualDir = DirBetween(toPos, ring[frontIdx]);
                    _eventQueue.Enqueue(new GameEvent { Type = GameEventType.Collision, TargetPieceId = frontPiece.ID, SourcePieceId = piece.ID, Direction = actualDir });
                }
            }

            // 被旋风卷过的棋子到达新位置后，也按重力方向触发碰撞
            var gravFront = toPos.Neighbor(gravDir);
            var gravFrontPiece = _board.GetPiece(gravFront);
            if (gravFrontPiece != null)
            {
                _eventQueue.Enqueue(new GameEvent { Type = GameEventType.Collision, TargetPieceId = gravFrontPiece.ID, SourcePieceId = piece.ID, Direction = gravDir });
            }
        }

        // 最后：撞击者本身移入腾空的下一个有效位置
        if (firstValid >= 0)
        {
            var attackerToPos = ring[firstValid];
            if (_board.GetContent(attackerToPos) == CellContent.Empty)
            {
                var attackerFromPos = attacker.Position;
                _board.MovePiece(attacker, attackerToPos);
                _pendingAnimLog.Add(new GameEvent
                {
                    Type = GameEventType.PushMove,
                    TargetPieceId = attacker.ID,
                    SourcePieceId = center.ID,
                    FromPos = attackerFromPos,
                    ToPos = attackerToPos,
                    View = attacker.View,
                    Executed = true,
                });

                // 补位后碰撞检查（环方向）
                int attackerFrontIdx = NextValid(firstValid);
                if (attackerFrontIdx >= 0)
                {
                    var attackerFrontPiece = _board.GetPiece(ring[attackerFrontIdx]);
                    if (attackerFrontPiece != null)
                    {
                        int actualDir = DirBetween(attackerToPos, ring[attackerFrontIdx]);
                        _eventQueue.Enqueue(new GameEvent { Type = GameEventType.Collision, TargetPieceId = attackerFrontPiece.ID, SourcePieceId = attacker.ID, Direction = actualDir });
                    }
                }

                // 补位后碰撞检查（重力方向）
                var attackerGravFront = attackerToPos.Neighbor(gravDir);
                var attackerGravFrontPiece = _board.GetPiece(attackerGravFront);
                if (attackerGravFrontPiece != null)
                {
                    _eventQueue.Enqueue(new GameEvent { Type = GameEventType.Collision, TargetPieceId = attackerGravFrontPiece.ID, SourcePieceId = attacker.ID, Direction = gravDir });
                }
            }
        }
    }

    private void ExecuteSpawn(GameEvent ev)
    {
        int spawnDir = ev.Direction;
        Hex originPos = ev.SpawnPos;
        Hex targetPos = originPos.Neighbor(spawnDir);

        // 确定最终生成位置
        Hex finalPos;
        if (!TryFindSpawnPos(targetPos, spawnDir, originPos, ev.SourcePieceId, out finalPos))
        {
            ev.Skipped = true; // 全场无空格，本侧生成失败，不影响另一侧
            return;
        }

        ev.ToPos = finalPos;

        var newPiece = new Piece { Type = ev.SpawnType };

        // View 创建在分裂原点（SpawnPos），动画阶段先播 Spawn 再 MoveTo 到目标
        if (_factory != null)
        {
            newPiece.View = _factory.CreateView(ev.SpawnType, ev.SpawnPos);
            // 立即设为不可见，否则 ProcessEventQueue 阶段到动画播放之间会裸奔几百毫秒
            if (newPiece.View is Component comp)
                comp.transform.localScale = Vector3.zero;
        }

        _board.PlacePiece(newPiece, finalPos);
        ev.SpawnedPiece = newPiece; // 记录引用，播放器用于播 PlaySpawn
    }

    /// <summary>
    /// 尝试找到分裂生成位置。
    /// 优先推链；推链失败找最近空格；全场无空格返回 false。
    /// </summary>
    private bool TryFindSpawnPos(Hex targetPos, int spawnDir, Hex originPos, int sourcePieceId, out Hex finalPos)
    {
        var content = _board.GetContent(targetPos);

        if (content == CellContent.Empty || content == CellContent.OutOfBoard)
        {
            // 目标格为空或出界（出界时回退到 originPos 附近最近空格）
            if (content == CellContent.Empty)
            {
                finalPos = targetPos;
                return true;
            }
            // 目标格出界，走最近空格逻辑
            return FindNearestEmpty(originPos, spawnDir, out finalPos);
        }

        if (content == CellContent.Wall)
            return FindNearestEmpty(originPos, spawnDir, out finalPos);

        // 目标格有棋子：尝试推链
        if (TryPushChain(targetPos, spawnDir, sourcePieceId))
        {
            // 推链成功后 targetPos 已腾空
            finalPos = targetPos;
            return true;
        }

        // 推链失败：最近空格
        return FindNearestEmpty(originPos, spawnDir, out finalPos);
    }

    /// <summary>
    /// 尝试沿 spawnDir 推动整条棋子链，为 targetPos 腾出空间。
    /// 分裂挤压不能将棋子推出棋盘。
    /// </summary>
    private bool TryPushChain(Hex targetPos, int spawnDir, int sourcePieceId)
    {
        // 收集链
        var chain = new List<Hex>();
        var cur = targetPos;
        while (true)
        {
            var cc = _board.GetContent(cur);
            if (cc != CellContent.Piece) break;
            chain.Add(cur);
            cur = cur.Neighbor(spawnDir);
        }

        // 检查链末端
        var chainEnd = _board.GetContent(cur);
        if (chainEnd != CellContent.Empty)
            return false; // 末端是墙/边缘/无空格

        // Phase 1: 棋盘状态更新（尾→头，确保每个棋子移入已腾空的位置）
        var fromList = new List<(Hex from, Hex to, Piece piece)>();
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            var piece = _board.GetPiece(chain[i]);
            if (piece == null) continue;
            var from = piece.Position;
            var pushTo = chain[i].Neighbor(spawnDir);
            _board.MovePiece(piece, pushTo);
            fromList.Insert(0, (from, pushTo, piece)); // 头→尾顺序存

            // 被分裂挤压的得分棋：独立计数 + 指数得分
            if (piece.Type == PieceType.Score)
            {
                piece.ScoreHitCount++;
                int sDelta = (int)Math.Pow(2, piece.ScoreHitCount);
                _totalScore += sDelta;
                _pendingAnimLog.Add(new GameEvent
                {
                    Type = GameEventType.Score,
                    ScoreDelta = sDelta,
                    ComboAtTrigger = piece.ScoreHitCount,
                    ScoreOriginPos = pushTo,
                    ScoreWorldPos = _boardView?.HexToWorld(pushTo) ?? Vector3.zero,
                    PopSerial = ++_popSerial,
                    Executed = true,
                });
            }
        }

        // Phase 2: 动画记录（头→尾，a挤b→b挤c的视觉效果）
        for (int j = 0; j < fromList.Count; j++)
        {
            var (from, pushTo, piece) = fromList[j];
            _pendingAnimLog.Add(new GameEvent
            {
                Type = GameEventType.PushMove,
                TargetPieceId = piece.ID,
                SourcePieceId = sourcePieceId,
                Direction = spawnDir,
                FromPos = from,
                ToPos = pushTo,
                View = piece.View,
                Executed = true,
            });

            // 碰撞判定：移动结束后，正前方若有棋子则产生碰撞
            var frontPos = pushTo.Neighbor(spawnDir);
            var frontPiece = _board.GetPiece(frontPos);
            if (frontPiece != null)
            {
                _eventQueue.Enqueue(new GameEvent
                {
                    Type = GameEventType.Collision,
                    TargetPieceId = frontPiece.ID,
                    SourcePieceId = piece.ID,
                    Direction = spawnDir,
                });
            }
        }
        return true;
    }

    /// <summary>最近空格规则（策划案 8.4）。</summary>
    private bool FindNearestEmpty(Hex originPos, int spawnDir, out Hex finalPos)
    {
        finalPos = default;
        var empties = _board.EmptyCells();
        if (empties.Count == 0) return false;

        int bestDist = int.MaxValue;
        var candidates = new List<Hex>();

        foreach (var cell in empties)
        {
            int d = originPos.Distance(cell);
            if (d < bestDist) { bestDist = d; candidates.Clear(); }
            if (d == bestDist) candidates.Add(cell);
        }

        if (candidates.Count == 1) { finalPos = candidates[0]; return true; }

        // 距离相同：从原生成方向起顺时针排序
        candidates.Sort((a, b) =>
        {
            double angA = AngleFromDir(originPos, a, spawnDir);
            double angB = AngleFromDir(originPos, b, spawnDir);
            int angleCompare = CompareAngles(angA, angB);
            if (angleCompare != 0) return angleCompare;
            // 再按坐标顺序
            if (a.q != b.q) return a.q.CompareTo(b.q);
            return a.r.CompareTo(b.r);
        });

        finalPos = candidates[0];
        return true;
    }

    /// <summary>返回从 origin 到 target 的方向相对 baseDir 的顺时针偏移角度。</summary>
    private static double AngleFromDir(Hex origin, Hex target, int baseDir)
    {
        var delta = target - origin;
        double x = Math.Sqrt(3d) * 0.5d * delta.q;
        double y = 0.5d * delta.q + delta.r;
        double angle = Math.Atan2(x, y);
        if (angle < 0d) angle += Math.PI * 2d;

        double baseAngle = Hex.RotateDir(baseDir, 0) * Math.PI / 3d;
        double relative = angle - baseAngle;
        while (relative < 0d) relative += Math.PI * 2d;
        while (relative >= Math.PI * 2d) relative -= Math.PI * 2d;
        return relative;
    }

    private static int CompareAngles(double a, double b)
    {
        const double epsilon = 0.000001d;
        double diff = a - b;
        if (Math.Abs(diff) <= epsilon) return 0;
        return diff < 0d ? -1 : 1;
    }

    private void ExecuteRemove(GameEvent ev)
    {
        var piece = FindPieceById(ev.TargetPieceId);
        if (piece == null) { ev.Skipped = true; return; }
        ev.FromPos = piece.Position;
        ev.RemovedView = piece.View; // 记录 View，Board 移除后播放器仍可用
        _board.RemovePiece(piece);
    }

    private void ExecuteScore(GameEvent ev)
    {
        _totalScore += ev.ScoreDelta;
        // ScoreOriginPos 已在 RecordScore 或链推时设定，无需修改
    }

    // ── 工具 ─────────────────────────────────────────────────────

    private Piece FindPieceById(int id) => _board.GetPieceById(id);

    /// <summary>计算沿 dir 方向在棋盘内可达的最远格（不含推出棋盘）。</summary>
    private Hex CalcFarthestOnBoard(Hex from, int dir)
    {
        var cur = from;
        while (true)
        {
            var next = cur.Neighbor(dir);
            var c = _board.GetContent(next);
            if (c == CellContent.OutOfBoard || c == CellContent.Wall || c == CellContent.Piece)
                break;
            cur = next;
        }
        return cur;
    }

    /// <summary>计算平顶六边形中 from 到 to 的邻居方向（假设相邻）。</summary>
    /// <summary>斐波那契数列，n ≥ 1。</summary>
    private static int Fib(int n)
    {
        int a = 0, b = 1;
        for (int i = 1; i < n; i++) { int t = b; b = a + b; a = t; }
        return b;
    }

    private static int DirBetween(Hex from, Hex to)
    {
        int dq = to.q - from.q;
        int dr = to.r - from.r;
        if (dq == 1 && dr == 0) return 0;
        if (dq == 0 && dr == 1) return 1;
        if (dq == -1 && dr == 1) return 2;
        if (dq == -1 && dr == 0) return 3;
        if (dq == 0 && dr == -1) return 4;
        if (dq == 1 && dr == -1) return 5;
        return 0;
    }

    /// <summary>用于 SimulateSmack 的克隆棋盘最远格计算。</summary>
    private static Hex CalcFarthest(Hex from, int dir, Dictionary<Hex, int> grid, int selfId, Board board)
    {
        var cur = from;
        while (true)
        {
            var next = cur.Neighbor(dir);
            var content = board.GetContent(next);
            if (content == CellContent.OutOfBoard || content == CellContent.Wall)
                break;
            if (!grid.ContainsKey(next)) { cur = next; continue; }
            if (grid[next] == selfId) { cur = next; continue; } // 自身
            break;
        }
        return cur;
    }

    // ── A9 播放器（逐事件串行播动画）────────────────────────────

    private IEnumerator PlayEventLog()
    {
        foreach (var ev in _executedLog)
        {
            if (ev.Skipped || !ev.Executed)
            {
                // 跳过的事件如果有残留 View 仍需销毁（如 Consume 被抢先但提前保存了 View）
                if (ev.RemovedView is UnityEngine.Object viewObj && viewObj != null)
                {
                    _factory?.DestroyView(ev.RemovedView);
                }
                continue;
            }
            yield return StartCoroutine(PlayEvent(ev));
        }
    }

    private IEnumerator PlayEvent(GameEvent ev)
    {
        float duration = 0f;
        switch (ev.Type)
        {
            case GameEventType.GravityMove:
            case GameEventType.PushMove:
            case GameEventType.BounceMove:
            case GameEventType.TurnMove:
            case GameEventType.SwapPosition:
            case GameEventType.ContinueMove:
            case GameEventType.StomachMove:
            {
                if (ev.RemovedView is UnityEngine.Object viewObj1 && viewObj1 != null)
                {
                    duration = ev.RemovedView.PlayRemove() / _animSpeedScale;
                    if (duration > 0f) yield return new WaitForSeconds(duration);
                    _factory?.DestroyView(ev.RemovedView);
                    yield break;
                }
                if (ev.View != null && _boardView != null)
                {
                    bool isFail = (ev.Type == GameEventType.PushMove || ev.Type == GameEventType.BounceMove || ev.Type == GameEventType.TurnMove)
                                  && ev.FromPos == ev.ToPos;
                    if (isFail)
                    {
                        duration = ev.View.PlayHitShake() / _animSpeedScale;
                    }
                    else
                    {
                        var fromWorld = _boardView.HexToWorld(ev.FromPos);
                        var toWorld = _boardView.HexToWorld(ev.ToPos);
                        ev.View.SnapTo(fromWorld);
                        float stepBoost = 1f + ev.ConsumeCount * 0.2f; // 胃袋逐格加速
                        duration = ev.View.MoveTo(toWorld) / (_animSpeedScale * stepBoost);
                    }
                }
                break;
            }
            case GameEventType.Collision:
            {
                // 普通棋撞击时播放放大缩小动画
                if (ev.View != null)
                    duration = ev.View.PlayAbilityFX() / _animSpeedScale;
                break;
            }
            case GameEventType.AbilityTrigger:
            {
                if (ev.View != null)
                    duration = ev.View.PlayAbilityFX() / _animSpeedScale;
                break;
            }
            case GameEventType.Spawn:
            {
                if (ev.SpawnedPiece?.View != null && _boardView != null)
                {
                    // 从分裂原点 Snap，播生成动画，再移动到最终位置
                    var originWorld = _boardView.HexToWorld(ev.SpawnPos);
                    var targetWorld = _boardView.HexToWorld(ev.ToPos);
                    ev.SpawnedPiece.View.SnapTo(originWorld);
                    // 出现动画（0→1）必须完整播放，否则棋子缩在小状态变不回来
                    float spawnDur = ev.SpawnedPiece.View.PlaySpawn() / _animSpeedScale;
                    if (spawnDur > 0f) yield return new WaitForSeconds(spawnDur);
                    if (originWorld != targetWorld)
                    {
                        // 移动动画可加速（下一帧 SnapTo 会修复位置）
                        float moveDur = ev.SpawnedPiece.View.MoveTo(targetWorld) / (_animSpeedScale * ev.SpawnSpeedBoost);
                        if (moveDur > 0f) yield return new WaitForSeconds(moveDur);
                    }
                }
                yield break; // 已自行处理等待，跳过末尾统一 WaitForSeconds
            }
            case GameEventType.Remove:
            case GameEventType.Consume:
            {
                if (ev.RemovedView is UnityEngine.Object viewObj2 && viewObj2 != null)
                {
                    duration = ev.RemovedView.PlayRemove() / _animSpeedScale;
                    if (duration > 0f) yield return new WaitForSeconds(duration);
                    _factory?.DestroyView(ev.RemovedView);
                }
                yield break;
            }
            case GameEventType.Score:
            {
                if (_hud != null && _boardView != null)
                {
                    var pos = _boardView.HexToWorld(ev.ScoreOriginPos);
                    _hud.ShowScorePop(ev.ScoreDelta, ev.PopSerial, pos);
                }
                break;
            }
        }

        if (duration > 0f)
            yield return new WaitForSeconds(duration);
    }


}
