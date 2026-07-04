using Ciga2026.Shared;

namespace Ciga2026.Game.Presentation
{
    public interface IPieceViewFactory
    {
        PieceView CreateView(PieceType type, Hex hex);
        void DestroyView(PieceView view);
    }
}
