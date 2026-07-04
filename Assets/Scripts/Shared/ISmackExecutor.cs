using System;

namespace Ciga2026.Shared
{
    public interface ISmackExecutor
    {
        void ExecuteSmack(int boardOrientation, SmackRules rules, Action<SmackResult> onRoundStable);
    }
}
