using UnityEngine;

public sealed class PieceView : MonoBehaviour, IPieceView
{
    [SerializeField] private float stubAnimationSeconds = 0.1f;

    public Transform Transform => transform;

    public float MoveTo(Vector3 worldPosition)
    {
        transform.position = worldPosition;
        return stubAnimationSeconds;
    }

    public float PlayHitShake() => stubAnimationSeconds;

    public float PlayAbilityFX() => stubAnimationSeconds;

    public float PlaySpawn()
    {
        gameObject.SetActive(true);
        return stubAnimationSeconds;
    }

    public float PlayRemove()
    {
        gameObject.SetActive(false);
        return stubAnimationSeconds;
    }
}

