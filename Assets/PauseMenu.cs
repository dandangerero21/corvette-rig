using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }

    [Header("Panels")]
    public GameObject pausePanel;
    public GameObject settingsPanel;

    [Header("Scenes Config")]
    [Tooltip("Target Main Menu scene")]
    public string mainMenuSceneName = "MainMenu";
    [Tooltip("Target 3D Garage scene to load additively behind the main menu")]
    public string garageSceneName = "Garage Scene";

    [Tooltip("Circuit track scene")]
    public string mapSceneName = "nurburgring";
    [Tooltip("Car vehicle scene")]
    public string carSceneName = "scene new";

    [Header("Settings UI")]
    public Slider musicSlider;
    public Slider sfxSlider;

    public bool isPaused { get; private set; } = false;

    private const string MUSIC_KEY = "MusicVolume";
    private const string SFX_KEY = "SFXVolume";

    public static PauseMenu GetOrCreate()
    {
        if (Instance != null) return Instance;

        PauseMenu existing = Object.FindFirstObjectByType<PauseMenu>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject canvasObj = new GameObject("PauseMenuCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();
        Instance = canvasObj.AddComponent<PauseMenu>();
        Instance.BuildRuntimePauseUI();
        return Instance;
    }

    public void TogglePause()
    {
        if (isPaused)
            Resume();
        else
            Pause();
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        Resume();

        // Load saved volume settings
        float savedMusic = PlayerPrefs.GetFloat(MUSIC_KEY, 1f);
        float savedSFX = PlayerPrefs.GetFloat(SFX_KEY, 1f);

        if (musicSlider != null) musicSlider.value = savedMusic;
        if (sfxSlider != null) sfxSlider.value = savedSFX;
    }

    private void Update()
    {
        bool pauseToggled = false;

        // Check Keyboard
        if (Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame)
                pauseToggled = true;
        }

        // Check Gamepad (Options / Start / Select button)
        if (!pauseToggled)
        {
            if (Gamepad.current != null && (Gamepad.current.startButton.wasPressedThisFrame || Gamepad.current.selectButton.wasPressedThisFrame))
            {
                pauseToggled = true;
            }
            else if (Gamepad.all.Count > 0)
            {
                for (int i = 0; i < Gamepad.all.Count; i++)
                {
                    var pad = Gamepad.all[i];
                    if (pad != null && (pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame))
                    {
                        pauseToggled = true;
                        break;
                    }
                }
            }
        }

        // Legacy input fallback
        if (!pauseToggled)
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
                    pauseToggled = true;
            }
            catch { }
        }

        if (pauseToggled)
        {
            if (isPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }

    // ── Pause Controls ───────────────────────────────────

    public void Pause()
    {
        isPaused = true;
        Time.timeScale = 0f; // Freeze game physics & time
        AudioListener.pause = true; // Mute / pause all in-game audio & car engine sounds

        if (pausePanel != null) pausePanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void Resume()
    {
        isPaused = false;
        Time.timeScale = 1f; // Resume normal game physics & time
        AudioListener.pause = false; // Unmute in-game audio

        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void RestartRace()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        
        // Reload both map scene and car scene additively
        if (SceneController.Instance != null)
        {
            SceneController.Instance.LoadGameScenes(mapSceneName, carSceneName);
        }
        else
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(mapSceneName, LoadSceneMode.Single);
            op.completed += (asyncOp) =>
            {
                if (!string.IsNullOrEmpty(carSceneName) && !SceneController.IsSceneLoaded(carSceneName))
                {
                    SceneManager.LoadSceneAsync(carSceneName, LoadSceneMode.Additive);
                }
            };
        }
    }

    public void ShowSettings()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void ShowPauseMain()
    {
        if (pausePanel != null) pausePanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f; // Ensure time is unpaused
        AudioListener.pause = false; // Ensure audio is unmuted

        if (SceneController.Instance != null)
        {
            SceneController.Instance.GoToMenuScene(mainMenuSceneName, garageSceneName);
        }
        else
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(mainMenuSceneName, LoadSceneMode.Single);
            op.completed += (asyncOp) =>
            {
                if (!string.IsNullOrEmpty(garageSceneName) && !SceneController.IsSceneLoaded(garageSceneName))
                {
                    SceneManager.LoadSceneAsync(garageSceneName, LoadSceneMode.Additive);
                }
            };
        }
    }

    public void OnQuitGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ── Settings Callbacks ────────────────────────────────

    public void OnMusicChanged(float value)
    {
        PlayerPrefs.SetFloat(MUSIC_KEY, value);
        if (SceneController.Instance != null)
        {
            SceneController.Instance.SetMusicVolume(value);
        }
    }

    public void OnSFXChanged(float value)
    {
        PlayerPrefs.SetFloat(SFX_KEY, value);
        if (SceneController.Instance != null)
        {
            SceneController.Instance.SetSFXVolume(value);
        }
    }

    public void BuildRuntimePauseUI()
    {
        if (pausePanel != null) return;

        Color backdropColor = new Color(0.02f, 0.03f, 0.05f, 0.85f);
        Color darkPanelColor = new Color(0.06f, 0.08f, 0.12f, 0.95f);
        Color accentYellow = new Color(0.95f, 0.82f, 0.15f, 1.0f);
        Color textWhite = new Color(0.95f, 0.95f, 0.98f, 1.0f);

        // Backdrop
        pausePanel = CreateUIPanel("PausePanel", transform, backdropColor, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Card Box
        GameObject cardBox = CreateUIPanel("CardBox", pausePanel.transform, darkPanelColor, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440, 520));

        CreateUIText("PauseTitle", cardBox.transform, "GAME PAUSED", 36, accentYellow, new Vector2(0, 190), new Vector2(380, 50));

        CreateUIButton("ResumeButton", cardBox.transform, "RESUME", accentYellow, Color.black, new Vector2(0, 95), new Vector2(360, 60), Resume);
        CreateUIButton("RestartButton", cardBox.transform, "RESTART RACE", darkPanelColor, textWhite, new Vector2(0, 20), new Vector2(360, 55), RestartRace);
        CreateUIButton("MenuButton", cardBox.transform, "MAIN MENU", darkPanelColor, textWhite, new Vector2(0, -55), new Vector2(360, 55), ReturnToMainMenu);
        CreateUIButton("QuitButton", cardBox.transform, "EXIT GAME", darkPanelColor, textWhite, new Vector2(0, -130), new Vector2(360, 55), OnQuitGame);

        pausePanel.SetActive(false);
    }

    private GameObject CreateUIPanel(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = obj.AddComponent<Image>();
        img.color = color;
        return obj;
    }

    private void CreateUIText(string name, Transform parent, string text, float fontSize, Color color, Vector2 anchoredPos, Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        TMPro.TextMeshProUGUI tmp = obj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = color;
    }

    private void CreateUIButton(string name, Transform parent, string text, Color btnColor, Color textColor, Vector2 anchoredPos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = obj.AddComponent<Image>();
        img.color = btnColor;

        Button btn = obj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(obj.transform, false);
        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        TMPro.TextMeshProUGUI tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 20;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = textColor;
    }
}
