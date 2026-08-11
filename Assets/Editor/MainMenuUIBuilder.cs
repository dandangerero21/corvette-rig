using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class MainMenuUIBuilder : EditorWindow
{
    [MenuItem("Tools/Generate Main Menu UI Canvas")]
    public static void GenerateUI()
    {
        // Delete existing MainMenuCanvas if present to prevent duplicates
        GameObject existingCanvas = GameObject.Find("MainMenuCanvas");
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
        GameObject canvasObj = new GameObject("MainMenuCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        MainMenu menuScript = canvasObj.AddComponent<MainMenu>();

        // 3. Add Background Music (emotion engine.mp3)
        AudioClip bgmClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/emotion engine.mp3");
        if (bgmClip != null)
        {
            AudioSource audioSource = canvasObj.AddComponent<AudioSource>();
            audioSource.clip = bgmClip;
            audioSource.loop = true;
            audioSource.playOnAwake = true;
            audioSource.volume = PlayerPrefs.GetFloat("MusicVolume", 1f);
            menuScript.bgmSource = audioSource;
        }

        // Color Palette
        Color darkPanelColor = new Color(0.05f, 0.07f, 0.1f, 0.88f);
        Color accentYellow = new Color(0.95f, 0.82f, 0.15f, 1.0f);
        Color textWhite = new Color(0.95f, 0.95f, 0.98f, 1.0f);

        // 4. Header Title (Cool single word motorsport title)
        GameObject headerObj = createTextObject("TitleHeader", canvasObj.transform, "VELOCITY", 64, FontStyles.Bold, textWhite, new Vector2(80, -60), new Vector2(600, 100), TextAlignmentOptions.Left, new Vector2(0, 1));

        // 5. Corvette Spec Card (Left side)
        GameObject statsPanel = createPanel("CorvetteStatsPanel", canvasObj.transform, darkPanelColor, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(80, 50), new Vector2(340, 260));
        createTextObject("StatsTitle", statsPanel.transform, "CORVETTE STINGRAY C8", 22, FontStyles.Bold, accentYellow, new Vector2(20, -25), new Vector2(300, 40), TextAlignmentOptions.Left, new Vector2(0, 1));
        createTextObject("Stat1", statsPanel.transform, "• Engine: 6.2L LT2 V8", 18, FontStyles.Normal, textWhite, new Vector2(20, -70), new Vector2(300, 35), TextAlignmentOptions.Left, new Vector2(0, 1));
        createTextObject("Stat2", statsPanel.transform, "• Horsepower: 495 HP", 18, FontStyles.Normal, textWhite, new Vector2(20, -110), new Vector2(300, 35), TextAlignmentOptions.Left, new Vector2(0, 1));
        createTextObject("Stat3", statsPanel.transform, "• Top Speed: 194 MPH", 18, FontStyles.Normal, textWhite, new Vector2(20, -150), new Vector2(300, 35), TextAlignmentOptions.Left, new Vector2(0, 1));
        createTextObject("Stat4", statsPanel.transform, "• Drivetrain: RWD", 18, FontStyles.Normal, textWhite, new Vector2(20, -190), new Vector2(300, 35), TextAlignmentOptions.Left, new Vector2(0, 1));

        // 6. MAIN PANEL (Right Side)
        GameObject mainPanel = createPanel("MainPanel", canvasObj.transform, new Color(0, 0, 0, 0), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-100, 0), new Vector2(400, 380));
        menuScript.mainPanel = mainPanel;

        Button playBtn = createButton("PlayButton", mainPanel.transform, "START RACE", accentYellow, Color.black, new Vector2(0, 80), new Vector2(380, 75));
        Button settingsBtn = createButton("SettingsButton", mainPanel.transform, "SETTINGS", darkPanelColor, textWhite, new Vector2(0, -10), new Vector2(380, 65));
        Button quitBtn = createButton("QuitButton", mainPanel.transform, "EXIT GAME", darkPanelColor, textWhite, new Vector2(0, -90), new Vector2(380, 65));

        UnityEditor.Events.UnityEventTools.AddPersistentListener(playBtn.onClick, menuScript.OnPlay);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(settingsBtn.onClick, menuScript.ShowSettings);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(quitBtn.onClick, menuScript.OnQuit);

        // 7. SETTINGS PANEL (Fixed Clean Layout, Text sits clearly ABOVE sliders)
        GameObject settingsPanel = createPanel("SettingsPanel", canvasObj.transform, darkPanelColor, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-100, 0), new Vector2(420, 450));
        settingsPanel.SetActive(false);
        menuScript.settingsPanel = settingsPanel;

        createTextObject("SettingsTitle", settingsPanel.transform, "SETTINGS", 28, FontStyles.Bold, accentYellow, new Vector2(0, -30), new Vector2(380, 45), TextAlignmentOptions.Center, new Vector2(0.5f, 1));

        // Music Slider Block
        createTextObject("MusicLabel", settingsPanel.transform, "MUSIC VOLUME", 16, FontStyles.Bold, textWhite, new Vector2(0, -95), new Vector2(360, 30), TextAlignmentOptions.Center, new Vector2(0.5f, 1));
        Slider musicSlider = createSlider("MusicSlider", settingsPanel.transform, new Vector2(0, -130), new Vector2(360, 30), new Vector2(0.5f, 1));
        menuScript.musicSlider = musicSlider;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(musicSlider.onValueChanged, menuScript.OnMusicChanged);

        // SFX Slider Block
        createTextObject("SFXLabel", settingsPanel.transform, "ENGINE & SFX VOLUME", 16, FontStyles.Bold, textWhite, new Vector2(0, -200), new Vector2(360, 30), TextAlignmentOptions.Center, new Vector2(0.5f, 1));
        Slider sfxSlider = createSlider("SFXSlider", settingsPanel.transform, new Vector2(0, -235), new Vector2(360, 30), new Vector2(0.5f, 1));
        menuScript.sfxSlider = sfxSlider;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(sfxSlider.onValueChanged, menuScript.OnSFXChanged);

        Button settingsBackBtn = createButton("SettingsBackBtn", settingsPanel.transform, "BACK", darkPanelColor, textWhite, new Vector2(0, -330), new Vector2(360, 60), new Vector2(0.5f, 1));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(settingsBackBtn.onClick, menuScript.OnBack);

        // Save & Register Undo
        Undo.RegisterCreatedObjectUndo(canvasObj, "Create Main Menu UI Canvas");
        Selection.activeGameObject = canvasObj;

        EditorUtility.DisplayDialog("Main Menu UI Builder", 
            "Successfully updated Main Menu UI Canvas!\n\n" +
            "• Settings text labels fixed (positioned clearly above sliders).\n" +
            "• Clean typography and background music ready!", "OK");
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
        rect.sizeDelta = size;

        Image img = panelObj.AddComponent<Image>();
        img.color = bgColor;

        return panelObj;
    }

    private static GameObject createTextObject(string name, Transform parent, string text, float fontSize, FontStyles style, Color color, Vector2 pos = default, Vector2 size = default, TextAlignmentOptions alignment = TextAlignmentOptions.Left, Vector2 anchorTop = default)
    {
        GameObject txtObj = new GameObject(name);
        txtObj.transform.SetParent(parent, false);

        if (anchorTop == default) anchorTop = new Vector2(0, 1);

        RectTransform rect = txtObj.AddComponent<RectTransform>();
        rect.anchorMin = anchorTop;
        rect.anchorMax = anchorTop;
        rect.pivot = anchorTop;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size != default ? size : new Vector2(400, 50);

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = alignment;

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
        tmp.fontSize = 22;
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
