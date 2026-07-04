using Ciga2026.Game.Presentation;

namespace Ciga2026.Shared
{
    public sealed class Piece
    {
        public int ID { get; internal set; }
        public PieceType Type { get; set; }
        public Hex Position { get; set; }
        public int TriggerCountThisSmack { get; set; }
        public PieceView View { get; set; }

        public Piece(PieceType type)
        {
            Type = type;
        }
    }
}
