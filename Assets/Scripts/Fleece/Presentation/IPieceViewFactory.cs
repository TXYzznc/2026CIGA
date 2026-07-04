public interface IPieceViewFactory
{
    IPieceView CreateView(PieceType type, Hex hex);
    void DestroyView(IPieceView view);
}

