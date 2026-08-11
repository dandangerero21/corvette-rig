using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class PauseMenuUIBuilder : EditorWindow
{
    [MenuItem("Tools/Generate In-Game Pause Menu UI")]
    public static void GeneratePauseUI()
    {
        // Delete existing PauseMenuCanvas if present to prevent duplicates
        GameObject existingCanvas = GameObject.Find("PauseMenuCanvas");
        if (existingCanvas != null)
        {
            Undo.DestroyObjectImmediate(existingCanvas);
        }

        // 1. Ensure EventSystem exists in Scene
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // 2. Create Canvas Root
        GameObject canvasObj = new GameObject("PauseMenuCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // Higher sorting order so pause overlay draws over HUD
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        PauseMenu pauseScript = canvasObj.AddComponent<PauseMenu>();

        // Color Palette
        Color backdropColor = new Color(0.02f, 0.03f, 0.05f, 0.75f);
        Color darkPanelColor = new Color(0.06f, 0.08f, 0.12f, 0.92f);
        Color accentYellow = new Color(0.95f, 0.82f, 0.15f, 1.0f);
        Color textWhite = new Color(0.95f, 0.95f, 0.98f, 1.0f);

        // 3. Main Pause Panel (Centered overlay)
        GameObject pausePanel = createPanel("PausePanel", canvasObj.transform, backdropColor, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        pauseScript.pausePanel = pausePanel;

        // Inner Card Box
        GameObject cardBox = createPanel("CardBox", pausePanel.transform, darkPanelColor, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440, 560));

        createTextObject("PauseTitle", cardBox.transform, "GAME PAUSED", 36, FontStyles.Bold, accentYellow, new Vector2(0, -35), new Vector2(380, 50));

        Button resumeBtn = createButton("ResumeButton", cardBox.transform, "RESUME", accentYellow, Color.black, new Vector2(0, 130), new Vector2(380, 65));
        Button restartBtn = createButton("RestartButton", cardBox.transform, "RESTART RACE", darkPanelColor, textWhite, new Vector2(0, 55), new Vector2(380, 60));
        Button settingsBtn = createButton("SettingsButton", cardBox.transform, "SETTINGS", darkPanelColor, textWhite, new Vector2(0, -15), new Vector2(380, 60));
        Button menuBtn = createButton("MenuButton", cardBox.transform, "MAIN MENU / GARAGE", darkPanelColor, textWhite, new Vector2(0, -85), new Vector2(380, 60));
        Button quitBtn = createButton("QuitButton", cardBox.transform, "EXIT GAME", darkPanelColor, textWhite, new Vector2(0, -155), new Vector2(380, 60));

        UnityEditor.Events.UnityEventTools.AddPersistentListener(resumeBtn.onClick, pauseScript.Resume);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(restartBtn.onClick, pauseScript.RestartRace);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(settingsBtn.onClick, pauseScript.ShowSettings);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(menuBtn.onClick, pauseScript.ReturnToMainMenu);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(quitBtn.onClick, pauseScript.OnQuitGame);

        // 4. Settings Panel
        GameObject settingsPanel = createPanel("SettingsPanel", canvasObj.transform, backdropColor, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        settingsPanel.SetActive(false);
        pauseScript.settingsPanel = settingsPanel;

        GameObject settingsCardBox = createPanel("SettingsCardBox", settingsPanel.transform, darkPanelColor, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440, 480));

        createTextObject("SettingsTitle", settingsCardBox.transform, "SETTINGS", 32, FontStyles.Bold, accentYellow, new Vector2(0, -30), new Vector2(380, 50));

        // Music Slider Block
        createTextObject("MusicLabel", settingsCardBox.transform, "MUSIC VOLUME", 16, FontStyles.Bold, textWhite, new Vector2(0, -95), new Vector2(380, 30));
        Slider musicSlider = createSlider("MusicSlider", settingsCardBox.transform, new Vector2(0, -130), new Vector2(380, 30), new Vector2(0.5f, 1));
        pauseScript.musicSlider = musicSlider;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(musicSlider.onValueChanged, pauseScript.OnMusicChanged);

        // SFX Slider Block
        createTextObject("SFXLabel", settingsCardBox.transform, "ENGINE & SFX VOLUME", 16, FontStyles.Bold, textWhite, new Vector2(0, -200), new Vector2(380, 30));
        Slider sfxSlider = createSlider("SFXSlider", settingsCardBox.transform, new Vector2(0, -235), new Vector2(380, 30), new Vector2(0.5f, 1));
        pauseScript.sfxSlider = sfxSlider;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(sfxSlider.onValueChanged, pauseScript.OnSFXChanged);

        Button settingsBackBtn = createButton("SettingsBackBtn", settingsCardBox.transform, "BACK", darkPanelColor, textWhite, new Vector2(0, -340), new Vector2(380, 60), new Vector2(0.5f, 1));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(settingsBackBtn.onClick, pauseScript.ShowPauseMain);

        // Hide pause menu by default at start
        pausePanel.SetActive(false);

        // Save & Register Undo
        Undo.RegisterCreatedObjectUndo(canvasObj, "Create In-Game Pause Menu UI");
        Selection.activeGameObject = canvasObj;

        EditorUtility.DisplayDialog("Pause Menu Builder", 
            "Successfully generated In-Game Pause Menu UI Canvas!\n\n" +
            "• Press ESC or P during gameplay to open/close.\n" +
            "• Audio mutes on pause automatically.\n" +
            "• Settings labels fixed!", "OK");
    }

    private static GameObject createPanel(string name, Transform parent, Color bgColor, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchorPosition, Vector2 size)
    {
        GameObject panelObj = new GameObject(name);
        panelObj.transform.SetParent(parent, false);

        RectTransform rect = panelObj.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchorPosition;
        if (size != Vector2.zero) rect.sizeDelta = size;

        Image img = panelObj.AddComponent<Image>();
        img.color = bgColor;

        return panelObj;
    }

    private static GameObject createTextObject(string name, Transform parent, string text, float fontSize, FontStyles style, Color color, Vector2 pos = default, Vector2 size = default)
    {
        GameObject txtObj = new GameObject(name);
        txtObj.transform.SetParent(parent, false);

        RectTransform rect = txtObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size != default ? size : new Vector2(400, 50);

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;

        return txtObj;
    }

    private static Button createButton(string name, Transform parent, string text, Color bgColor, Color textColor, Vector2 anchoredPos, Vector2 size, Vector2 anchor = default)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        if (anchor == default) anchor = new Vector2(0.5f, 0.5f);

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);

        RectTransform txtRect = txtObj.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 20;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.Center;

        return btn;
    }

    private static Slider createSlider(string name, Transform parent, Vector2 anchoredPos, Vector2 size, Vector2 anchor = default)
    {
        GameObject sliderObj = new GameObject(name);
        sliderObj.transform.SetParent(parent, false);

        if (anchor == default) anchor = new Vector2(0.5f, 0.5f);

        RectTransform rect = sliderObj.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.sizeDelta = Vector2.zero;

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.color = new Color(0.95f, 0.82f, 0.15f, 1.0f);

        slider.fillRect = fillRect;

        return slider;
    }
}
