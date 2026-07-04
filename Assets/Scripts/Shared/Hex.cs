using System;
using UnityEngine;

namespace Ciga2026.Shared
{
    [Serializable]
    public readonly struct Hex : IEquatable<Hex>
    {
        public readonly int Q;
        public readonly int R;

        public Hex(int q, int r)
        {
            Q = q;
            R = r;
        }

        public static readonly Hex[] Directions =
        {
            new Hex(0, 1),
            new Hex(-1, 1),
            new Hex(-1, 0),
            new Hex(0, -1),
            new Hex(1, -1),
            new Hex(1, 0),
        };

        public Hex Neighbor(int dir)
        {
            var direction = Directions[WrapDirection(dir)];
            return this + direction;
        }

        public int Distance(Hex other)
        {
            var dq = Q - other.Q;
            var dr = R - other.R;
            var ds = -Q - R - (-other.Q - other.R);
            return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(ds)) / 2;
        }

        public static int WrapDirection(int dir) => ((dir % 6) + 6) % 6;

        public bool Equals(Hex other) => Q == other.Q && R == other.R;

        public override bool Equals(object obj) => obj is Hex other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Q, R);

        public override string ToString() => $"({Q}, {R})";

        public static Hex operator +(Hex a, Hex b) => new Hex(a.Q + b.Q, a.R + b.R);

        public static Hex operator -(Hex a, Hex b) => new Hex(a.Q - b.Q, a.R - b.R);

        public static bool operator ==(Hex a, Hex b) => a.Equals(b);

        public static bool operator !=(Hex a, Hex b) => !a.Equals(b);
    }
}
