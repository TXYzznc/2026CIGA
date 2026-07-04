using System.Collections.Generic;
using NUnit.Framework;

public class GravityMoveTests
{
    private static Board MakeBoard(int radius = 2)
    {
        var board = new Board();
        var cells = new List<Hex>();
        for (int q = -radius; q <= radius; q++)
            for (int r = -radius; r <= radius; r++)
                cells.Add(new Hex(q, r));
        board.SetShape(cells);
        return board;
    }

    private static Hex CalcFarthestOnBoard(Board board, Hex from, int dir)
    {
        var cur = from;
        while (true)
        {
            var next = cur.Neighbor(dir);
            var content = board.GetContent(next);
            if (content == CellContent.OutOfBoard || content == CellContent.Wall || content == CellContent.Piece)
                break;
            cur = next;
        }
        return cur;
    }

    private static int TrackIndex(Hex pos, int gravDir)
    {
        switch (gravDir)
        {
            case 0: return pos.q;
            case 1: return -pos.r;
            case 2: return -(pos.q + pos.r);
            case 3: return -pos.q;
            case 4: return pos.r;
            case 5: return pos.q + pos.r;
            default: return TrackIndex(pos, Hex.RotateDir(gravDir, 0));
        }
    }

    private static int DepthIndex(Hex pos, int gravDir)
    {
        switch (gravDir)
        {
            case 0: return -pos.r;
            case 1: return -(pos.q + pos.r);
            case 2: return -pos.q;
            case 3: return pos.r;
            case 4: return pos.q + pos.r;
            case 5: return pos.q;
            default: return DepthIndex(pos, Hex.RotateDir(gravDir, 0));
        }
    }

    [Test]
    public void CalcFarthestOnBoard_MovesToFarthestEmptyCell()
    {
        TestLog.Start(nameof(CalcFarthestOnBoard_MovesToFarthestEmptyCell), "验证棋子沿 D0 重力方向移动到最远空格。");
        var board = MakeBoard();
        var piece = new Piece { Type = PieceType.Normal };
        board.PlacePiece(piece, new Hex(0, -2));
        TestLog.Step("创建半径 2 的棋盘，并把普通棋放在 (0,-2)。");
        TestLog.Board(board);

        var target = CalcFarthestOnBoard(board, piece.Position, 0);
        TestLog.Step("从棋子当前位置沿 D0 逐格寻找最远合法位置。");
        TestLog.Expect("目标位置应为棋盘底部 (0,2)。");
        TestLog.Actual("计算目标", target);

        Assert.AreEqual(new Hex(0, 2), target);
        TestLog.Pass("棋子会落到最远空格。");
    }

    [Test]
    public void CalcFarthestOnBoard_StopsBeforeWall()
    {
        TestLog.Start(nameof(CalcFarthestOnBoard_StopsBeforeWall), "验证重力移动遇墙时停在墙前一格。");
        var board = MakeBoard();
        var piece = new Piece { Type = PieceType.Normal };
        board.PlacePiece(piece, new Hex(0, -2));
        board.PlaceWall(new Hex(0, 1));
        TestLog.Step("普通棋在 (0,-2)，墙体在 (0,1)。");
        TestLog.Board(board);

        var target = CalcFarthestOnBoard(board, piece.Position, 0);
        TestLog.Step("沿 D0 搜索，前方 (0,1) 是墙。");
        TestLog.Expect("目标位置应停在 (0,0)。");
        TestLog.Actual("计算目标", target);

        Assert.AreEqual(new Hex(0, 0), target);
        TestLog.Pass("墙体正确阻挡重力移动。");
    }

