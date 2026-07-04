using UnityEngine;

public enum GameEventType
{
    GravityMove,
    PushMove,
    Collision,
    AbilityTrigger,
    Explosion,
    Split,
    Spawn,
    Remove,
    Score,

    // 新棋子事件
    BounceMove,     // 反弹移动
    TurnMove,       // 转向移动
    SwapPosition,   // 交换位置
    ContinueMove,   // 交换后继续移动
    StomachMove,    // 胃袋连续移动
    Consume,        // 单个吞噬删除
    AreaConsume,    // 相邻范围吞噬
    RingRotate,     // 旋风环状态轮换
}

public class GameEvent
{
    public GameEventType Type;

    public int TargetPieceId;
    public int SourcePieceId;
    public int Direction;

    // Score 事件
    public int ScoreDelta;
    public int ComboAtTrigger;

    // Spawn 事件
    public PieceType SpawnType;
    public Hex SpawnPos;

    // 执行后记录（播放器用）
    public bool Executed;
    public bool Skipped;
    public Hex FromPos;
    public Hex ToPos;
    public Vector3 FromWorldPos;
    public Vector3 ToWorldPos;

    public IPieceView View;

    public IPieceView RemovedView;
    public Piece SpawnedPiece;
    public Hex ScoreOriginPos;

    // 胃袋/吞噬计数
    public int ConsumeCount;

    // 旋风环旋转快照（位置数组），最多6格
    public Hex[] RingSnapshot;

    public override string ToString() =>
        $"[{Type}] target={TargetPieceId} src={SourcePieceId} dir={Direction}";
}
