using UnityEngine;
using UnityEngine.UI;

public class OpponentIndicator : MonoBehaviour
{
    [Header("Indicator Settings")]
    public float heightOffset = 2.2f;    // Height offset above the car's pivot center
    public float scale = 0.015f;         // Visual scale of the canvas in the 3D scene
    public Color pinColor = new Color(1f, 0.2f, 0.2f, 0.9f); // Bright Neon Red/Pink
    public Sprite customPinSprite;       // Optional custom pin graphic
    
    private GameObject canvasGO;
    private RectTransform canvasRect;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;

        // 1. Create a child GameObject for the World Space Canvas
        canvasGO = new GameObject("OpponentIndicatorCanvas");
        canvasGO.transform.SetParent(this.transform, false);
        
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        
        canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(100f, 100f);
        canvasRect.localScale = Vector3.one * scale;
        
        // 2. Create the Pin Image child
        GameObject pinGO = new GameObject("PinImage", typeof(RectTransform));
        pinGO.transform.SetParent(canvasGO.transform, false);
        Image image = pinGO.AddComponent<Image>();
        
        // Try to load built-in knob/circle or fallback to a solid diamond
        if (customPinSprite != null)
        {
            image.sprite = customPinSprite;
        }
        else
        {
            #if UNITY_EDITOR
            Sprite defaultSprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (defaultSprite != null)
            {
                image.sprite = defaultSprite;
            }
            #endif
        }
        
        image.color = pinColor;
        
        RectTransform pinRect = pinGO.GetComponent<RectTransform>();
        pinRect.sizeDelta = new Vector2(30f, 30f);
        // Rotate image 45 degrees so that if it defaults to a square (no sprite loaded), it looks like a clean diamond!
        pinRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    void LateUpdate()
    {
        if (canvasGO == null) return;
        
        // Anchor the Canvas above the opponent's position
        canvasGO.transform.position = transform.position + Vector3.up * heightOffset;
        
        // Dynamic camera validation check
        if (mainCamera == null || !mainCamera.isActiveAndEnabled)
        {
            mainCamera = Camera.main;
        }

        // Billboard rotation: Make it face the main camera
        if (mainCamera != null)
        {
            // Face the camera directly
            canvasGO.transform.rotation = Quaternion.LookRotation(canvasGO.transform.position - mainCamera.transform.position);
        }
    }
}
