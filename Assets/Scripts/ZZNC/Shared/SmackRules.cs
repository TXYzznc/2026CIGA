using System.Collections.Generic;

public struct SmackRules
{
    public int EventLimit;
    public int PieceTriggerLimit;
    public int ScorePieceBaseScore;

    public static SmackRules Default => new SmackRules
    {
        EventLimit = 500,
        PieceTriggerLimit = 50,
        ScorePieceBaseScore = 100,
    };
}

public struct SmackResult
{
    public int ScoreGained;
    public int MaxCombo;
    public bool EventOverflow;
}

public struct PreviewResult
{
    public Dictionary<int, Hex> FinalPositions;
    public List<int> CollidingPieces;
}
