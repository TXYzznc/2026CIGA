using UnityEngine;

public class PlaceholderPieceView : MonoBehaviour, IPieceView
{
    public float MoveTo(Vector3 worldPos)   { transform.position = worldPos; return 0.1f; }
    public float PlayHitShake()             { return 0.1f; }
    public float PlayAbilityFX()            { return 0.1f; }
    public float PlaySpawn()                { return 0.1f; }
    public float PlayRemove()               { Destroy(gameObject); return 0.1f; }
}
