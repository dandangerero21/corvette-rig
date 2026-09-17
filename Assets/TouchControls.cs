using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class TouchControls : MonoBehaviour
{
    public static TouchControls Instance { get; private set; }

    [Header("Touch Buttons")]
    public TouchButton btnSteerLeft;
    public TouchButton btnSteerRight;
    public TouchButton btnGas;
    public TouchButton btnBrake;
    public TouchButton btnHandbrake;
    public Button btnPause;

    [Header("Diagnostics")]
    public TextMeshProUGUI txtDiagnostics;
    public bool showDiagnostics = true;

    [Header("Behavior")]
    [Tooltip("If true, automatically constructs UI elements if none are assigned at startup")]
    public bool autoGenerateIfMissing = true;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    private void Start()
    {
        // Auto build UI if not wired
        if (autoGenerateIfMissing && (btnSteerLeft == null || btnGas == null))
        {
            BuildRuntimeUI();
        }

        // Wire up pause button if present
        if (btnPause != null)
        {
            btnPause.onClick.RemoveAllListeners();
            btnPause.onClick.AddListener(OnPauseClicked);
        }
    }

    private void OnPauseClicked()
    {
        PauseMenu menu = PauseMenu.GetOrCreate();
        if (menu != null)
        {
            menu.TogglePause();
        }
    }

    public void UpdateDiagnostics(string message)
    {
        if (txtDiagnostics != null && showDiagnostics)
        {
            txtDiagnostics.text = message;
        }
    }

    public float GetSteerInput()
    {
        float steer = 0f;
        if (btnSteerLeft != null && btnSteerLeft.isPressed) steer -= 1f;
        if (btnSteerRight != null && btnSteerRight.isPressed) steer += 1f;
        return steer;
    }

    public float GetThrottleInput()
    {
        if (btnGas != null && btnGas.isPressed) return 1f;
        return 0f;
    }

    public float GetBrakeInput()
    {
        if (btnBrake != null && btnBrake.isPressed) return 1f;
        return 0f;
    }

    public bool GetHandbrakeInput()
    {
        return btnHandbrake != null && btnHandbrake.isPressed;
    }

    /// <summary>
    /// Programmatically generates clean, professional touchscreen controls at runtime if not set up in editor.
    /// </summary>
    public void BuildRuntimeUI()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // Draw under Pause Menu (order 100), above game

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();
        }

        // Ensure EventSystem exists
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // Colors
        Color glassDark = new Color(0.08f, 0.10f, 0.14f, 0.65f);
        Color glassPressed = new Color(0.20f, 0.25f, 0.35f, 0.90f);
        Color gasNormal = new Color(0.12f, 0.70f, 0.35f, 0.65f);
        Color gasPressed = new Color(0.18f, 0.95f, 0.48f, 0.95f);
        Color brakeNormal = new Color(0.85f, 0.20f, 0.20f, 0.65f);
        Color brakePressed = new Color(1.0f, 0.30f, 0.30f, 0.95f);
        Color driftNormal = new Color(0.95f, 0.65f, 0.10f, 0.65f);
        Color driftPressed = new Color(1.0f, 0.80f, 0.20f, 0.95f);
        Color textWhite = new Color(0.95f, 0.95f, 0.98f, 1.0f);

        // 1. STEER LEFT BUTTON (Bottom-Left)
        if (btnSteerLeft == null)
        {
            GameObject leftObj = CreateButtonObject("Btn_SteerLeft", transform, new Vector2(0f, 0f), new Vector2(130, 160), new Vector2(140, 140), glassDark);
            btnSteerLeft = leftObj.AddComponent<TouchButton>();
            btnSteerLeft.normalColor = glassDark;
            btnSteerLeft.pressedColor = glassPressed;
            CreateLabel(leftObj.transform, "<", 64, textWhite);
        }

        // 2. STEER RIGHT BUTTON (Bottom-Left next to Left)
        if (btnSteerRight == null)
        {
            GameObject rightObj = CreateButtonObject("Btn_SteerRight", transform, new Vector2(0f, 0f), new Vector2(300, 160), new Vector2(140, 140), glassDark);
            btnSteerRight = rightObj.AddComponent<TouchButton>();
            btnSteerRight.normalColor = glassDark;
            btnSteerRight.pressedColor = glassPressed;
            CreateLabel(rightObj.transform, ">", 64, textWhite);
        }

        // 3. BRAKE / REVERSE PEDAL (Bottom-Right, inner)
        if (btnBrake == null)
        {
            GameObject brakeObj = CreateButtonObject("Btn_Brake", transform, new Vector2(1f, 0f), new Vector2(-330, 170), new Vector2(130, 170), brakeNormal);
            btnBrake = brakeObj.AddComponent<TouchButton>();
            btnBrake.normalColor = brakeNormal;
            btnBrake.pressedColor = brakePressed;
            CreateLabel(brakeObj.transform, "BRAKE\n[R2]", 24, textWhite);
        }

        // 4. GAS / ACCELERATE PEDAL (Bottom-Right, outer)
        if (btnGas == null)
        {
            GameObject gasObj = CreateButtonObject("Btn_Gas", transform, new Vector2(1f, 0f), new Vector2(-160, 190), new Vector2(150, 210), gasNormal);
            btnGas = gasObj.AddComponent<TouchButton>();
            btnGas.normalColor = gasNormal;
            btnGas.pressedColor = gasPressed;
            CreateLabel(gasObj.transform, "GAS\n[L2]", 28, textWhite);
        }

        // 5. HANDBRAKE / DRIFT BUTTON (Above Brake)
        if (btnHandbrake == null)
        {
            GameObject driftObj = CreateButtonObject("Btn_Handbrake", transform, new Vector2(1f, 0f), new Vector2(-330, 310), new Vector2(130, 75), driftNormal);
            btnHandbrake = driftObj.AddComponent<TouchButton>();
            btnHandbrake.normalColor = driftNormal;
            btnHandbrake.pressedColor = driftPressed;
            CreateLabel(driftObj.transform, "HANDBRAKE", 18, textWhite);
        }

        // 6. PAUSE BUTTON (Top-Right)
        if (btnPause == null)
        {
            GameObject pauseObj = CreateButtonObject("Btn_TouchPause", transform, new Vector2(1f, 1f), new Vector2(-80, -80), new Vector2(75, 75), glassDark);
            btnPause = pauseObj.AddComponent<Button>();
            btnPause.onClick.AddListener(OnPauseClicked);
            CreateLabel(pauseObj.transform, "||", 28, textWhite);
        }

        // 7. DIAGNOSTICS TEXT (Top-Center)
        if (txtDiagnostics == null)
        {
            GameObject diagObj = new GameObject("Txt_Diagnostics");
            diagObj.transform.SetParent(transform, false);
            RectTransform rt = diagObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -25);
            rt.sizeDelta = new Vector2(700, 45);

            txtDiagnostics = diagObj.AddComponent<TextMeshProUGUI>();
            txtDiagnostics.fontSize = 18;
            txtDiagnostics.fontStyle = FontStyles.Bold;
            txtDiagnostics.alignment = TextAlignmentOptions.Center;
            txtDiagnostics.color = new Color(1f, 1f, 0.4f, 0.9f);
            txtDiagnostics.raycastTarget = false;
        }
    }

    private GameObject CreateButtonObject(string name, Transform parent, Vector2 anchor, Vector2 anchoredPos, Vector2 size, Color bgColor)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = obj.AddComponent<Image>();
        img.color = bgColor;
        img.raycastTarget = true;

        return obj;
    }

    private void CreateLabel(Transform parent, string text, float fontSize, Color color)
    {
        GameObject lblObj = new GameObject("Label");
        lblObj.transform.SetParent(parent, false);

        RectTransform rt = lblObj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = lblObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.raycastTarget = false;
    }
}
