using UnityEngine;

public static class WeightedPicker
{
    public static PieceType Pick(PieceWeight[] pool, System.Random random)
    {
        if (pool == null || pool.Length == 0)
        {
            return PieceType.Normal;
        }

        var total = 0;
        foreach (var item in pool)
        {
            total += Mathf.Max(0, item.weight);
        }

        if (total <= 0)
        {
            return pool[0].type;
        }

        var roll = random.Next(0, total);
        foreach (var item in pool)
        {
            roll -= Mathf.Max(0, item.weight);
            if (roll < 0)
            {
                return item.type;
            }
        }

        return pool[pool.Length - 1].type;
    }
}

