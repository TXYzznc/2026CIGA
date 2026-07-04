using System;

namespace Ciga2026.Shared
{
    [Serializable]
    public struct HexCoord
    {
        public int q;
        public int r;

        public HexCoord(int q, int r)
        {
            this.q = q;
            this.r = r;
        }

        public Hex ToHex() => new Hex(q, r);
    }
}
