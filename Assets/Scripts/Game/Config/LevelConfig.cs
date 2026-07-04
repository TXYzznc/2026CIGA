using Ciga2026.Shared;
using UnityEngine;

namespace Ciga2026.Game.Config
{
    [System.Serializable]
    public sealed class BoardLayoutJson
    {
        public HexCoord[] walls;
        public BoardPieceJson[] pieces;
    }

    [System.Serializable]
    public struct BoardPieceJson
    {
        public PieceType type;
        public int q;
        public int r;

        public Hex ToHex() => new Hex(q, r);
    }

    [CreateAssetMenu(menuName = "CIGA 2026/Level Config")]
    public sealed class LevelConfig : ScriptableObject
    {
        [Header("Board")]
        [Min(0)] public int boardRadius = 2;
        public HexCoord[] walls;

        [Header("Optional Fixed Layout")]
        public TextAsset boardLayoutJson;

        [Header("Initial Pieces")]
        [Min(0)] public int initialPieceCount = 6;
        public PieceWeight[] initialPiecePool =
        {
            new PieceWeight { type = PieceType.Normal, weight = 6 },
            new PieceWeight { type = PieceType.Score, weight = 3 },
        };

        [Header("Choice")]
        public PieceWeight[] choicePool =
        {
            new PieceWeight { type = PieceType.Normal, weight = 5 },
            new PieceWeight { type = PieceType.Score, weight = 3 },
            new PieceWeight { type = PieceType.Explosion, weight = 1 },
        };

        [Header("Goal")]
        [Min(1)] public int targetScore = 100;
        [Min(1)] public int smackCount = 8;

        [Header("Smack Rules")]
        [Min(1)] public int eventLimit = 500;
        [Min(1)] public int pieceTriggerLimit = 50;
        [Min(1)] public int scorePieceBaseScore = 10;

        public SmackRules ToSmackRules()
        {
            return new SmackRules(eventLimit, pieceTriggerLimit, scorePieceBaseScore);
        }

        public bool TryGetBoardLayout(out BoardLayoutJson layout)
        {
            layout = null;
            if (boardLayoutJson == null || string.IsNullOrWhiteSpace(boardLayoutJson.text))
            {
                return false;
            }

            layout = JsonUtility.FromJson<BoardLayoutJson>(boardLayoutJson.text);
            return layout != null;
        }
    }
}
