using NUnit.Framework;

public class HexTests
{
    [Test]
    public void Neighbor_D0_IsDown()
    {
        TestLog.Start(nameof(Neighbor_D0_IsDown), "验证 D0 是正下方向。");
        var h = new Hex(0, 0);
        var actual = h.Neighbor(0);
        TestLog.Step("从 (0,0) 取 D0 邻居。");
        TestLog.Expect("D0 邻居应为 (0,1)。");
        TestLog.Actual("Neighbor(D0)", actual);
        Assert.AreEqual(new Hex(0, 1), actual);
        TestLog.Pass("D0 方向定义正确。");
    }

    [Test]
    public void Neighbor_D3_IsUp()
    {
        TestLog.Start(nameof(Neighbor_D3_IsUp), "验证 D3 是正上方向。");
        var h = new Hex(2, 2);
        var actual = h.Neighbor(3);
        TestLog.Step("从 (2,2) 取 D3 邻居。");
        TestLog.Expect("D3 邻居应为 (2,1)。");
        TestLog.Actual("Neighbor(D3)", actual);
        Assert.AreEqual(new Hex(2, 1), actual);
        TestLog.Pass("D3 方向定义正确。");
    }

    [Test]
    public void Distance_Same()
    {
        TestLog.Start(nameof(Distance_Same), "验证相同坐标的六边形距离为 0。");
        var from = new Hex(1, 1);
        var to = new Hex(1, 1);
        var distance = from.Distance(to);
        TestLog.Step("计算 (1,1) 到 (1,1) 的距离。");
        TestLog.Expect("距离为 0。");
        TestLog.Actual("距离", distance);
        Assert.AreEqual(0, distance);
        TestLog.Pass("相同坐标距离正确。");
    }

    [Test]
    public void Distance_OneStep()
    {
        TestLog.Start(nameof(Distance_OneStep), "验证相邻格距离为 1。");
        var from = new Hex(0, 0);
        var to = new Hex(0, 1);
        var distance = from.Distance(to);
        TestLog.Step("计算 (0,0) 到 D0 相邻格 (0,1) 的距离。");
        TestLog.Expect("距离为 1。");
        TestLog.Actual("距离", distance);
        Assert.AreEqual(1, distance);
        TestLog.Pass("相邻格距离正确。");
    }

    [Test]
    public void Distance_TwoSteps()
    {
        TestLog.Start(nameof(Distance_TwoSteps), "验证两步六边形距离计算。");
        var from = new Hex(0, 0);
        var to = new Hex(1, 1);
        var distance = from.Distance(to);
        TestLog.Step("计算 (0,0) 到 (1,1) 的距离。");
        TestLog.Expect("距离为 2。");
        TestLog.Actual("距离", distance);
        Assert.AreEqual(2, distance);
        TestLog.Pass("两步距离正确。");
    }

    [Test]
    public void OrientationToGravityDir_Zero_IsD0()
    {
        TestLog.Start(nameof(OrientationToGravityDir_Zero_IsD0), "验证棋盘朝向 0 时重力方向为 D0。");
        var dir = Hex.OrientationToGravityDir(0);
        TestLog.Expect("Orientation=0 -> D0。");
        TestLog.Actual("重力方向", "D" + dir);
        Assert.AreEqual(0, dir);
        TestLog.Pass("朝向 0 的重力映射正确。");
    }

    [Test]
    public void OrientationToGravityDir_One_IsD5()
    {
        TestLog.Start(nameof(OrientationToGravityDir_One_IsD5), "验证棋盘顺时针转 1 档时，逻辑重力方向逆时针退到 D5。");
        var dir = Hex.OrientationToGravityDir(1);
        TestLog.Expect("Orientation=1 -> D5。");
        TestLog.Actual("重力方向", "D" + dir);
        Assert.AreEqual(5, dir);
        TestLog.Pass("朝向 1 的重力映射正确。");
    }

    [Test]
    public void RotateDir_Clockwise()
    {
        TestLog.Start(nameof(RotateDir_Clockwise), "验证方向顺时针旋转 1 档。");
        var dir = Hex.RotateDir(0, 1);
        TestLog.Expect("D0 顺时针 1 档为 D1。");
        TestLog.Actual("旋转结果", "D" + dir);
        Assert.AreEqual(1, dir);
        TestLog.Pass("顺时针旋转正确。");
    }

    [Test]
    public void RotateDir_Wraps()
    {
        TestLog.Start(nameof(RotateDir_Wraps), "验证方向旋转超过 D5 后回绕。");
        var dir = Hex.RotateDir(5, 1);
        TestLog.Expect("D5 顺时针 1 档回到 D0。");
        TestLog.Actual("旋转结果", "D" + dir);
        Assert.AreEqual(0, dir);
        TestLog.Pass("方向回绕正确。");
    }

    [Test]
    public void Opposite_D0_IsD3()
    {
        TestLog.Start(nameof(Opposite_D0_IsD3), "验证 D0 的反方向是 D3。");
        var dir = Hex.Opposite(0);
        TestLog.Expect("Opposite(D0)=D3。");
        TestLog.Actual("反方向", "D" + dir);
        Assert.AreEqual(3, dir);
        TestLog.Pass("反方向计算正确。");
    }

    [Test]
    public void SixDirectionsAreUnique()
    {
        TestLog.Start(nameof(SixDirectionsAreUnique), "验证六个方向向量互不重复。");
        var set = new System.Collections.Generic.HashSet<Hex>();
        for (int i = 0; i < 6; i++)
        {
            set.Add(Hex.Directions[i]);
            TestLog.State("D" + i, Hex.Directions[i]);
        }
        TestLog.Expect("去重后数量为 6。");
        TestLog.Actual("唯一方向数", set.Count);
        Assert.AreEqual(6, set.Count);
        TestLog.Pass("六方向定义互不重复。");
    }
}
