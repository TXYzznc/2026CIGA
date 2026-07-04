using System.Collections.Generic;

namespace Ciga2026.Shared
{
    public sealed class Board
    {
        private readonly HashSet<Hex> playableCells = new HashSet<Hex>();
        private readonly HashSet<Hex> walls = new HashSet<Hex>();
        private readonly Dictionary<Hex, Piece> piecesByCell = new Dictionary<Hex, Piece>();
        private readonly Dictionary<int, Piece> piecesById = new Dictionary<int, Piece>();
        private int nextPieceId = 1;

        public IEnumerable<Hex> AllCells => playableCells;

        public void SetHexagonShape(int radius)
        {
            playableCells.Clear();
            var safeRadius = UnityEngine.Mathf.Max(0, radius);
            for (var q = -safeRadius; q <= safeRadius; q++)
            {
                var r1 = UnityEngine.Mathf.Max(-safeRadius, -q - safeRadius);
                var r2 = UnityEngine.Mathf.Min(safeRadius, -q + safeRadius);
                for (var r = r1; r <= r2; r++)
                {
                    playableCells.Add(new Hex(q, r));
                }
            }
        }

        public CellContent GetContent(Hex hex)
        {
            if (!IsInside(hex))
            {
                return CellContent.OutOfBoard;
            }

            if (walls.Contains(hex))
            {
                return CellContent.Wall;
            }

            return piecesByCell.ContainsKey(hex) ? CellContent.Piece : CellContent.Empty;
        }

        public Piece GetPiece(Hex hex)
        {
            piecesByCell.TryGetValue(hex, out var piece);
            return piece;
        }

        public IReadOnlyCollection<Piece> AllPieces() => piecesById.Values;

        public List<Hex> EmptyCells()
        {
            var result = new List<Hex>();
            foreach (var cell in playableCells)
            {
                if (GetContent(cell) == CellContent.Empty)
                {
                    result.Add(cell);
                }
            }

            return result;
        }

        public bool IsInside(Hex hex) => playableCells.Contains(hex);

        public bool PlaceWall(Hex hex)
        {
            if (!IsInside(hex) || piecesByCell.ContainsKey(hex))
            {
                return false;
            }

            walls.Add(hex);
            return true;
        }

        public bool PlacePiece(Piece piece, Hex hex)
        {
            if (piece == null || GetContent(hex) != CellContent.Empty)
            {
                return false;
            }

            if (piece.ID <= 0)
            {
                piece.ID = nextPieceId++;
            }

            piece.Position = hex;
            piecesByCell[hex] = piece;
            piecesById[piece.ID] = piece;
            return true;
        }

        public bool MovePiece(Piece piece, Hex hex)
        {
            if (piece == null || GetContent(hex) != CellContent.Empty)
            {
                return false;
            }

            piecesByCell.Remove(piece.Position);
            piece.Position = hex;
            piecesByCell[hex] = piece;
            return true;
        }

        public bool RemovePiece(Piece piece)
        {
            if (piece == null || !piecesById.ContainsKey(piece.ID))
            {
                return false;
            }

            piecesByCell.Remove(piece.Position);
            piecesById.Remove(piece.ID);
            return true;
        }

        public void Clear()
        {
            walls.Clear();
            piecesByCell.Clear();
            piecesById.Clear();
            nextPieceId = 1;
        }
    }
}
