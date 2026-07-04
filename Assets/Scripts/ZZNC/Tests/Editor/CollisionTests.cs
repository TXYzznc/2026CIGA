using System.Collections.Generic;
using NUnit.Framework;

public class CollisionTests
{
    private static Board MakeBoard3x3()
    {
        var board = new Board();
        var cells = new List<Hex>();
        for (int q = -1; q <= 1; q++)
            for (int r = -1; r <= 1; r++)
                cells.Add(new Hex(q, r));
        board.SetShape(cells);
        return board;
    }

    private static bool TryGravityMoveAndCreateCollision(Board board, Piece mover, int dir, out Piece collisionTarget)
    {
        collisionTarget = null;
        var from = mover.Position;
        var to = CalcFarthestOnBoard(board, from, dir);
        if (to == from)
            return false;

        board.MovePiece(mover, to);
        collisionTarget = board.GetPiece(to.Neighbor(dir));
        return collisionTarget != null;
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

    private static bool TryCreateAbilityTrigger(Piece collisionTarget, int dir, Piece source, out GameEvent abilityEvent)
    {
        abilityEvent = null;
        if (collisionTarget.Type == PieceType.Normal)
            return false;

        abilityEvent = new GameEvent
        {
            Type = GameEventType.AbilityTrigger,
            TargetPieceId = collisionTarget.ID,
            SourcePieceId = source.ID,
            Direction = dir,
        };
        return true;
    }

    [Test]
    public void CollisionWithNormalPiece_CreatesNoAbilityTrigger()
    {
        TestLog.Start(nameof(CollisionWithNormalPiece_CreatesNoAbilityTrigger), "验证普通棋被撞时只产生碰撞，不生成能力触发事件。");
        var board = MakeBoard3x3();
        var mover = new Piece { Type = PieceType.Score };
        var target = new Piece { Type = PieceType.Normal };
        board.PlacePiece(mover, new Hex(0, -1));
        board.PlacePiece(target, new Hex(0, 1));
        TestLog.Step("得分棋作为移动来源，普通棋作为被撞目标。");
        TestLog.Board(board);

        bool collided = TryGravityMoveAndCreateCollision(board, mover, 0, out var collisionTarget);
        TestLog.Step("移动来源沿 D0 移动到最远合法位置，并检查正前方棋子。");
        TestLog.Actual("是否碰撞", collided);
        TestLog.Actual("碰撞目标", TestLog.Piece(collisionTarget));
        bool triggered = TryCreateAbilityTrigger(collisionTarget, 0, mover, out var abilityEvent);
        TestLog.Step("根据被撞棋类型尝试创建 AbilityTrigger。");
        TestLog.Expect("普通棋 Type=Normal，因此 triggered=false，abilityEvent=null。");
        TestLog.Actual("是否触发能力", triggered);
        TestLog.Actual("能力事件", TestLog.Event(abilityEvent));

        Assert.IsTrue(collided);
        Assert.AreEqual(target, collisionTarget);
        Assert.IsFalse(triggered);
        Assert.IsNull(abilityEvent);
        TestLog.Pass("普通棋被撞不会触发能力。");
    }

    [Test]
    public void CollisionWithScorePiece_CreatesAbilityTrigger()
    {
        TestLog.Start(nameof(CollisionWithScorePiece_CreatesAbilityTrigger), "验证得分棋被撞时会创建 AbilityTrigger 事件。");
        var board = MakeBoard3x3();
        var mover = new Piece { Type = PieceType.Normal };
        var target = new Piece { Type = PieceType.Score };
        board.PlacePiece(mover, new Hex(0, -1));
        board.PlacePiece(target, new Hex(0, 1));
        TestLog.Step("普通棋作为移动来源，得分棋作为被撞目标。");
        TestLog.Board(board);

        bool collided = TryGravityMoveAndCreateCollision(board, mover, 0, out var collisionTarget);
        TestLog.Actual("是否碰撞", collided);
        TestLog.Actual("碰撞目标", TestLog.Piece(collisionTarget));
        bool triggered = TryCreateAbilityTrigger(collisionTarget, 0, mover, out var abilityEvent);
        TestLog.Step("被撞目标 Type=Score，创建 AbilityTrigger。");
        TestLog.Expect("事件类型 AbilityTrigger，target 为得分棋 ID，source 为移动棋 ID，方向 D0。");
        TestLog.Actual("是否触发能力", triggered);
        TestLog.Actual("能力事件", TestLog.Event(abilityEvent));

        Assert.IsTrue(collided);
        Assert.IsTrue(triggered);
        Assert.AreEqual(GameEventType.AbilityTrigger, abilityEvent.Type);
        Assert.AreEqual(target.ID, abilityEvent.TargetPieceId);
        Assert.AreEqual(mover.ID, abilityEvent.SourcePieceId);
        TestLog.Pass("得分棋被撞会进入能力触发流程。");
    }

    [Test]
    public void GravityMoveWithoutActualMovement_CreatesNoCollision()
    {
        TestLog.Start(nameof(GravityMoveWithoutActualMovement_CreatesNoCollision), "验证没有实际位移时不产生碰撞。");
        var board = MakeBoard3x3();
        var mover = new Piece { Type = PieceType.Normal };
        var target = new Piece { Type = PieceType.Score };
        board.PlacePiece(mover, new Hex(0, 1));
        board.PlacePiece(target, new Hex(1, 1));
        TestLog.Step("移动棋已在 D0 底部边缘，旁边有得分棋但不是正前方碰撞结果。");
        TestLog.Board(board);

        bool collided = TryGravityMoveAndCreateCollision(board, mover, 0, out var collisionTarget);
        TestLog.Step("尝试沿 D0 移动。因为目标等于起点，规则应直接返回不碰撞。");
        TestLog.Expect("collided=false，collisionTarget=null。");
        TestLog.Actual("是否碰撞", collided);
        TestLog.Actual("碰撞目标", TestLog.Piece(collisionTarget));

        Assert.IsFalse(collided);
        Assert.IsNull(collisionTarget);
        TestLog.Pass("无实际位移不会产生碰撞。");
    }
}
