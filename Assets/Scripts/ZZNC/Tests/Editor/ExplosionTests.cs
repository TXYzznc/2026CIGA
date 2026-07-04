using System.Collections.Generic;
using NUnit.Framework;

public class ExplosionTests
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

    private static List<GameEvent> CreateExplosionPushEvents(Board board, Piece center, int gravityDir)
    {
        var events = new List<GameEvent>();
        for (int i = 0; i < 6; i++)
        {
            int dir = Hex.RotateDir(gravityDir, i);
            var neighbor = center.Position.Neighbor(dir);
            if (board.GetContent(neighbor) == CellContent.Wall)
                continue;

            var piece = board.GetPiece(neighbor);
            if (piece == null)
                continue;

            events.Add(new GameEvent
            {
                Type = GameEventType.PushMove,
                TargetPieceId = piece.ID,
                SourcePieceId = center.ID,
                Direction = dir,
            });
        }
        return events;
    }

    private static bool ExecutePushMove(Board board, Piece piece, int dir, out bool removed, out bool collisionCreated)
    {
        removed = false;
        collisionCreated = false;
        var next = piece.Position.Neighbor(dir);
        var content = board.GetContent(next);
        if (content == CellContent.OutOfBoard)
        {
            board.RemovePiece(piece);
            removed = true;
            return true;
        }

        if (content != CellContent.Empty)
            return false;

        board.MovePiece(piece, next);
        collisionCreated = board.GetPiece(next.Neighbor(dir)) != null;
        return true;
    }

    [Test]
    public void ExplosionWithAdjacentPiece_CreatesPushMoveAwayFromCenter()
    {
        TestLog.Start(nameof(ExplosionWithAdjacentPiece_CreatesPushMoveAwayFromCenter), "验证爆炸棋相邻有棋子时产生远离中心的 PushMove。");
        var board = MakeBoard();
        var center = new Piece { Type = PieceType.Explosion };
        var neighbor = new Piece { Type = PieceType.Normal };
        board.PlacePiece(center, new Hex(0, 0));
        board.PlacePiece(neighbor, new Hex(0, 1));
        TestLog.Step("爆炸棋在中心 (0,0)，普通棋位于 D0 相邻格 (0,1)。");
        TestLog.Board(board);

        var events = CreateExplosionPushEvents(board, center, 0);
        TestLog.Step("以重力方向 D0 为起点，按顺时针 D0~D5 扫描相邻格。");
        TestLog.Expect("应产生 1 个 PushMove，方向 D0，目标为相邻普通棋。");
        TestLog.Actual("事件数量", events.Count);
        foreach (var ev in events)
            TestLog.Actual("事件", TestLog.Event(ev));

        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(GameEventType.PushMove, events[0].Type);
        Assert.AreEqual(neighbor.ID, events[0].TargetPieceId);
        Assert.AreEqual(0, events[0].Direction);
        TestLog.Pass("爆炸推动方向正确远离中心。");
    }

    [Test]
    public void ExplosionNeighborWall_CreatesNoEvent()
    {
        TestLog.Start(nameof(ExplosionNeighborWall_CreatesNoEvent), "验证爆炸棋相邻格为墙时不产生 PushMove。");
        var board = MakeBoard();
        var center = new Piece { Type = PieceType.Explosion };
        board.PlacePiece(center, new Hex(0, 0));
        board.PlaceWall(new Hex(0, 1));
        TestLog.Step("爆炸棋在中心，D0 相邻格放置墙体。");
        TestLog.Board(board);

        var events = CreateExplosionPushEvents(board, center, 0);
        TestLog.Expect("墙体不是棋子，不产生事件。");
        TestLog.Actual("事件数量", events.Count);

        Assert.AreEqual(0, events.Count);
        TestLog.Pass("墙体正确忽略。");
    }

    [Test]
    public void PushMoveTargetOccupied_FailsAndPieceDoesNotMove()
    {
        TestLog.Start(nameof(PushMoveTargetOccupied_FailsAndPieceDoesNotMove), "验证爆炸推动目标格被占时推动失败，棋子不移动。");
        var board = MakeBoard();
        var pushed = new Piece { Type = PieceType.Normal };
        var blocker = new Piece { Type = PieceType.Score };
        board.PlacePiece(pushed, new Hex(0, 1));
        board.PlacePiece(blocker, new Hex(0, 2));
        TestLog.Step("被推棋在 (0,1)，推动方向 D0 的目标格 (0,2) 已被得分棋占用。");
        TestLog.Board(board);

        bool moved = ExecutePushMove(board, pushed, 0, out var removed, out var collisionCreated);
        TestLog.Expect("推动失败 moved=false，removed=false，不产生碰撞，棋子仍在 (0,1)。");
        TestLog.Actual("是否移动", moved);
        TestLog.Actual("是否移除", removed);
        TestLog.Actual("是否产生碰撞", collisionCreated);
        TestLog.Actual("被推棋位置", pushed.Position);
        TestLog.Board(board);

        Assert.IsFalse(moved);
        Assert.IsFalse(removed);
        Assert.IsFalse(collisionCreated);
        Assert.AreEqual(new Hex(0, 1), pushed.Position);
        Assert.AreEqual(pushed, board.GetPiece(new Hex(0, 1)));
        TestLog.Pass("目标格被占时推动失败且棋盘不变。");
    }

    [Test]
    public void PushMoveOutOfBoard_RemovesPieceAndCreatesNoCollisionOrCombo()
    {
        TestLog.Start(nameof(PushMoveOutOfBoard_RemovesPieceAndCreatesNoCollisionOrCombo), "验证爆炸可将棋子推出棋盘，且不产生碰撞/连击。");
        var board = MakeBoard(1);
        var pushed = new Piece { Type = PieceType.Score };
        board.PlacePiece(pushed, new Hex(0, 1));
        int combo = 0;
        TestLog.Step("半径 1 棋盘中，得分棋在 D0 边缘 (0,1)，继续 D0 会出界。");
        TestLog.Board(board);

        bool moved = ExecutePushMove(board, pushed, 0, out var removed, out var collisionCreated);
        TestLog.Expect("出界推动会移除棋子，collisionCreated=false，combo 保持 0。");
        TestLog.Actual("是否执行移动/移除", moved);
        TestLog.Actual("是否移除", removed);
        TestLog.Actual("是否产生碰撞", collisionCreated);
        TestLog.Actual("combo", combo);
        TestLog.Actual("按 ID 查找被推棋", board.GetPieceById(pushed.ID));

        Assert.IsTrue(moved);
        Assert.IsTrue(removed);
        Assert.IsFalse(collisionCreated);
        Assert.AreEqual(0, combo);
        Assert.IsNull(board.GetPieceById(pushed.ID));
        TestLog.Pass("推出棋盘只移除棋子，不触发额外连锁。");
    }
}
