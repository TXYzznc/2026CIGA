using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Catmull-Rom 样条工具类
/// </summary>
public static class CatmullRomSpline
{
    /// <summary>
    /// 计算 Catmull-Rom 插值点
    /// </summary>
    public static Vector3 Evaluate(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f
            * (
                2f * p1
                + (-p0 + p2) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
    }

    /// <summary>
    /// 计算切线方向
    /// </summary>
    public static Vector3 EvaluateTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        return 0.5f
            * (
                (-p0 + p2)
                + (2f * (2f * p0 - 5f * p1 + 4f * p2 - p3)) * t
                + (3f * (-p0 + 3f * p1 - 3f * p2 + p3)) * t2
            );
    }

    /// <summary>
    /// 将控制点列表采样为均匀世界坐标点列表
    /// </summary>
    public static List<(Vector3 pos, Vector3 tangent)> Sample(
        List<Vector3> pts,
        float step,
        bool closed
    )
    {
        var result = new List<(Vector3, Vector3)>();
        if (pts == null || pts.Count < 2)
            return result;

        int count = pts.Count;
        int segCount = closed ? count : count - 1;

        for (int i = 0; i < segCount; i++)
        {
            Vector3 p0 = pts[Mod(i - 1, count)];
            Vector3 p1 = pts[i];
            Vector3 p2 = pts[Mod(i + 1, count)];
            Vector3 p3 = pts[Mod(i + 2, count)];

            // 对于非闭合曲线，端点用镜像点
            if (!closed)
            {
                if (i == 0)
                    p0 = p1 + (p1 - p2);
                if (i == count - 2)
                    p3 = p2 + (p2 - p1);
            }

            int steps = Mathf.Max(1, Mathf.RoundToInt(1f / step));
            for (int j = 0; j < steps; j++)
            {
                float t = j / (float)steps;
                result.Add((Evaluate(p0, p1, p2, p3, t), EvaluateTangent(p0, p1, p2, p3, t)));
            }
        }

        // 加上最后一个点
        if (!closed && pts.Count >= 2)
        {
            int last = count - 1;
            Vector3 p0 = pts[Mod(last - 2, count)];
            Vector3 p1 = pts[last - 1];
            Vector3 p2 = pts[last];
            Vector3 p3 = p2 + (p2 - p1);
            result.Add((Evaluate(p0, p1, p2, p3, 1f), EvaluateTangent(p0, p1, p2, p3, 1f)));
        }

        return result;
    }

    /// <summary>
    /// 在采样点上按间隔均匀取点
    /// </summary>
    public static List<(Vector3 pos, Vector3 tangent)> GetEvenlySpacedPoints(
        List<(Vector3 pos, Vector3 tangent)> samples,
        float spacing
    )
    {
        var result = new List<(Vector3, Vector3)>();
        if (samples == null || samples.Count == 0 || spacing <= 0f)
            return result;

        result.Add(samples[0]);
        float distSinceLastPoint = 0f;

        for (int i = 1; i < samples.Count; i++)
        {
            float segDist = Vector3.Distance(samples[i - 1].pos, samples[i].pos);
            distSinceLastPoint += segDist;

            while (distSinceLastPoint >= spacing)
            {
                float overshoot = distSinceLastPoint - spacing;
                float t = 1f - overshoot / segDist;
                Vector3 pos = Vector3.Lerp(samples[i - 1].pos, samples[i].pos, t);
                Vector3 tan = Vector3
                    .Lerp(samples[i - 1].tangent, samples[i].tangent, t)
                    .normalized;
                result.Add((pos, tan));
                distSinceLastPoint = overshoot;
            }
        }

        return result;
    }

    private static int Mod(int x, int m) => ((x % m) + m) % m;
}