    [Test]
    public void CalcFarthestOnBoard_StopsBeforePiece()
    {
        TestLog.Start(nameof(CalcFarthestOnBoard_StopsBeforePiece), "验证重力移动遇棋子时停在目标棋前一格。");
        var board = MakeBoard();
        var mover = new Piece { Type = PieceType.Normal };
        var blocker = new Piece { Type = PieceType.Score };
        board.PlacePiece(mover, new Hex(0, -2));
        board.PlacePiece(blocker, new Hex(0, 1));
        TestLog.Step("移动棋在 (0,-2)，阻挡得分棋在 (0,1)。");
        TestLog.Board(board);

        var target = CalcFarthestOnBoard(board, mover.Position, 0);
        TestLog.Expect("目标位置应停在阻挡棋前方 (0,0)。");
        TestLog.Actual("计算目标", target);

        Assert.AreEqual(new Hex(0, 0), target);
        TestLog.Pass("棋子正确阻挡重力移动。");
    }

    [Test]
    public void CalcFarthestOnBoard_StopsAtBoardEdge()
    {
        TestLog.Start(nameof(CalcFarthestOnBoard_StopsAtBoardEdge), "验证普通重力不会推出棋盘，会停在边缘格。");
        var board = MakeBoard(1);
        var piece = new Piece { Type = PieceType.Normal };
        board.PlacePiece(piece, new Hex(0, 0));
        TestLog.Step("创建半径 1 棋盘，普通棋从中心沿 D0 移动。");
        TestLog.Board(board);

        var target = CalcFarthestOnBoard(board, piece.Position, 0);
        TestLog.Expect("目标位置应为边缘格 (0,1)，不是棋盘外。");
        TestLog.Actual("计算目标", target);

        Assert.AreEqual(new Hex(0, 1), target);
        TestLog.Pass("边界正确阻挡普通重力。");
    }

    [Test]
    public void GravityOrder_SortsByTrackThenDepth()
    {
        TestLog.Start(nameof(GravityOrder_SortsByTrackThenDepth), "验证重力事件按轨道左到右、轨道内下到上排序。");
        const int gravDir = 0;
        var bottomRightTrack = new Piece { Type = PieceType.Normal };
        var upperLeftTrack = new Piece { Type = PieceType.Normal };
        var middleTrack = new Piece { Type = PieceType.Normal };
        var positions = new Dictionary<Piece, Hex>
        {
            { bottomRightTrack, new Hex(1, 2) },
            { upperLeftTrack, new Hex(-1, -1) },
            { middleTrack, new Hex(0, 0) },
        };
        var pieces = new List<Piece> { bottomRightTrack, upperLeftTrack, middleTrack };
        TestLog.Step("准备三个棋子，分别位于左轨、中轨、右轨。重力方向 D0。");
        foreach (var kv in positions)
            TestLog.State(TestLog.Piece(kv.Key), "pos=" + kv.Value + ", track=" + TrackIndex(kv.Value, gravDir) + ", depth=" + DepthIndex(kv.Value, gravDir));

        pieces.Sort((a, b) =>
        {
            int trackA = TrackIndex(positions[a], gravDir);
            int trackB = TrackIndex(positions[b], gravDir);
            if (trackA != trackB) return trackA.CompareTo(trackB);
            return DepthIndex(positions[a], gravDir).CompareTo(DepthIndex(positions[b], gravDir));
        });
        TestLog.Step("按 TrackIndex 升序，再按 DepthIndex 升序排序。");
        TestLog.Expect("排序结果应为 upperLeftTrack -> middleTrack -> bottomRightTrack。");
        TestLog.Actual("排序结果", string.Join(" -> ", pieces.ConvertAll(p => positions[p].ToString())));

        CollectionAssert.AreEqual(new[] { upperLeftTrack, middleTrack, bottomRightTrack }, pieces);
        Assert.AreEqual(-1, TrackIndex(positions[upperLeftTrack], gravDir));
        Assert.AreEqual(0, TrackIndex(positions[middleTrack], gravDir));
        Assert.AreEqual(1, TrackIndex(positions[bottomRightTrack], gravDir));
        TestLog.Pass("轨道排序符合左到右规则。");
    }
}
