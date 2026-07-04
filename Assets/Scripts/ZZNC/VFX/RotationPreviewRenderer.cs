using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 旋转预览增强渲染器。替换 TempPlaytestController 里的简单 dot 方案，提供：
///   1. Ghost 棋子：落点处显示半透明同类型精灵
///   2. 路径虚线：当前位置到落点之间三个小点
///   3. 碰撞目标橙色光圈：被撞棋子的位置叠加橙色圆
/// 调用顺序：Setup() → 每次预览 Refresh() → 清除 Clear()
/// </summary>
public class RotationPreviewRenderer : MonoBehaviour
{
    [Header("Ghost 棋子")]
    [SerializeField] private Color ghostColor = new Color(0.45f, 0.82f, 1f, 0.38f);
    [SerializeField, Range(0.5f, 1.5f)] private float ghostScaleMultiplier = 1f;

    [Header("路径虚线点")]
    [SerializeField] private Color trailColor = new Color(0.6f, 0.9f, 1f, 0.55f);
    [SerializeField, Range(0.1f, 0.6f)] private float trailDotScale = 0.28f;
    [SerializeField, Range(1, 5)] private int trailDotCount = 3;

    [Header("碰撞目标描边")]
    [SerializeField] private Color hitRingColor = new Color(1f, 0.52f, 0.08f, 0.55f);
    [SerializeField, Range(1f, 2.5f)] private float hitRingScale = 1.45f;

    // ── 运行时引用，由 Setup() 注入 ────────────────────────────────
    private GameObject _dotPrefab;
    private float _pieceScale;
    private Transform _root;

    private readonly List<GameObject> _objects = new List<GameObject>();
    // 需要在 Clear 时重置颜色的 SpriteRenderer（碰撞目标描边不修改原棋子，改为覆盖物）

    /// <summary>
    /// BuildLayout 完成后调用一次，注入依赖。
    /// dotPrefab：现有 previewDotPrefab（带 SpriteRenderer 的圆点 prefab）
    /// pieceScale：棋子 GameObject 的 localScale 大小（用于 ghost 对齐）
    /// root：特效 Transform 父节点
    /// </summary>
    public void Setup(GameObject dotPrefab, float pieceScale, Transform root)
    {
        _dotPrefab = dotPrefab;
        _pieceScale = pieceScale;
        _root = root;
    }

    /// <summary>
    /// 旋转预览刷新时调用。
    /// hexToWorld：把 Hex 坐标换算为世界坐标的委托
    /// getSprite：根据 PieceType 获取精灵的委托
    /// </summary>
    public void Refresh(
        PreviewResult preview,
        Board board,
        int gravityDir,
        Func<Hex, Vector3> hexToWorld,
        Func<PieceType, Sprite> getSprite)
    {
        Clear();
        if (_root == null) return;

        // 被撞棋子集合（去重，避免多个撞击者对同一目标重复绘制）
        var hitSet = new HashSet<int>(preview.HitTargetIds);

        foreach (var kv in preview.FinalPositions)
        {
            int pieceId = kv.Key;
            Hex finalPos = kv.Value;

            var piece = board.GetPieceById(pieceId);
            if (piece == null) continue;

            Hex fromPos = piece.Position;
            // 原地不动的棋子不显示预览
            if (fromPos == finalPos) continue;

            Vector3 fromWorld = hexToWorld(fromPos);
            Vector3 toWorld = hexToWorld(finalPos);

            // ── 1. Ghost 棋子（落点半透明精灵）────────────────────
            var ghost = CreateGhost(getSprite(piece.Type), toWorld);
            if (ghost != null) _objects.Add(ghost);

            // ── 2. 路径虚线点 ─────────────────────────────────────
            for (int i = 1; i <= trailDotCount; i++)
            {
                float t = i / (float)(trailDotCount + 1);
                var dot = CreateTrailDot(Vector3.Lerp(fromWorld, toWorld, t));
                if (dot != null) _objects.Add(dot);
            }
        }

        // ── 3. 碰撞目标橙色光圈 ───────────────────────────────────
        foreach (int targetId in hitSet)
        {
            var targetPiece = board.GetPieceById(targetId);
            if (targetPiece == null) continue;
            var ring = CreateHitRing(hexToWorld(targetPiece.Position));
            if (ring != null) _objects.Add(ring);
        }
    }

    /// <summary>销毁本次预览产生的所有临时对象。</summary>
    public void Clear()
    {
        foreach (var obj in _objects)
        {
            if (obj != null) Destroy(obj);
        }
        _objects.Clear();
    }

    // ── 内部创建方法 ───────────────────────────────────────────────

    private GameObject CreateGhost(Sprite sprite, Vector3 worldPos)
    {
        if (sprite == null) return null;

        var go = new GameObject("Preview_Ghost");
        go.transform.SetParent(_root);
        go.transform.position = worldPos + new Vector3(0f, 0f, -0.08f);
        go.transform.localScale = Vector3.one * _pieceScale * ghostScaleMultiplier;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = ghostColor;
        sr.sortingOrder = 1;
        return go;
    }

    private GameObject CreateTrailDot(Vector3 worldPos)
    {
        if (_dotPrefab == null) return null;

        var go = Instantiate(_dotPrefab, worldPos + new Vector3(0f, 0f, -0.09f), Quaternion.identity, _root);
        go.name = "Preview_Trail";
        go.transform.localScale = Vector3.one * trailDotScale;

        if (go.TryGetComponent<SpriteRenderer>(out var sr))
            sr.color = trailColor;

        return go;
    }

    private GameObject CreateHitRing(Vector3 worldPos)
    {
        if (_dotPrefab == null) return null;

        var go = Instantiate(_dotPrefab, worldPos + new Vector3(0f, 0f, -0.05f), Quaternion.identity, _root);
        go.name = "Preview_HitRing";
        go.transform.localScale = Vector3.one * _pieceScale * hitRingScale;

        if (go.TryGetComponent<SpriteRenderer>(out var sr))
            sr.color = hitRingColor;

        return go;
    }

    private void OnDestroy() => Clear();
}
