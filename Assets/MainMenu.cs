using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject settingsPanel;

    [Header("Game Scenes")]
    [Tooltip("The circuit/track scene")]
    public string mapSceneName = "nurburgring";

    [Tooltip("The car/vehicle scene to load additively onto the track")]
    public string carSceneName = "scene new";

    [Header("Garage Scene")]
    [Tooltip("The 3D garage environment scene to load additively behind the main menu UI")]
    public string garageSceneName = "Garage Scene";

    [Header("Settings UI")]
    public Slider musicSlider;
    public Slider sfxSlider;
    public AudioSource bgmSource;

    private const string MUSIC_KEY = "MusicVolume";
    private const string SFX_KEY = "SFXVolume";

    void Start()
    {
        ShowMain();

        // Ensure Garage Scene is loaded additively ONLY if not already loaded
        if (!string.IsNullOrEmpty(garageSceneName) && !SceneController.IsSceneLoaded(garageSceneName))
        {
            if (Application.CanStreamedLevelBeLoaded(garageSceneName))
            {
                SceneManager.LoadSceneAsync(garageSceneName, LoadSceneMode.Additive);
            }
        }

        // Load saved volume settings
        float savedMusic = PlayerPrefs.GetFloat(MUSIC_KEY, 1f);
        float savedSFX = PlayerPrefs.GetFloat(SFX_KEY, 1f);

        if (musicSlider != null) musicSlider.value = savedMusic;
        if (sfxSlider != null) sfxSlider.value = savedSFX;
        if (bgmSource != null) bgmSource.volume = savedMusic;
    }

    // ── Panel Navigation ─────────────────────────────────

    public void ShowMain()
    {
        SetPanelActive(mainPanel);
    }

    public void ShowSettings()
    {
        SetPanelActive(settingsPanel);
    }

    private void SetPanelActive(GameObject activePanel)
    {
        if (mainPanel != null) mainPanel.SetActive(activePanel == mainPanel);
        if (settingsPanel != null) settingsPanel.SetActive(activePanel == settingsPanel);
    }

    // ── Action Buttons ────────────────────────────────────

    public void OnPlay()
    {
        if (SceneController.Instance != null)
        {
            SceneController.Instance.LoadGameScenes(mapSceneName, carSceneName);
        }
        else
        {
            // Fallback load map scene first, then additively load car scene
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

    public void OnBack()
    {
        ShowMain();
    }

    public void OnQuit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ── Settings Callbacks ────────────────────────────────

    public void OnMusicChanged(float value)
    {
        PlayerPrefs.SetFloat(MUSIC_KEY, value);
        if (bgmSource != null) bgmSource.volume = value;
    }

    public void OnSFXChanged(float value)
    {
        PlayerPrefs.SetFloat(SFX_KEY, value);
    }
}
