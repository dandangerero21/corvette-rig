using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        // Toggle pause menu with Escape or P key
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
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
}
