using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 棋子名称+效果描述悬浮提示。挂在 Tooltip UI 预制体的根节点（需有 Canvas 祖先）。
/// TempPlaytestController 持有此组件引用，检测到棋子时调 Show/Hide。
/// </summary>
public class PieceTooltip : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descText;
    [SerializeField] private Vector2 offset = new Vector2(-120f, -14f);

    private Canvas _canvas;

    private void OnTransformParentChanged()
    {
        _canvas = null;
        EnsureReferences();
    }

    public static PieceTooltip CreateRuntime(Canvas canvas)
    {
        if (canvas == null) return null;

        var root = new GameObject("PieceTooltip_Runtime", typeof(RectTransform), typeof(PieceTooltip));
        root.transform.SetParent(canvas.transform, false);

        var rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(260f, 96f);

        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panelGo.transform.SetParent(root.transform, false);
        var panelRect = (RectTransform)panelGo.transform;
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.zero;
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.sizeDelta = new Vector2(260f, 96f);

        var image = panelGo.GetComponent<Image>();
        image.color = new Color(0.08f, 0.08f, 0.08f, 0.88f);
        image.raycastTarget = false;

        var layout = panelGo.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = panelGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var nameText = CreateText("Name", panelGo.transform, 26f, FontStyles.Bold);
        var descText = CreateText("Desc", panelGo.transform, 18f, FontStyles.Normal);

        var tooltip = root.GetComponent<PieceTooltip>();
        tooltip.panel = panelRect;
        tooltip.nameText = nameText;
        tooltip.descText = descText;
        tooltip._canvas = canvas;
        tooltip.Hide();
        return tooltip;
    }

    private void Awake()
    {
        EnsureReferences();
        Hide();
    }

    /// <param name="pieceName">棋子名称</param>
    /// <param name="desc">效果描述，传 null 或空字符串时隐藏描述行</param>
    public void Show(string pieceName, string desc = null)
    {
        EnsureReferences();
        if (panel == null || nameText == null) return;

        nameText.text = pieceName;

        bool hasDesc = !string.IsNullOrEmpty(desc);
        if (descText != null)
        {
            descText.gameObject.SetActive(hasDesc);
            if (hasDesc) descText.text = desc;
        }

        panel.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (panel == null) return;
        panel.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        EnsureReferences();
        if (panel == null || _canvas == null || !panel.gameObject.activeSelf) return;

        var cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.GetComponent<RectTransform>(),
                Input.mousePosition, cam,
                out var localPoint))
        {
            panel.anchoredPosition = localPoint + offset;
        }
    }

    private void EnsureReferences()
    {
        var currentCanvas = GetComponentInParent<Canvas>();
        if (_canvas != currentCanvas)
            _canvas = currentCanvas;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, FontStyles fontStyle)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(236f, 0f);

        var text = go.GetComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }
}
