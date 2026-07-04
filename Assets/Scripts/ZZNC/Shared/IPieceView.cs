using UnityEngine;

public interface IPieceView
{
    float MoveTo(Vector3 worldPos);
    float PlayHitShake();
    float PlayAbilityFX();
    float PlaySpawn();
    float PlayRemove();
}
