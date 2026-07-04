using System.Collections.Generic;
using NUnit.Framework;

public class BoardTests
{
    private Board MakeBoard3x3()
    {
        var b = new Board();
        var cells = new List<Hex>();
        for (int q = -1; q <= 1; q++)
            for (int r = -1; r <= 1; r++)
                cells.Add(new Hex(q, r));
        b.SetShape(cells);
        return b;
    }

    [Test]
    public void Empty_Board_HasNoPieces()
    {
        TestLog.Start(nameof(Empty_Board_HasNoPieces), "验证新建棋盘没有任何棋子。");
        var b = MakeBoard3x3();
        var pieceCount = new List<Piece>(b.AllPieces()).Count;
        TestLog.Step("创建 3x3 测试棋盘。");
        TestLog.Board(b);
        TestLog.Expect("棋子数量为 0。");
        TestLog.Actual("棋子数量", pieceCount);
        Assert.AreEqual(0, pieceCount);
        TestLog.Pass("空棋盘无棋子。");
    }

    [Test]
    public void PlacePiece_CanRetrieve()
    {
        TestLog.Start(nameof(PlacePiece_CanRetrieve), "验证 PlacePiece 后可以从目标格取回同一个棋子。");
        var b = MakeBoard3x3();
        var p = new Piece { Type = PieceType.Normal };
        TestLog.Step("在 (0,0) 放置普通棋。");
        b.PlacePiece(p, new Hex(0, 0));
        var actual = b.GetPiece(new Hex(0, 0));
        TestLog.Board(b);
        TestLog.Expect("GetPiece(0,0) 返回刚放入的棋子。");
        TestLog.Actual("取回棋子", TestLog.Piece(actual));
        Assert.AreEqual(p, actual);
        TestLog.Pass("PlacePiece 写入和查询一致。");
    }

    [Test]
    public void MovePiece_UpdatesPosition()
    {
        TestLog.Start(nameof(MovePiece_UpdatesPosition), "验证 MovePiece 会更新格子占用和 Piece.Position。");
        var b = MakeBoard3x3();
        var p = new Piece { Type = PieceType.Score };
        b.PlacePiece(p, new Hex(0, 0));
        TestLog.Step("先在 (0,0) 放置得分棋。");
        TestLog.Board(b);
        TestLog.Step("把棋子移动到 (0,1)。");
        b.MovePiece(p, new Hex(0, 1));
        TestLog.Board(b);
        TestLog.Expect("(0,0) 为空，(0,1) 为原棋子，Piece.Position=(0,1)。");
        TestLog.Actual("(0,0) 棋子", TestLog.Piece(b.GetPiece(new Hex(0, 0))));
        TestLog.Actual("(0,1) 棋子", TestLog.Piece(b.GetPiece(new Hex(0, 1))));
        TestLog.Actual("Piece.Position", p.Position);
        Assert.IsNull(b.GetPiece(new Hex(0, 0)));
        Assert.AreEqual(p, b.GetPiece(new Hex(0, 1)));
        Assert.AreEqual(new Hex(0, 1), p.Position);
        TestLog.Pass("MovePiece 状态更新正确。");
    }

    [Test]
    public void RemovePiece_CellBecomesEmpty()
    {
        TestLog.Start(nameof(RemovePiece_CellBecomesEmpty), "验证 RemovePiece 后原格变为空。");
        var b = MakeBoard3x3();
        var p = new Piece { Type = PieceType.Normal };
        b.PlacePiece(p, new Hex(0, 0));
        TestLog.Step("在 (0,0) 放置普通棋后移除。");
        TestLog.Board(b);
        b.RemovePiece(p);
        var content = b.GetContent(new Hex(0, 0));
        TestLog.Board(b);
        TestLog.Expect("(0,0) 内容为 Empty。");
        TestLog.Actual("(0,0) 内容", content);
        Assert.AreEqual(CellContent.Empty, content);
        TestLog.Pass("RemovePiece 清理格子占用。");
    }

    [Test]
    public void Wall_NotAPiece()
    {
        TestLog.Start(nameof(Wall_NotAPiece), "验证墙体是 CellContent.Wall，不属于 Piece。");
        var b = MakeBoard3x3();
        b.PlaceWall(new Hex(1, 0));
        var content = b.GetContent(new Hex(1, 0));
        var piece = b.GetPiece(new Hex(1, 0));
        TestLog.Step("在 (1,0) 放置墙体。");
        TestLog.Board(b);
        TestLog.Expect("GetContent 为 Wall，GetPiece 为 null。");
        TestLog.Actual("GetContent(1,0)", content);
        TestLog.Actual("GetPiece(1,0)", TestLog.Piece(piece));
        Assert.AreEqual(CellContent.Wall, content);
        Assert.IsNull(piece);
        TestLog.Pass("墙体不会被当作棋子返回。");
    }

    [Test]
    public void OutsideBoard_ReturnsOutOfBoard()
    {
        TestLog.Start(nameof(OutsideBoard_ReturnsOutOfBoard), "验证棋盘外坐标返回 OutOfBoard。");
        var b = MakeBoard3x3();
        var content = b.GetContent(new Hex(99, 99));
        TestLog.Step("查询远离棋盘的坐标 (99,99)。");
        TestLog.Expect("返回 OutOfBoard。");
        TestLog.Actual("GetContent(99,99)", content);
        Assert.AreEqual(CellContent.OutOfBoard, content);
        TestLog.Pass("棋盘外检测正确。");
    }

    [Test]
    public void EmptyCells_ExcludesWallsAndPieces()
    {
        TestLog.Start(nameof(EmptyCells_ExcludesWallsAndPieces), "验证 EmptyCells 不包含墙体和已有棋子。");
        var b = MakeBoard3x3();
        b.PlaceWall(new Hex(0, 1));
        var p = new Piece { Type = PieceType.Normal };
        b.PlacePiece(p, new Hex(0, 0));
        var empties = b.EmptyCells();
        TestLog.Step("在 (0,1) 放墙，在 (0,0) 放普通棋。");
        TestLog.Board(b);
        TestLog.Expect("EmptyCells 不包含 (0,1) 和 (0,0)。");
        TestLog.Actual("空格列表", TestLog.HexList(empties));
        Assert.IsFalse(empties.Contains(new Hex(0, 1)));
        Assert.IsFalse(empties.Contains(new Hex(0, 0)));
        TestLog.Pass("空格查询正确排除墙体和棋子。");
    }
}
