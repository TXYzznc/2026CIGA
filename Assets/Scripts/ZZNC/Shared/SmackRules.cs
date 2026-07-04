using System.Collections.Generic;

public struct SmackRules
{
    public int EventLimit;
    public int PieceTriggerLimit;

    public static SmackRules Default => new SmackRules
    {
        EventLimit = 500,
        PieceTriggerLimit = 50,
    };
}

public struct SmackResult
{
    public int ScoreGained;
    public bool EventOverflow;
}

public struct PreviewResult
{
    public Dictionary<int, Hex> FinalPositions;
    public List<int> CollidingPieces;
    /// <summary>将被撞中的棋子 ID（橙色描边对象）。</summary>
    public List<int> HitTargetIds;
}
