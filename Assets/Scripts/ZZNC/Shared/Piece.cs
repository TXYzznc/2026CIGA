public class Piece
{
    public int ID { get; internal set; }
    public PieceType Type { get; set; }
    public Hex Position { get; internal set; }

    public int TriggerCountThisSmack { get; set; }

    /// <summary>得分棋独立计数：本拍击内受到有效作用的次数。</summary>
    public int ScoreHitCount { get; set; }

    public IPieceView View { get; set; }

    public override string ToString() => $"Piece#{ID}[{Type}]@{Position}";
}
