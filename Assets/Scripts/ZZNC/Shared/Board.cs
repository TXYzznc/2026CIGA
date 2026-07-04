using System.Collections.Generic;

public class Board
{
    private readonly HashSet<Hex> _walls = new HashSet<Hex>();
    private readonly Dictionary<Hex, Piece> _pieces = new Dictionary<Hex, Piece>();
    private readonly Dictionary<int, Piece> _piecesById = new Dictionary<int, Piece>();
    private readonly HashSet<Hex> _insideCells = new HashSet<Hex>();
    private int _nextId = 1;

    public void SetShape(IEnumerable<Hex> cells)
    {
        _insideCells.Clear();
        foreach (var c in cells) _insideCells.Add(c);
    }

    public void Clear()
    {
        _walls.Clear();
        _pieces.Clear();
        _piecesById.Clear();
        _nextId = 1;
    }

    // ── 只读查询 ────────────────────────────────────────────────

    public bool IsInside(Hex pos) => _insideCells.Contains(pos);

    public CellContent GetContent(Hex pos)
    {
        if (!_insideCells.Contains(pos)) return CellContent.OutOfBoard;
        if (_walls.Contains(pos))        return CellContent.Wall;
        if (_pieces.ContainsKey(pos))    return CellContent.Piece;
        return CellContent.Empty;
    }

    public Piece GetPiece(Hex pos)
    {
        _pieces.TryGetValue(pos, out var p);
        return p;
    }

    public Piece GetPieceById(int id)
    {
        _piecesById.TryGetValue(id, out var p);
        return p;
    }

    public IReadOnlyCollection<Piece> AllPieces() => _pieces.Values;

    public List<Hex> EmptyCells()
    {
        var result = new List<Hex>();
        foreach (var cell in _insideCells)
            if (!_walls.Contains(cell) && !_pieces.ContainsKey(cell))
                result.Add(cell);
        return result;
    }

    public IReadOnlyCollection<Hex> AllInsideCells() => _insideCells;

    // ── 写入 ─────────────────────────────────────────────────────

    public void PlaceWall(Hex pos) => _walls.Add(pos);

    public void PlacePiece(Piece piece, Hex pos)
    {
        if (piece.ID == 0) piece.ID = _nextId++;
        piece.Position = pos;
        _pieces[pos] = piece;
        _piecesById[piece.ID] = piece;
    }

    public void MovePiece(Piece piece, Hex to)
    {
        _pieces.Remove(piece.Position);
        piece.Position = to;
        _pieces[to] = piece;
    }

    public void RemovePiece(Piece piece)
    {
        _pieces.Remove(piece.Position);
        _piecesById.Remove(piece.ID);
    }

    internal int AllocId() => _nextId++;
}
