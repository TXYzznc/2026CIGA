using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ZZNCModalUIInstaller
{
    private const string CanvasName = "ZZNCModalOverlayCanvas";
    private const string ControllerName = "ZZNCTempProgramBPlaytest";

    [MenuItem("Tools/ZZNC/Install Modal UI")]
    public static void Install()
    {
        var canvas = EnsureCanvas();
        var mainMenu = BuildMainMenu(canvas.transform);
        var settlement = BuildSettlement(canvas.transform);
        var threeChoice = BuildThreeChoice(canvas.transform);

        var controller = GameObject.Find(ControllerName)?.GetComponent<TempPlaytestController>();
        if (controller != null)
        {
            SetPrivateField(controller, "mainMenuView", mainMenu);
            SetPrivateField(controller, "settlementView", settlement);
            SetPrivateField(controller, "threeChoiceService", threeChoice);
            SetPrivateField(controller, "autoStartGame", false);
            EditorUtility.SetDirty(controller);
        }
        else
        {
            Debug.LogWarning($"[ZZNCModalUIInstaller] Cannot find {ControllerName}; UI was created but not bound.");
        }

        mainMenu.Hide();
        settlement.Hide();
        threeChoice.Hide();
        EditorUtility.SetDirty(canvas);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[ZZNCModalUIInstaller] Modal canvas, main menu, settlement UI, and three-choice UI installed.");
    }

    private static Canvas EnsureCanvas()
    {
        var go = GameObject.Find(CanvasName);
        if (go == null)
            go = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static MainMenuView BuildMainMenu(Transform parent)
    {
        var root = RecreateRect(parent, "MainMenuView");
        Stretch(root);

        var dim = AddImage(root.gameObject, new Color(0f, 0f, 0f, 0.58f));
        dim.raycastTarget = true;

        var panel = CreateRect(root, "Panel");
        Size(panel, 560f, 520f);
        Center(panel, 0f, 0f);
        AddImage(panel.gameObject, new Color(0.08f, 0.095f, 0.11f, 0.94f));

        CreateText(panel, "Title", "HEX CHAIN", 64, FontStyles.Bold, new Vector2(0f, 155f), new Vector2(480f, 90f));
        CreateText(panel, "Subtitle", "六边形重力连锁", 28, FontStyles.Normal, new Vector2(0f, 92f), new Vector2(480f, 54f));

        var start = CreateButton(panel, "StartButton", "开始游戏", new Vector2(0f, -20f), new Vector2(360f, 76f), new Color(0.94f, 0.72f, 0.28f, 1f));
        var settings = CreateButton(panel, "SettingsButton", "设置", new Vector2(0f, -114f), new Vector2(360f, 62f), new Color(0.27f, 0.34f, 0.42f, 1f));
        var quit = CreateButton(panel, "QuitButton", "返回", new Vector2(0f, -190f), new Vector2(360f, 62f), new Color(0.23f, 0.25f, 0.29f, 1f));

        var view = root.gameObject.GetComponent<MainMenuView>() ?? root.gameObject.AddComponent<MainMenuView>();
        var group = root.gameObject.GetComponent<CanvasGroup>() ?? root.gameObject.AddComponent<CanvasGroup>();
        SetPrivateField(view, "canvasGroup", group);
        SetPrivateField(view, "startButton", start);
        SetPrivateField(view, "continueButton", null);
        SetPrivateField(view, "settingsButton", settings);
        SetPrivateField(view, "quitButton", quit);
        EditorUtility.SetDirty(view);
        return view;
    }

    private static SettlementView BuildSettlement(Transform parent)
    {
        var root = RecreateRect(parent, "SettlementView");
        Stretch(root);

        var dim = AddImage(root.gameObject, new Color(0f, 0f, 0f, 0.62f));
        dim.raycastTarget = true;

        var panel = CreateRect(root, "Panel");
        Size(panel, 640f, 520f);
        Center(panel, 0f, 0f);
        AddImage(panel.gameObject, new Color(0.08f, 0.095f, 0.11f, 0.96f));

        var title = CreateText(panel, "TitleText", "Level Clear", 52, FontStyles.Bold, new Vector2(0f, 156f), new Vector2(560f, 78f));
        CreateText(panel, "ScoreLabel", "Score", 24, FontStyles.Normal, new Vector2(0f, 76f), new Vector2(360f, 42f));
        var score = CreateText(panel, "ScoreText", "0", 58, FontStyles.Bold, new Vector2(0f, 20f), new Vector2(400f, 74f));
        var detail = CreateText(panel, "DetailText", "Level 1  Round 1", 24, FontStyles.Normal, new Vector2(0f, -54f), new Vector2(560f, 48f));

        var next = CreateButton(panel, "NextButton", "下一关", new Vector2(-150f, -166f), new Vector2(250f, 68f), new Color(0.94f, 0.72f, 0.28f, 1f));
        var retry = CreateButton(panel, "RetryButton", "重新开始", new Vector2(-150f, -166f), new Vector2(250f, 68f), new Color(0.94f, 0.72f, 0.28f, 1f));
        var menu = CreateButton(panel, "MainMenuButton", "返回主菜单", new Vector2(150f, -166f), new Vector2(250f, 68f), new Color(0.27f, 0.34f, 0.42f, 1f));

        var view = root.gameObject.GetComponent<SettlementView>() ?? root.gameObject.AddComponent<SettlementView>();
        var group = root.gameObject.GetComponent<CanvasGroup>() ?? root.gameObject.AddComponent<CanvasGroup>();
        SetPrivateField(view, "canvasGroup", group);
        SetPrivateField(view, "titleText", title);
        SetPrivateField(view, "scoreText", score);
        SetPrivateField(view, "detailText", detail);
        SetPrivateField(view, "nextButton", next);
        SetPrivateField(view, "retryButton", retry);
        SetPrivateField(view, "mainMenuButton", menu);
        EditorUtility.SetDirty(view);
        return view;
    }

    private static ThreeChoiceView BuildThreeChoice(Transform parent)
    {
        var root = RecreateRect(parent, "ThreeChoiceView");
        Stretch(root);

        var dim = AddImage(root.gameObject, new Color(0f, 0f, 0f, 0.55f));
        dim.raycastTarget = true;

        var panel = CreateRect(root, "Panel");
        Size(panel, 980f, 520f);
        Center(panel, 0f, 0f);
        AddImage(panel.gameObject, new Color(0.08f, 0.095f, 0.11f, 0.96f));

        var title = CreateText(panel, "TitleText", "Choose a Piece", 46, FontStyles.Bold, new Vector2(0f, 198f), new Vector2(860f, 64f));
        var message = CreateText(panel, "MessageText", "", 24, FontStyles.Normal, new Vector2(0f, 148f), new Vector2(860f, 42f));

        var buttons = new Button[3];
        var titleTexts = new TMP_Text[3];
        var descriptionTexts = new TMP_Text[3];
        var iconImages = new Image[3];
        var xs = new[] { -300f, 0f, 300f };

        for (var i = 0; i < 3; i++)
        {
            var button = CreateChoiceButton(panel, $"Option{i + 1}", new Vector2(xs[i], -40f));
            buttons[i] = button;
            iconImages[i] = button.transform.Find("Icon")?.GetComponent<Image>();
            titleTexts[i] = button.transform.Find("TitleText")?.GetComponent<TMP_Text>();
            descriptionTexts[i] = button.transform.Find("DescriptionText")?.GetComponent<TMP_Text>();
        }

        var view = root.gameObject.GetComponent<ThreeChoiceView>() ?? root.gameObject.AddComponent<ThreeChoiceView>();
        var group = root.gameObject.GetComponent<CanvasGroup>() ?? root.gameObject.AddComponent<CanvasGroup>();
        SetPrivateField(view, "canvasGroup", group);
        SetPrivateField(view, "titleText", title);
        SetPrivateField(view, "messageText", message);
        SetPrivateField(view, "optionButtons", buttons);
        SetPrivateField(view, "optionTitleTexts", titleTexts);
        SetPrivateField(view, "optionDescriptionTexts", descriptionTexts);
        SetPrivateField(view, "optionIconImages", iconImages);
        EditorUtility.SetDirty(view);
        return view;
    }

    private static RectTransform RecreateRect(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);

        return CreateRect(parent, name);
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void Center(RectTransform rect, float x, float y)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
    }

    private static void Size(RectTransform rect, float w, float h)
    {
        rect.sizeDelta = new Vector2(w, h);
    }

    private static Image AddImage(GameObject go, Color color)
    {
        var image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateText(RectTransform parent, string name, string text, float size, FontStyles style, Vector2 pos, Vector2 dimensions)
    {
        var rect = CreateRect(parent, name);
        Center(rect, pos.x, pos.y);
        Size(rect, dimensions.x, dimensions.y);

        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.94f, 0.96f, 0.98f, 1f);
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        return label;
    }

    private static Button CreateButton(RectTransform parent, string name, string text, Vector2 pos, Vector2 dimensions, Color color)
    {
        var rect = CreateRect(parent, name);
        Center(rect, pos.x, pos.y);
        Size(rect, dimensions.x, dimensions.y);

        var image = AddImage(rect.gameObject, color);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var label = CreateText(rect, "Text", text, 28, FontStyles.Bold, Vector2.zero, dimensions);
        label.color = new Color(0.04f, 0.045f, 0.05f, 1f);
        return button;
    }

    private static Button CreateChoiceButton(RectTransform parent, string name, Vector2 pos)
    {
        var rect = CreateRect(parent, name);
        Center(rect, pos.x, pos.y);
        Size(rect, 260f, 300f);

        var image = AddImage(rect.gameObject, new Color(0.18f, 0.22f, 0.27f, 1f));
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var icon = CreateRect(rect, "Icon");
        Center(icon, 0f, 72f);
        Size(icon, 108f, 108f);
        var iconImage = AddImage(icon.gameObject, new Color(1f, 1f, 1f, 1f));
        iconImage.preserveAspect = true;
        iconImage.enabled = false;

        var title = CreateText(rect, "TitleText", "Piece", 28, FontStyles.Bold, new Vector2(0f, -28f), new Vector2(220f, 48f));
        title.color = new Color(0.96f, 0.82f, 0.42f, 1f);

        var description = CreateText(rect, "DescriptionText", "", 18, FontStyles.Normal, new Vector2(0f, -92f), new Vector2(220f, 78f));
        description.enableWordWrapping = true;
        description.color = new Color(0.78f, 0.84f, 0.9f, 1f);

        return button;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);

        field.SetValue(target, value);
    }
}
