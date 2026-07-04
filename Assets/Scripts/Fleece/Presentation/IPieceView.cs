using UnityEngine;

public interface IPieceView
{
    Transform Transform { get; }
    float MoveTo(Vector3 worldPosition);
    float PlayHitShake();
    float PlayAbilityFX();
    float PlaySpawn();
    float PlayRemove();
}

