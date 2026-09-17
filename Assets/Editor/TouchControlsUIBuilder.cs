using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class TouchControlsUIBuilder : EditorWindow
{
    [MenuItem("Tools/Generate Touchscreen Mobile Controls UI")]
    public static void GenerateTouchUI()
    {
        // 1. Remove existing to avoid duplicate canvases
        GameObject existingCanvas = GameObject.Find("TouchControlsCanvas");
        if (existingCanvas != null)
        {
            Undo.DestroyObjectImmediate(existingCanvas);
        }

        // 2. Ensure EventSystem exists
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        // 3. Create Root Canvas
        GameObject canvasObj = new GameObject("TouchControlsCanvas");
        Undo.RegisterCreatedObjectUndo(canvasObj, "Create TouchControlsCanvas");

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();
        TouchControls touchControls = canvasObj.AddComponent<TouchControls>();
        touchControls.autoGenerateIfMissing = false;

        // Visual Palette
        Color glassDark = new Color(0.06f, 0.08f, 0.12f, 0.65f);
        Color glassPressed = new Color(0.22f, 0.28f, 0.38f, 0.90f);
        Color gasNormal = new Color(0.10f, 0.65f, 0.32f, 0.70f);
        Color gasPressed = new Color(0.16f, 0.92f, 0.45f, 0.95f);
        Color brakeNormal = new Color(0.80f, 0.18f, 0.18f, 0.70f);
        Color brakePressed = new Color(0.98f, 0.28f, 0.28f, 0.95f);
        Color driftNormal = new Color(0.92f, 0.62f, 0.08f, 0.65f);
        Color driftPressed = new Color(1.0f, 0.78f, 0.18f, 0.95f);
        Color textWhite = new Color(0.96f, 0.96f, 0.98f, 1.0f);

        // STEER LEFT (Bottom-Left)
        GameObject leftObj = CreateTouchButton("Btn_SteerLeft", canvasObj.transform, new Vector2(0f, 0f), new Vector2(140, 160), new Vector2(140, 140), glassDark);
        TouchButton tbLeft = leftObj.AddComponent<TouchButton>();
        tbLeft.normalColor = glassDark;
        tbLeft.pressedColor = glassPressed;
        CreateTMPLabel(leftObj.transform, "<", 64, textWhite);
        touchControls.btnSteerLeft = tbLeft;

        // STEER RIGHT (Bottom-Left)
        GameObject rightObj = CreateTouchButton("Btn_SteerRight", canvasObj.transform, new Vector2(0f, 0f), new Vector2(310, 160), new Vector2(140, 140), glassDark);
        TouchButton tbRight = rightObj.AddComponent<TouchButton>();
        tbRight.normalColor = glassDark;
        tbRight.pressedColor = glassPressed;
        CreateTMPLabel(rightObj.transform, ">", 64, textWhite);
        touchControls.btnSteerRight = tbRight;

        // BRAKE / REVERSE (Bottom-Right, inner)
        GameObject brakeObj = CreateTouchButton("Btn_Brake", canvasObj.transform, new Vector2(1f, 0f), new Vector2(-330, 175), new Vector2(130, 175), brakeNormal);
        TouchButton tbBrake = brakeObj.AddComponent<TouchButton>();
        tbBrake.normalColor = brakeNormal;
        tbBrake.pressedColor = brakePressed;
        CreateTMPLabel(brakeObj.transform, "BRAKE\n[R2]", 24, textWhite);
        touchControls.btnBrake = tbBrake;

        // GAS / ACCELERATE (Bottom-Right, outer)
        GameObject gasObj = CreateTouchButton("Btn_Gas", canvasObj.transform, new Vector2(1f, 0f), new Vector2(-160, 195), new Vector2(150, 215), gasNormal);
        TouchButton tbGas = gasObj.AddComponent<TouchButton>();
        tbGas.normalColor = gasNormal;
        tbGas.pressedColor = gasPressed;
        CreateTMPLabel(gasObj.transform, "GAS\n[L2]", 28, textWhite);
        touchControls.btnGas = tbGas;

        // HANDBRAKE (Above Brake)
        GameObject driftObj = CreateTouchButton("Btn_Handbrake", canvasObj.transform, new Vector2(1f, 0f), new Vector2(-330, 315), new Vector2(130, 75), driftNormal);
        TouchButton tbDrift = driftObj.AddComponent<TouchButton>();
        tbDrift.normalColor = driftNormal;
        tbDrift.pressedColor = driftPressed;
        CreateTMPLabel(driftObj.transform, "DRIFT", 18, textWhite);
        touchControls.btnHandbrake = tbDrift;

        // PAUSE BUTTON (Top-Right)
        GameObject pauseObj = CreateTouchButton("Btn_TouchPause", canvasObj.transform, new Vector2(1f, 1f), new Vector2(-80, -80), new Vector2(75, 75), glassDark);
        Button pauseBtn = pauseObj.AddComponent<Button>();
        CreateTMPLabel(pauseObj.transform, "||", 28, textWhite);
        touchControls.btnPause = pauseBtn;

        EditorUtility.SetDirty(canvasObj);
        Debug.Log("<color=#00FF66><b>[TouchControlsUIBuilder]</b> Touchscreen Mobile Controls UI generated successfully!</color>");
    }

    private static GameObject CreateTouchButton(string name, Transform parent, Vector2 anchor, Vector2 anchoredPos, Vector2 size, Color color)
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
        img.color = color;
        img.raycastTarget = true;

        return obj;
    }

    private static void CreateTMPLabel(Transform parent, string text, float fontSize, Color color)
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
