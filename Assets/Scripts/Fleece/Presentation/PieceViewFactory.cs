using UnityEngine;

public sealed class PieceViewFactory : MonoBehaviour, IPieceViewFactory
{
    [SerializeField] private BoardView boardView;
    [SerializeField] private PieceView normalPrefab;
    [SerializeField] private PieceView scorePrefab;
    [SerializeField] private PieceView explosionPrefab;
    [SerializeField] private PieceView splitPrefab;

    public IPieceView CreateView(PieceType type, Hex hex)
    {
        var prefab = GetPrefab(type);
        var position = boardView != null ? boardView.HexToWorld(hex) : Vector3.zero;
        var parent = boardView != null ? boardView.PieceRoot : transform;

        PieceView view;
        if (prefab != null)
        {
            view = Instantiate(prefab, position, Quaternion.identity, parent);
        }
        else
        {
            var fallback = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fallback.name = $"{type} Piece";
            fallback.transform.SetParent(parent, false);
            fallback.transform.position = position;
            view = fallback.AddComponent<PieceView>();
            ApplyFallbackColor(fallback.GetComponent<Renderer>(), type);
        }

        view.name = $"{type} Piece";
        view.PlaySpawn();
        return view;
    }

    public void DestroyView(IPieceView view)
    {
        if (view?.Transform != null)
        {
            Destroy(view.Transform.gameObject);
        }
    }

    private PieceView GetPrefab(PieceType type)
    {
        return type switch
        {
            PieceType.Score => scorePrefab,
            PieceType.Explosion => explosionPrefab,
            PieceType.Split => splitPrefab,
            _ => normalPrefab,
        };
    }

    private static void ApplyFallbackColor(Renderer renderer, PieceType type)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.material.color = type switch
        {
            PieceType.Score => new Color(1f, 0.78f, 0.2f),
            PieceType.Explosion => new Color(1f, 0.22f, 0.12f),
            PieceType.Split => new Color(0.2f, 0.8f, 1f),
            _ => new Color(0.55f, 0.6f, 0.65f),
        };
    }
}

