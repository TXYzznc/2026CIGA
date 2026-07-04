using System;

[Serializable]
public struct Hex : IEquatable<Hex>
{
    public int q;
    public int r;

    public Hex(int q, int r) { this.q = q; this.r = r; }

    // D0~D5 顺时针，D0 = 正下（+r 方向，尖顶六边形）
    public static readonly Hex[] Directions = new Hex[6]
    {
        new Hex( 0, +1),  // D0 下
        new Hex(+1,  0),  // D1 右下
        new Hex(+1, -1),  // D2 右上
        new Hex( 0, -1),  // D3 上
        new Hex(-1,  0),  // D4 左上
        new Hex(-1, +1),  // D5 左下
    };

    public Hex Neighbor(int dir) => this + Directions[((dir % 6) + 6) % 6];

    public int Distance(Hex other)
    {
        int dq = q - other.q;
        int dr = r - other.r;
        int ds = -dq - dr;
        return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(ds)) / 2;
    }

    // Orientation=0 时屏幕下方=D0；棋盘顺时针转1档，重力方向逆时针退1档
    public static int OrientationToGravityDir(int boardOrientation)
        => (6 - (boardOrientation % 6)) % 6;

    public static int RotateDir(int dir, int steps)
        => (((dir + steps) % 6) + 6) % 6;

    public static int RotateDirCCW(int dir, int steps) => RotateDir(dir, -steps);

    public static int Opposite(int dir) => (dir + 3) % 6;

    public static Hex operator +(Hex a, Hex b) => new Hex(a.q + b.q, a.r + b.r);
    public static Hex operator -(Hex a, Hex b) => new Hex(a.q - b.q, a.r - b.r);
    public static bool operator ==(Hex a, Hex b) => a.q == b.q && a.r == b.r;
    public static bool operator !=(Hex a, Hex b) => !(a == b);

    public bool Equals(Hex other) => q == other.q && r == other.r;
    public override bool Equals(object obj) => obj is Hex h && Equals(h);
    public override int GetHashCode() => q * 31 + r;
    public override string ToString() => $"({q},{r})";
}
