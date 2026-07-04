using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 挂在临时 GameObject 上，GO 销毁时自动 Destroy 所有注册的动态材质，
/// 防止 MissingReferenceException。
/// </summary>
public class MaterialAutoDestroy : MonoBehaviour
{
    private readonly List<Material> _materials = new List<Material>();

    public void Track(Material mat)
    {
        if (mat != null) _materials.Add(mat);
    }

    private void OnDestroy()
    {
        foreach (var mat in _materials)
            if (mat != null) Destroy(mat);
        _materials.Clear();
    }
}
