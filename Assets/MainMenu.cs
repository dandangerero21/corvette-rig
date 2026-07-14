using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenu : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject settingsPanel;

    [Header("Settings UI")]
    public Slider musicSlider;
    public Slider sfxSlider;

    private const string MUSIC_KEY = "MusicVolume";
    private const string SFX_KEY = "SFXVolume";

    void Start()
    {
        // Always open on main panel
        ShowMain();

        // Load saved volume settings
        if (musicSlider != null)
            musicSlider.value = PlayerPrefs.GetFloat(MUSIC_KEY, 1f);
        if (sfxSlider != null)
            sfxSlider.value = PlayerPrefs.GetFloat(SFX_KEY, 1f);
    }

    // ── Button callbacks ──────────────────────────────────

    public void OnPlay()
    {
        // Replace "GameScene" with the exact name of your racing scene
        SceneManager.LoadScene("GameScene");
    }

    public void OnSettings()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
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

    // ── Settings callbacks ────────────────────────────────

    public void OnMusicChanged(float value)
    {
        PlayerPrefs.SetFloat(MUSIC_KEY, value);
        // Hook up to AudioMixer if you have one:
        // audioMixer.SetFloat("MusicVolume", Mathf.Log10(value) * 20);
    }

    public void OnSFXChanged(float value)
    {
        PlayerPrefs.SetFloat(SFX_KEY, value);
    }

    // ── Helpers ───────────────────────────────────────────

    void ShowMain()
    {
        if (mainPanel != null) mainPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }
}
