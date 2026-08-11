using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    public static SceneController Instance { get; private set; }

    [Header("Default Game Scenes")]
    public string defaultMapScene = "nurburgring";
    public string defaultCarScene = "scene new";

    [Header("Default Menu Scenes")]
    public string defaultMenuScene = "MainMenu";
    public string defaultGarageScene = "Garage Scene";

    [Header("Audio Settings")]
    public float defaultMusicVolume = 1f;
    public float defaultSFXVolume = 1f;

    private const string MUSIC_KEY = "MusicVolume";
    private const string SFX_KEY = "SFXVolume";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Checks if a scene by name is currently loaded in the scene hierarchy.
    /// </summary>
    public static bool IsSceneLoaded(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name.Equals(sceneName, System.StringComparison.OrdinalIgnoreCase) && scene.isLoaded)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Loads the track map scene as primary, and additively loads the car scene on top.
    /// </summary>
    public void LoadGameScenes(string mapScene = "nurburgring", string carScene = "scene new")
    {
        if (string.IsNullOrEmpty(mapScene)) mapScene = defaultMapScene;
        if (string.IsNullOrEmpty(carScene)) carScene = defaultCarScene;

        AsyncOperation loadMapOp = SceneManager.LoadSceneAsync(mapScene, LoadSceneMode.Single);
        loadMapOp.completed += (op) =>
        {
            if (!string.IsNullOrEmpty(carScene) && !IsSceneLoaded(carScene))
            {
                SceneManager.LoadSceneAsync(carScene, LoadSceneMode.Additive);
            }
        };
    }

    /// <summary>
    /// Loads MainMenu scene as primary, and additively loads Garage Scene on top if not already present.
    /// </summary>
    public void GoToMenuScene(string menuSceneName = "MainMenu", string garageSceneName = "Garage Scene")
    {
        if (string.IsNullOrEmpty(menuSceneName)) menuSceneName = defaultMenuScene;
        if (string.IsNullOrEmpty(garageSceneName)) garageSceneName = defaultGarageScene;

        AsyncOperation loadMenuOp = SceneManager.LoadSceneAsync(menuSceneName, LoadSceneMode.Single);
        loadMenuOp.completed += (op) =>
        {
            if (!string.IsNullOrEmpty(garageSceneName) && !IsSceneLoaded(garageSceneName))
            {
                SceneManager.LoadSceneAsync(garageSceneName, LoadSceneMode.Additive);
            }
        };
    }

    // Backwards compatibility methods
    public void loadGameScene(string map, string car)
    {
        LoadGameScenes(map, car);
    }

    public void goToMenuScene(string scene)
    {
        GoToMenuScene(scene, defaultGarageScene);
    }

    public void LoadTrack(string trackSceneName)
    {
        if (!string.IsNullOrEmpty(trackSceneName))
        {
            SceneManager.LoadScene(trackSceneName);
        }
    }

    public void SetMusicVolume(float volume)
    {
        PlayerPrefs.SetFloat(MUSIC_KEY, volume);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float volume)
    {
        PlayerPrefs.SetFloat(SFX_KEY, volume);
        PlayerPrefs.Save();
    }

    public float GetMusicVolume()
    {
        return PlayerPrefs.GetFloat(MUSIC_KEY, defaultMusicVolume);
    }

    public float GetSFXVolume()
    {
        return PlayerPrefs.GetFloat(SFX_KEY, defaultSFXVolume);
    }

    public void ExitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
