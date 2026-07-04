using UnityEngine;

namespace Ciga2026.Game.Presentation
{
    public sealed class PieceView : MonoBehaviour
    {
        [SerializeField] private float stubAnimationSeconds = 0.1f;

        public float MoveTo(Vector3 worldPosition)
        {
            transform.position = worldPosition;
            return stubAnimationSeconds;
        }

        public float PlayHitShake()
        {
            return stubAnimationSeconds;
        }

        public float PlayAbilityFX()
        {
            return stubAnimationSeconds;
        }

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
}
