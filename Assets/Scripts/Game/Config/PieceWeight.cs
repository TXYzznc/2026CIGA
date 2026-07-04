using System;
using Ciga2026.Shared;

namespace Ciga2026.Game.Config
{
    [Serializable]
    public struct PieceWeight
    {
        public PieceType type;
        public int weight;
    }
}
