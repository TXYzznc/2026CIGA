public interface IPieceViewFactory
{
    IPieceView CreateView(PieceType type, Hex pos);
    void DestroyView(IPieceView view);
}
