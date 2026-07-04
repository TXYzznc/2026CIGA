public class Piece
{
    public int ID { get; internal set; }
    public PieceType Type { get; set; }
    public Hex Position { get; internal set; }

    public int TriggerCountThisSmack { get; set; }

    public IPieceView View { get; set; }

    public override string ToString() => $"Piece#{ID}[{Type}]@{Position}";
}
