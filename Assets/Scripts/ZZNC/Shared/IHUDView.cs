using UnityEngine;

public interface IHUDView
{
    void ShowScorePop(int scoreDelta, int combo, Vector3 worldPos);
}
