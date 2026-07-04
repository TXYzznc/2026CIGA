using System.Collections.Generic;
using NUnit.Framework;

public class SplitTests
{
    private enum SpawnResult
    {
        Spawned,
        Skipped,
    }

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

    private static int[] SplitSpawnDirs(int collisionDir)
    {
        return new[]
        {
            Hex.RotateDir(collisionDir, 1),
            Hex.RotateDirCCW(collisionDir, 1),
        };
    }

    private static SpawnResult TrySpawnSplit(Board board, Hex originPos, int spawnDir, out Piece spawned)
    {
        spawned = null;
        var targetPos = originPos.Neighbor(spawnDir);
        Hex finalPos;
        var content = board.GetContent(targetPos);

        if (content == CellContent.Empty)
        {
            finalPos = targetPos;
        }
        else if (content == CellContent.Piece && TryPushChain(board, targetPos, spawnDir, out finalPos))
        {
            finalPos = targetPos;
        }
        else if (!FindNearestEmpty(board, originPos, spawnDir, out finalPos))
        {
            return SpawnResult.Skipped;
        }

        spawned = new Piece { Type = PieceType.Split };
        board.PlacePiece(spawned, finalPos);
        return SpawnResult.Spawned;
    }

    private static bool TryPushChain(Board board, Hex targetPos, int spawnDir, out Hex finalPos)
    {
        finalPos = targetPos;
        var chain = new List<Hex>();
        var cur = targetPos;
        while (board.GetContent(cur) == CellContent.Piece)
        {
            chain.Add(cur);
            cur = cur.Neighbor(spawnDir);
        }

        if (board.GetContent(cur) != CellContent.Empty)
            return false;

        for (int i = chain.Count - 1; i >= 0; i--)
        {
            var piece = board.GetPiece(chain[i]);
            board.MovePiece(piece, chain[i].Neighbor(spawnDir));
        }
        return true;
    }

    private static bool FindNearestEmpty(Board board, Hex originPos, int spawnDir, out Hex finalPos)
    {
        finalPos = default;
        var empties = board.EmptyCells();
        if (empties.Count == 0)
            return false;

        int bestDist = int.MaxValue;
        var candidates = new List<Hex>();
        foreach (var cell in empties)
        {
            int distance = originPos.Distance(cell);
            if (distance < bestDist)
            {
                bestDist = distance;
                candidates.Clear();
            }

            if (distance == bestDist)
                candidates.Add(cell);
        }

        candidates.Sort((a, b) =>
        {
            int angleA = AngleFromDir(originPos, a, spawnDir);
            int angleB = AngleFromDir(originPos, b, spawnDir);
            if (angleA != angleB) return angleA.CompareTo(angleB);
            if (a.q != b.q) return a.q.CompareTo(b.q);
            return a.r.CompareTo(b.r);
        });

        finalPos = candidates[0];
        return true;
    }

    private static int AngleFromDir(Hex origin, Hex target, int baseDir)
    {
        var delta = target - origin;
        int best = 0;
        int bestDot = int.MinValue;
        for (int dir = 0; dir < 6; dir++)
        {
            var dirVector = Hex.Directions[dir];
            int dot = delta.q * dirVector.q + delta.r * dirVector.r;
            if (dot > bestDot)
            {
                bestDot = dot;
                best = dir;
            }
        }
        return ((best - baseDir) % 6 + 6) % 6;
    }

    [Test]
    public void SplitSpawnDirs_CollisionD0_ReturnsD1ThenD5()
    {
        TestLog.Start(nameof(SplitSpawnDirs_CollisionD0_ReturnsD1ThenD5), "验证分裂棋碰撞方向 D0 时，生成方向为 D1 后 D5。");
        var dirs = SplitSpawnDirs(0);
        TestLog.Step("计算 D0 的顺时针 60 度和逆时针 60 度方向。");
        TestLog.Expect("第一个方向 D1，第二个方向 D5。");
        TestLog.Actual("第一个方向", "D" + dirs[0]);
        TestLog.Actual("第二个方向", "D" + dirs[1]);

        Assert.AreEqual(1, dirs[0]);
        Assert.AreEqual(5, dirs[1]);
        TestLog.Pass("分裂生成方向顺序正确。");
    }

