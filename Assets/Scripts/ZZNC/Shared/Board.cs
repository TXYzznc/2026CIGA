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

    // ── 快照 (Undo) ──────────────────────────────────────────────

    public class Snapshot
    {
        public List<PieceData> Pieces = new List<PieceData>();
        public int NextId;

        public struct PieceData
        {
            public int ID;
            public PieceType Type;
            public Hex Position;
        }
    }

    /// <summary>保存棋子数据快照（不含墙体/形状，它们布局期间不变）。</summary>
    public Snapshot Capture()
    {
        var snap = new Snapshot { NextId = _nextId };
        foreach (var p in _pieces.Values)
            snap.Pieces.Add(new Snapshot.PieceData { ID = p.ID, Type = p.Type, Position = p.Position });
        return snap;
    }

    /// <summary>从快照恢复棋子数据（保留墙体/形状）。</summary>
    public void Restore(Snapshot snap)
    {
        _pieces.Clear();
        _piecesById.Clear();
        _nextId = snap.NextId;
        foreach (var pd in snap.Pieces)
        {
            var piece = new Piece { ID = pd.ID, Type = pd.Type };
            piece.Position = pd.Position;
            _pieces[pd.Position] = piece;
            _piecesById[pd.ID] = piece;
        }
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
        if (_pieces.ContainsKey(pos))
        {
            UnityEngine.Debug.LogWarning($"[Board] 棋子重叠: 位置 {pos} 已有 #{_pieces[pos].ID}，正在放置 #{piece.ID} 将覆盖。请检查结算逻辑。");
        }
        if (piece.ID == 0) piece.ID = _nextId++;
        piece.Position = pos;
        _pieces[pos] = piece;
        _piecesById[piece.ID] = piece;
    }

    public void MovePiece(Piece piece, Hex to)
    {
        if (_pieces.ContainsKey(to) && _pieces[to] != piece)
        {
            UnityEngine.Debug.LogWarning($"[Board] 移动时目标位置 {to} 已有 #{_pieces[to].ID}，但移动者是 #{piece.ID}，可能产生重叠。");
        }
        _pieces.Remove(piece.Position);
        piece.Position = to;
        _pieces[to] = piece;
    }

    public void RemovePiece(Piece piece)
    {
        _pieces.Remove(piece.Position);
        _piecesById.Remove(piece.ID);
    }

    /// <summary>原子交换两枚棋子的位置（不触发碰撞/事件）。</summary>
    public void SwapPieces(Piece a, Piece b)
    {
        var posA = a.Position;
        var posB = b.Position;
        _pieces.Remove(posA);
        _pieces.Remove(posB);
        a.Position = posB;
        b.Position = posA;
        _pieces[posB] = a;
        _pieces[posA] = b;
    }

    internal int AllocId() => _nextId++;
}
