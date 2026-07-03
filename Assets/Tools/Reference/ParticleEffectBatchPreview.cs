using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 粒子特效批量预览 - 场景版本
/// 在场景中以网格方式显示特效，支持批次切换和播放控制
/// </summary>
public class ParticleEffectBatchPreview : MonoBehaviour
{
    [Header("特效配置")]
    [SerializeField]
    [Tooltip("特效文件夹路径（相对于 Assets，勿需添加 Assets/ 前缀）")]
    private string m_FolderPath = "Effects";

    [Header("网格参数")]
    [SerializeField]
    [Tooltip("网格列数")]
    private int m_GridColumns = 3;

    [SerializeField]
    [Tooltip("网格行数")]
    private int m_GridRows = 3;

    [SerializeField]
    [Tooltip("格子间距")]
    private float m_CellSpacing = 2f;

    [SerializeField]
    [Tooltip("特效最大尺寸")]
    private float m_MaxEffectSize = 1.5f;

    private List<GameObject> m_EffectPrefabs = new List<GameObject>();
    private List<GameObject> m_CurrentBatchInstances = new List<GameObject>();
    private List<ParticleSystem> m_AllParticleSystems = new List<ParticleSystem>();

    private int m_CurrentBatch = 0;
    private bool m_IsPlaying = true;
    private GameObject m_ContainerRoot;
    private string m_LastFolderPath;

    public int CurrentBatch => m_CurrentBatch;
    public int TotalBatches => m_EffectPrefabs.Count == 0 ? 0 : Mathf.CeilToInt((float)m_EffectPrefabs.Count / (m_GridColumns * m_GridRows));
    public int TotalEffects => m_EffectPrefabs.Count;
    public int ParticleSystemCount => m_AllParticleSystems.Count;
    public bool IsPlaying => m_IsPlaying;
    public string FolderPath => m_FolderPath;

    private void Start()
    {
        m_LastFolderPath = m_FolderPath;
        LoadEffectPrefabs();
        CreateContainerRoot();
        DisplayBatch(0);

        Debug.Log($"[{nameof(ParticleEffectBatchPreview)}] 特效预览已启动，加载 {m_EffectPrefabs.Count} 个特效");
    }

    private void Update()
    {
        HandleInput();
        DetectFolderPathChange();
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            PreviousBatch();

        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            NextBatch();

        if (Input.GetKeyDown(KeyCode.Space))
            TogglePlayPause();

        if (Input.GetKeyDown(KeyCode.R))
            RestartAllParticles();
    }

    private void LoadEffectPrefabs()
    {
        m_EffectPrefabs.Clear();

#if UNITY_EDITOR
        string searchPath = m_FolderPath.StartsWith("Assets/") ? m_FolderPath : "Assets/" + m_FolderPath;
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { searchPath });

        foreach (string guid in guids)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            if (prefab != null && prefab.GetComponentInChildren<ParticleSystem>() != null)
            {
                m_EffectPrefabs.Add(prefab);
            }
        }

        if (m_EffectPrefabs.Count == 0)
        {
            Debug.LogWarning($"[{nameof(ParticleEffectBatchPreview)}] 未找到特效，请检查路径: {searchPath}");
        }
#else
        GameObject[] prefabs = Resources.LoadAll<GameObject>(m_FolderPath);
        foreach (var prefab in prefabs)
        {
            if (prefab.GetComponentInChildren<ParticleSystem>() != null)
            {
                m_EffectPrefabs.Add(prefab);
            }
        }
