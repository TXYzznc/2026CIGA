using System.Collections.Generic;
using UnityEngine;

public static class ZZNCLevelConfigLoader
{
    public const string ResourcePath = "DataTable/ZZNCLevelConfig";

    private static ZZNCLevelConfigTable cachedTable;
    private static Dictionary<int, List<ZZNCLevelRoundConfig>> roundsByLevelId;

    public static ZZNCLevelConfigTable Load()
    {
        if (cachedTable != null)
        {
            return cachedTable;
        }

        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
        {
            Debug.LogError($"ZZNC level config not found at Resources/{ResourcePath}.json");
            cachedTable = new ZZNCLevelConfigTable { rounds = new ZZNCLevelRoundConfig[0], choicePool = new ZZNCPieceWeightConfig[0] };
            return cachedTable;
        }

        cachedTable = JsonUtility.FromJson<ZZNCLevelConfigTable>(asset.text);
        if (cachedTable == null || cachedTable.rounds == null)
        {
            Debug.LogError("ZZNC level config parse failed.");
            cachedTable = new ZZNCLevelConfigTable { rounds = new ZZNCLevelRoundConfig[0], choicePool = new ZZNCPieceWeightConfig[0] };
        }
        if (cachedTable.choicePool == null)
            cachedTable.choicePool = new ZZNCPieceWeightConfig[0];

        return cachedTable;
    }

    public static List<ZZNCLevelRoundConfig> GetRoundsByLevelId(int levelId)
    {
        EnsureIndex();
        if (!roundsByLevelId.TryGetValue(levelId, out var result))
            result = new List<ZZNCLevelRoundConfig>();
        return result;
    }

    public static void ClearCache()
    {
        cachedTable = null;
        roundsByLevelId = null;
    }

    private static void EnsureIndex()
    {
        if (roundsByLevelId != null)
        {
            return;
        }

        ZZNCLevelConfigTable table = Load();
        roundsByLevelId = new Dictionary<int, List<ZZNCLevelRoundConfig>>();

        foreach (ZZNCLevelRoundConfig config in table.rounds)
        {
            if (!roundsByLevelId.TryGetValue(config.levelId, out var rounds))
            {
                rounds = new List<ZZNCLevelRoundConfig>();
                roundsByLevelId[config.levelId] = rounds;
            }
            rounds.Add(config);
        }

        foreach (var rounds in roundsByLevelId.Values)
            rounds.Sort((a, b) => a.roundIndex.CompareTo(b.roundIndex));
    }
}
