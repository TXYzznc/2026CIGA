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
    private int _currentCombo;
    private int _maxCombo;
    private int _totalScore;
    private int _totalEventCount;
    private int _currentGravityDir;
    private bool _overflow;

    private readonly Queue<GameEvent> _eventQueue = new Queue<GameEvent>();
    private readonly List<GameEvent> _executedLog = new List<GameEvent>(); // 播放器消费

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
                result.CollidingPieces.Add(pieceId);
        }
        return result;
    }

    // ── 结算协程 ─────────────────────────────────────────────────

    private IEnumerator DoSmack(int boardOrientation, SmackRules rules, Action<SmackResult> onRoundStable)
    {
        _rules = rules;
        _currentCombo = 0;
        _maxCombo = 0;
        _totalScore = 0;
        _totalEventCount = 0;
        _overflow = false;
        _eventQueue.Clear();
        _executedLog.Clear();

        // 重置触发计数
        foreach (var p in _board.AllPieces())
            p.TriggerCountThisSmack = 0;

        // A3：生成初始重力事件
        _currentGravityDir = Hex.OrientationToGravityDir(boardOrientation);
        EnqueueGravityEvents(_currentGravityDir);

        // A4：逐事件执行（先算逻辑记录，播放在下方）
        ProcessEventQueue();

        // A9：逐条播放动画
        yield return StartCoroutine(PlayEventLog());

        var result = new SmackResult
        {
            ScoreGained = _totalScore,
            MaxCombo = _maxCombo,
            EventOverflow = _overflow,
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
        while (_eventQueue.Count > 0)
        {
            if (_totalEventCount >= _rules.EventLimit)
            {
                _overflow = true;
                _eventQueue.Clear();
                Debug.LogError($"[SmackResolver] EventLimit({_rules.EventLimit}) 溢出，强制清空队列");
                break;
            }

            var ev = _eventQueue.Dequeue();
            _totalEventCount++;

            if (IsEventInvalid(ev))
            {
                ev.Skipped = true;
                _executedLog.Add(ev);
                continue;
            }

            Execute(ev);
            _executedLog.Add(ev);
        }
    }

    private bool IsEventInvalid(GameEvent ev)
    {
        if (ev.Type == GameEventType.Spawn) return false; // Spawn 无需目标存在
        if (ev.Type == GameEventType.Score) return false;
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
        }
    }

    /// <summary>GravityMove / PushMove 共用移动逻辑。isPush=true 时只移动 1 格且可推出棋盘。</summary>
    private void ExecuteMove(GameEvent ev, bool isPush)
    {
        var piece = FindPieceById(ev.TargetPieceId);
        if (piece == null) { ev.Skipped = true; return; }

        ev.FromPos = piece.Position;

        Hex to;
        if (isPush)
        {
            // 爆炸推动：只移 1 格，且可推出棋盘
            var next = piece.Position.Neighbor(ev.Direction);
            var content = _board.GetContent(next);
            if (content == CellContent.OutOfBoard)
            {
                // 推出棋盘：移除，不碰撞，不触发能力，不加连击
                ev.ToPos = next;
                ev.RemovedView = piece.View; // 记录 View，播放器负责播 PlayRemove + DestroyView
                _board.RemovePiece(piece);
                return;
            }
            if (content != CellContent.Empty)
            {
                // 推动失败：目标格有棋子或墙
                ev.Executed = false;
                ev.Skipped = true;
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
        _board.MovePiece(piece, to);

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

    private void ExecuteCollision(GameEvent ev)
    {
        var target = FindPieceById(ev.TargetPieceId);
        if (target == null) { ev.Skipped = true; return; }

        // 普通棋子不产生能力事件
        if (target.Type == PieceType.Normal) return;

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

        // 检查单棋子触发上限
        if (target.TriggerCountThisSmack >= _rules.PieceTriggerLimit)
        {
            ev.Skipped = true;
            return;
        }

        target.TriggerCountThisSmack++;
        _currentCombo++;
        if (_currentCombo > _maxCombo) _maxCombo = _currentCombo;

        switch (target.Type)
        {
            case PieceType.Score:
                _eventQueue.Enqueue(new GameEvent
                {
                    Type = GameEventType.Score,
                    TargetPieceId = target.ID,
                    ScoreDelta = _rules.ScorePieceBaseScore * _currentCombo,
                    ComboAtTrigger = _currentCombo,
                });
                break;

            case PieceType.Explosion:
                _eventQueue.Enqueue(new GameEvent
                {
                    Type = GameEventType.Explosion,
                    TargetPieceId = target.ID,
                    Direction = _currentGravityDir,
                });
                break;

            case PieceType.Split:
                _eventQueue.Enqueue(new GameEvent
                {
                    Type = GameEventType.Split,
                    TargetPieceId = target.ID,
                    Direction = ev.Direction,
                });
                break;
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

        // 移除原棋子
        _eventQueue.Enqueue(new GameEvent
        {
            Type = GameEventType.Remove,
            TargetPieceId = origin.ID,
        });

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
        });
        _eventQueue.Enqueue(new GameEvent
        {
            Type = GameEventType.Spawn,
            SpawnType = PieceType.Split,
            SpawnPos = originPos,
            Direction = dirCCW,
            SourcePieceId = origin.ID,
        });
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

        if (_factory != null)
            newPiece.View = _factory.CreateView(ev.SpawnType, finalPos);

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

        // 从最远端开始逐个推 1 格
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            var piece = _board.GetPiece(chain[i]);
            if (piece == null) continue;
            var from = piece.Position;
            var pushTo = chain[i].Neighbor(spawnDir);
            _board.MovePiece(piece, pushTo);

            _executedLog.Add(new GameEvent
            {
                Type = GameEventType.PushMove,
                TargetPieceId = piece.ID,
                SourcePieceId = sourcePieceId,
                Direction = spawnDir,
                FromPos = from,
                ToPos = pushTo,
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
        // ScoreOriginPos 由 AbilityTrigger 调用前已知（得分棋位置）
        var scorePiece = FindPieceById(ev.TargetPieceId);
        ev.ScoreOriginPos = scorePiece?.Position ?? default;
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
            if (ev.Skipped || !ev.Executed) continue;
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
            {
                if (ev.RemovedView != null)
                {
                    // 被推出棋盘：播移除动画后销毁
                    duration = ev.RemovedView.PlayRemove();
                    if (duration > 0f) yield return new WaitForSeconds(duration);
                    _factory?.DestroyView(ev.RemovedView);
                    yield break;
                }
                var piece = FindPieceByPos(ev.ToPos) ?? FindPieceById(ev.TargetPieceId);
                if (piece?.View != null && _boardView != null)
                    duration = piece.View.MoveTo(_boardView.HexToWorld(ev.ToPos));
                break;
            }
            case GameEventType.Collision:
            {
                var target = FindPieceById(ev.TargetPieceId);
                if (target?.View != null)
                    duration = target.View.PlayHitShake();
                break;
            }
            case GameEventType.AbilityTrigger:
            {
                var target = FindPieceById(ev.TargetPieceId);
                if (target?.View != null)
                    duration = target.View.PlayAbilityFX();
                break;
            }
            case GameEventType.Spawn:
            {
                if (ev.SpawnedPiece?.View != null)
                    duration = ev.SpawnedPiece.View.PlaySpawn();
                break;
            }
            case GameEventType.Remove:
            {
                // 使用执行阶段记录的 View 引用（piece 已从 Board 移除）
                if (ev.RemovedView != null)
                {
                    duration = ev.RemovedView.PlayRemove();
                    if (duration > 0f) yield return new WaitForSeconds(duration);
                    _factory?.DestroyView(ev.RemovedView);
                }
                yield break; // 已自己处理等待，跳过末尾的统一 WaitForSeconds
            }
            case GameEventType.Score:
            {
                if (_hud != null && _boardView != null)
                {
                    var worldPos = _boardView.HexToWorld(ev.ScoreOriginPos);
                    _hud.ShowScorePop(ev.ScoreDelta, ev.ComboAtTrigger, worldPos);
                }
                break;
            }
        }

        if (duration > 0f)
            yield return new WaitForSeconds(duration);
    }

    private Piece FindPieceByPos(Hex pos) => _board.GetPiece(pos);
}
