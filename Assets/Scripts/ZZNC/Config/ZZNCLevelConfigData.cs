using System;

[Serializable]
public class ZZNCLevelConfigTable
{
    public ZZNCLevelRoundConfig[] rounds;
    public ZZNCPieceWeightConfig[] choicePool;
}

[Serializable]
public class ZZNCLevelRoundConfig
{
    public int id;
    public int levelId;
    public int roundIndex;
    public int maxRowLength;
    public int smackCount;
    public int targetScore;
    public int addWallCountOnPass;
    public int initialPieceCount;
}

[Serializable]
public class ZZNCPieceWeightConfig
{
    public string pieceType;
    public int weight;
}
