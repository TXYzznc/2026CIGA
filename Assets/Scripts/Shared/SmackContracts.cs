namespace Ciga2026.Shared
{
    public readonly struct SmackRules
    {
        public readonly int EventLimit;
        public readonly int PieceTriggerLimit;
        public readonly int ScorePieceBaseScore;

        public SmackRules(int eventLimit, int pieceTriggerLimit, int scorePieceBaseScore)
        {
            EventLimit = eventLimit;
            PieceTriggerLimit = pieceTriggerLimit;
            ScorePieceBaseScore = scorePieceBaseScore;
        }
    }

    public readonly struct SmackResult
    {
        public readonly int ScoreGained;
        public readonly int MaxCombo;
        public readonly bool EventOverflow;

        public SmackResult(int scoreGained, int maxCombo, bool eventOverflow)
        {
            ScoreGained = scoreGained;
            MaxCombo = maxCombo;
            EventOverflow = eventOverflow;
        }
    }
}
