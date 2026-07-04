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

    public IPieceView RemovedView;   // Remove/推出棋盘时记录，Board移除后播放器仍可用
    public Piece SpawnedPiece;       // Spawn后记录，播放器播SpawnFX用
    public Hex ScoreOriginPos;       // Score事件触发位置，飘分用

    public override string ToString() =>
        $"[{Type}] target={TargetPieceId} src={SourcePieceId} dir={Direction}";
}