    [Test]
    public void SplitTargetEmpty_SpawnsDirectly()
    {
        TestLog.Start(nameof(SplitTargetEmpty_SpawnsDirectly), "验证目标格为空时，分裂棋直接生成到目标格。");
        var board = MakeBoard();
        TestLog.Step("空棋盘，原分裂位置视为 (0,0)，生成方向 D1。");
        TestLog.Board(board);
        var result = TrySpawnSplit(board, new Hex(0, 0), 1, out var spawned);
        TestLog.Expect("D1 目标格 (1,0) 为空，应直接生成 Split 棋。");
        TestLog.Actual("生成结果", result);
        TestLog.Actual("新棋子", TestLog.Piece(spawned));
        TestLog.Board(board);

        Assert.AreEqual(SpawnResult.Spawned, result);
        Assert.IsNotNull(spawned);
        Assert.AreEqual(PieceType.Split, spawned.Type);
        Assert.AreEqual(new Hex(1, 0), spawned.Position);
        TestLog.Pass("空目标格直接生成成功。");
    }

    [Test]
    public void SplitPushChainWithEmptyEnd_ShiftsChainAndSpawnsAtTarget()
    {
        TestLog.Start(nameof(SplitPushChainWithEmptyEnd_ShiftsChainAndSpawnsAtTarget), "验证目标格被占且链末端有空格时，整条链向生成方向挤压 1 格。");
        var board = MakeBoard(3);
        var first = new Piece { Type = PieceType.Normal };
        var second = new Piece { Type = PieceType.Score };
        board.PlacePiece(first, new Hex(1, 0));
        board.PlacePiece(second, new Hex(2, 0));
        TestLog.Step("原位置 (0,0)，D1 目标格 (1,0) 有 first，链上 (2,0) 有 second，(3,0) 为空。");
        TestLog.Board(board);

        var result = TrySpawnSplit(board, new Hex(0, 0), 1, out var spawned);
        TestLog.Expect("链从远端开始移动：second 到 (3,0)，first 到 (2,0)，新 Split 生成在 (1,0)。");
        TestLog.Actual("生成结果", result);
        TestLog.Actual("新棋子", TestLog.Piece(spawned));
        TestLog.Actual("first 当前位置", first.Position);
        TestLog.Actual("second 当前位置", second.Position);
        TestLog.Board(board);

        Assert.AreEqual(SpawnResult.Spawned, result);
        Assert.AreEqual(new Hex(1, 0), spawned.Position);
        Assert.AreEqual(first, board.GetPiece(new Hex(2, 0)));
        Assert.AreEqual(second, board.GetPiece(new Hex(3, 0)));
        TestLog.Pass("分裂挤压链成功，目标格腾空后生成。");
    }

    [Test]
    public void SplitPushChainFailsAtBoardEdge_UsesNearestEmptyCell()
    {
        TestLog.Start(nameof(SplitPushChainFailsAtBoardEdge_UsesNearestEmptyCell), "验证挤压链末端是边界时，改用最近空格生成。");
        var board = MakeBoard(1);
        var blocker = new Piece { Type = PieceType.Normal };
        board.PlacePiece(blocker, new Hex(1, 0));
        TestLog.Step("半径 1 棋盘，原位置 (0,0)，D1 目标格 (1,0) 被占，链下一格 (2,0) 出界。");
        TestLog.Board(board);

        var result = TrySpawnSplit(board, new Hex(0, 0), 1, out var spawned);
        TestLog.Expect("挤压失败后寻找距离原位置最近空格；本场景应选择 (0,0)。");
        TestLog.Actual("生成结果", result);
        TestLog.Actual("新棋子", TestLog.Piece(spawned));
        TestLog.Actual("阻挡棋位置", blocker.Position);
        TestLog.Board(board);

        Assert.AreEqual(SpawnResult.Spawned, result);
        Assert.AreEqual(new Hex(0, 0), spawned.Position);
        Assert.AreEqual(blocker, board.GetPiece(new Hex(1, 0)));
        TestLog.Pass("边界导致挤压失败后，最近空格生成成功。");
    }

    [Test]
    public void SplitWhenNoEmptyCells_SkipsThisSide()
    {
        TestLog.Start(nameof(SplitWhenNoEmptyCells_SkipsThisSide), "验证全场无空格时，本侧分裂生成失败并返回 Skipped。");
        var board = MakeBoard(1);
        foreach (var cell in new List<Hex>(board.EmptyCells()))
            board.PlacePiece(new Piece { Type = PieceType.Normal }, cell);
        TestLog.Step("用普通棋填满半径 1 棋盘所有空格。");
        TestLog.Board(board);

        var result = TrySpawnSplit(board, new Hex(0, 0), 1, out var spawned);
        TestLog.Expect("无目标空格、无最近空格，应 Skipped 且 spawned=null。");
        TestLog.Actual("生成结果", result);
        TestLog.Actual("新棋子", TestLog.Piece(spawned));

        Assert.AreEqual(SpawnResult.Skipped, result);
        Assert.IsNull(spawned);
        TestLog.Pass("全场无空格时本侧生成失败。");
    }
}
