using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 最小结算验收脚本。挂到场景中任意空 GameObject，按 Play，看 Console。
/// 不依赖 B 的 prefab，使用 Null 实现（动画全跳过）。
/// </summary>
public class SmackDebugger : MonoBehaviour
{
    private SmackResolver _resolver;
    private Board _board;

    private void Start()
    {
        _board = new Board();
        _resolver = gameObject.AddComponent<SmackResolver>();
        _resolver.Init(_board, new NullBoardView(), new NullFactory());

        StartCoroutine(RunAllCases());
    }

    // 顺序执行：等上一个 case 回调完成再跑下一个，避免多协程并发修改 Board
    private IEnumerator RunAllCases()
    {
        yield return StartCoroutine(Case_GravityFall());
        yield return StartCoroutine(Case_ScoreCombo());
        yield return StartCoroutine(Case_ExplosionPush());
        yield return StartCoroutine(Case_SplitBasic());
        Debug.Log("=== 全部 Case 完成 ===");
    }

    // ── 测试用例 ─────────────────────────────────────────────────

    // Case 1：两个普通棋在同一轨道，下面的先落底，上面的紧跟
    private IEnumerator Case_GravityFall()
    {
        Reset();
        var pA = PlaceNormal(new Hex(0, -1));
        var pB = PlaceNormal(new Hex(0,  1));
        Debug.Log("=== Case 1: 重力下落 ===");
        Debug.Log($"初始：A={pA.Position} B={pB.Position}");

        bool done = false;
        _resolver.ExecuteSmack(0, SmackRules.Default, result =>
        {
            Debug.Log($"结果：A={pA.Position} B={pB.Position}");
            Log(pB.Position == new Hex(0, 3), $"B 落到 (0,3)，实际 {pB.Position}");
            Log(pA.Position == new Hex(0, 2), $"A 落到 (0,2)，实际 {pA.Position}");
            Log(result.ScoreGained == 0, "无得分");
            done = true;
        });
        yield return new WaitUntil(() => done);
    }

    // Case 2：普通棋撞得分棋，连击得分
    private IEnumerator Case_ScoreCombo()
    {
        Reset();
        PlaceNormal(new Hex(0, -2));
        Place(new Hex(0, 3), PieceType.Score);
        Debug.Log("=== Case 2: 普通棋撞得分棋 ===");

        bool done = false;
        _resolver.ExecuteSmack(0, SmackRules.Default, result =>
        {
            Log(result.ScoreGained == 100, $"得分应为 100，实际 {result.ScoreGained}");
            Log(result.MaxCombo == 1,      $"连击应为 1，实际 {result.MaxCombo}");
            done = true;
        });
        yield return new WaitUntil(() => done);
    }

    // Case 3：普通棋撞爆炸棋，旁边棋子被推走
    private IEnumerator Case_ExplosionPush()
    {
        Reset();
        PlaceNormal(new Hex(0, -2));
        Place(new Hex(0, 2), PieceType.Explosion);
        var pSide = PlaceNormal(new Hex(1, 2));
        var initPos = pSide.Position;
        Debug.Log("=== Case 3: 爆炸棋推动 ===");

        bool done = false;
        _resolver.ExecuteSmack(0, SmackRules.Default, result =>
        {
            Log(pSide.Position != initPos, $"旁边棋子被推走，初始={initPos} 推后={pSide.Position}");
            Log(result.MaxCombo == 1,      $"连击应为 1，实际 {result.MaxCombo}");
            done = true;
        });
        yield return new WaitUntil(() => done);
    }

    // Case 4：普通棋撞分裂棋，原棋消失，两侧各生成一个分裂棋
    private IEnumerator Case_SplitBasic()
    {
        Reset();
        PlaceNormal(new Hex(0, -2));
        var pSplit = Place(new Hex(0, 2), PieceType.Split);
        int splitId = pSplit.ID;
        Debug.Log("=== Case 4: 分裂棋 ===");

        bool done = false;
        _resolver.ExecuteSmack(0, SmackRules.Default, result =>
        {
            bool originGone = _board.GetPieceById(splitId) == null;
            int splitCount = 0;
            foreach (var p in _board.AllPieces())
                if (p.Type == PieceType.Split) splitCount++;

            Log(originGone,      "原分裂棋已移除");
            Log(splitCount == 2, $"场上分裂棋应为 2，实际 {splitCount}");
            Log(result.MaxCombo == 1, $"连击应为 1，实际 {result.MaxCombo}");
            done = true;
        });
        yield return new WaitUntil(() => done);
    }

    // ── 工具 ────────────────────────────────────────────────────

    private void Reset()
    {
        _board.Clear();
        _board.SetShape(MakeHexagonShape(3));
    }

    private Piece PlaceNormal(Hex pos) => Place(pos, PieceType.Normal);

    private Piece Place(Hex pos, PieceType type)
    {
        var p = new Piece { Type = type, View = new NullPieceView() };
        _board.PlacePiece(p, pos);
        return p;
    }

    private static void Log(bool pass, string msg) =>
        Debug.Log(pass ? $"  <color=green>PASS</color> {msg}" : $"  <color=red>FAIL: {msg}</color>");

    private static IEnumerable<Hex> MakeHexagonShape(int radius)
    {
        var cells = new List<Hex>();
        for (int q = -radius; q <= radius; q++)
        {
            int r1 = Math.Max(-radius, -q - radius);
            int r2 = Math.Min( radius, -q + radius);
            for (int r = r1; r <= r2; r++)
                cells.Add(new Hex(q, r));
        }
        return cells;
    }

    // ── Null 实现 ────────────────────────────────────────────────

    private class NullPieceView : IPieceView
    {
        public float MoveTo(Vector3 worldPos) => 0f;
        public float PlayHitShake()           => 0f;
        public float PlayAbilityFX()          => 0f;
        public float PlaySpawn()              => 0f;
        public float PlayRemove()             => 0f;
    }

    private class NullFactory : IPieceViewFactory
    {
        public IPieceView CreateView(PieceType type, Hex pos) => new NullPieceView();
        public void DestroyView(IPieceView view) { }
    }

    private class NullBoardView : IBoardView
    {
        public Vector3 HexToWorld(Hex hex) => new Vector3(hex.q, 0, hex.r);
    }
}
