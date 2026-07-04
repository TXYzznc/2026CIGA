using System;
using UnityEngine;

public sealed class FakeSmackExecutor : MonoBehaviour, ISmackExecutor
{
    [SerializeField] private int fakeScorePerSmack = 10;

    public void ExecuteSmack(int boardOrientation, SmackRules rules, Action<SmackResult> onRoundStable)
    {
        Debug.Log($"Fake smack resolved. Orientation={boardOrientation}");
        onRoundStable?.Invoke(new SmackResult(fakeScorePerSmack, 1, false));
    }
}

