using UnityEngine;

public interface IPieceView
{
    /// <summary>直接将 View 拉到指定世界位置（不播动画）。</summary>
    void SnapTo(Vector3 worldPos);
    float MoveTo(Vector3 worldPos);
    float PlayHitShake();
    float PlayAbilityFX();
    float PlaySpawn();
    float PlayRemove();
}