#endif

        Debug.Log($"[{nameof(ParticleEffectBatchPreview)}] 加载特效完成，共 {m_EffectPrefabs.Count} 个");
    }

    private void CreateContainerRoot()
    {
        m_ContainerRoot = new GameObject("ParticleEffectBatchRoot");
        m_ContainerRoot.transform.SetParent(transform, false);
    }

    private void DisplayBatch(int batchIndex)
    {
        ClearCurrentBatch();

        if (m_EffectPrefabs.Count == 0)
        {
            Debug.LogWarning($"[{nameof(ParticleEffectBatchPreview)}] 没有特效预制体可显示");
            return;
        }

        m_CurrentBatch = Mathf.Clamp(batchIndex, 0, TotalBatches - 1);

        int batchSize = m_GridColumns * m_GridRows;
        int startIndex = m_CurrentBatch * batchSize;
        int endIndex = Mathf.Min(startIndex + batchSize, m_EffectPrefabs.Count);

        float cellSize = m_MaxEffectSize + m_CellSpacing;
        float gridWidth = m_GridColumns * cellSize;
        float gridHeight = m_GridRows * cellSize;

        int localIndex = 0;
        for (int i = startIndex; i < endIndex; i++)
        {
            int row = localIndex / m_GridColumns;
            int col = localIndex % m_GridColumns;

            float x = col * cellSize - gridWidth / 2 + cellSize / 2;
            float z = row * cellSize - gridHeight / 2 + cellSize / 2;
            Vector3 cellPos = new Vector3(x, 0, z);

            GameObject cellObj = new GameObject($"Cell_{row}_{col}");
            cellObj.transform.SetParent(m_ContainerRoot.transform, false);
            cellObj.transform.localPosition = cellPos;

            GameObject effectInstance = Instantiate(m_EffectPrefabs[i]);
            effectInstance.name = m_EffectPrefabs[i].name;
            effectInstance.transform.SetParent(cellObj.transform, false);
            effectInstance.transform.localPosition = Vector3.zero;
            effectInstance.transform.localRotation = Quaternion.identity;

            ScaleEffectToBounds(effectInstance);

            m_CurrentBatchInstances.Add(effectInstance);

            var particles = effectInstance.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particles)
            {
                m_AllParticleSystems.Add(ps);
            }

            localIndex++;
        }

        PlayAllParticles();

        Debug.Log($"[{nameof(ParticleEffectBatchPreview)}] 显示批次 {m_CurrentBatch + 1}，共 {m_CurrentBatchInstances.Count} 个特效");
    }

    private void ClearCurrentBatch()
    {
        if (m_ContainerRoot != null)
        {
            foreach (Transform child in m_ContainerRoot.transform)
            {
                Destroy(child.gameObject);
            }
        }

        m_CurrentBatchInstances.Clear();
        m_AllParticleSystems.Clear();
    }

    private void ScaleEffectToBounds(GameObject effectObj)
    {
        ParticleSystem[] particles = effectObj.GetComponentsInChildren<ParticleSystem>();
        if (particles.Length == 0) return;

        float maxSize = 0;
        foreach (var particle in particles)
        {
            ParticleSystemRenderer renderer = particle.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                Bounds bounds = renderer.bounds;
                float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                maxSize = Mathf.Max(maxSize, size);
            }
        }

        if (maxSize > 0)
        {
            float scale = m_MaxEffectSize / maxSize;
            effectObj.transform.localScale = Vector3.one * scale;
        }
    }

    private void PlayAllParticles()
    {
        foreach (var ps in m_AllParticleSystems)
        {
            if (ps != null)
            {
                ps.Stop();
                ps.Play();
            }
        }
        m_IsPlaying = true;
    }

    private void PauseAllParticles()
    {
        foreach (var ps in m_AllParticleSystems)
        {
            if (ps != null)
                ps.Pause();
        }
        m_IsPlaying = false;
    }

    private void DetectFolderPathChange()
    {
        if (m_FolderPath != m_LastFolderPath)
        {
            m_LastFolderPath = m_FolderPath;
            RefreshEffects();
        }
    }

    public void RefreshEffects()
    {
        LoadEffectPrefabs();
        DisplayBatch(0);
        Debug.Log($"[{nameof(ParticleEffectBatchPreview)}] 已刷新资源，路径: {m_FolderPath}，共 {m_EffectPrefabs.Count} 个特效");
    }

    public void PreviousBatch()
    {
        if (m_CurrentBatch > 0)
            DisplayBatch(m_CurrentBatch - 1);
    }

    public void NextBatch()
    {
        if (m_CurrentBatch < TotalBatches - 1)
            DisplayBatch(m_CurrentBatch + 1);
    }

    public void TogglePlayPause()
    {
        if (m_IsPlaying)
        {
            PauseAllParticles();
            Debug.Log($"[{nameof(ParticleEffectBatchPreview)}] 已暂停");
        }
        else
        {
            PlayAllParticles();
            Debug.Log($"[{nameof(ParticleEffectBatchPreview)}] 已播放");
        }
    }

    public void RestartAllParticles()
    {
        foreach (var ps in m_AllParticleSystems)
        {
            if (ps != null)
            {
                ps.Stop();
                ps.Play();
            }
        }
        m_IsPlaying = true;
        Debug.Log($"[{nameof(ParticleEffectBatchPreview)}] 已重启所有粒子系统");
    }
}
