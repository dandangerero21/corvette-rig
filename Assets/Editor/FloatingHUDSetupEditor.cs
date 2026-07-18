#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class FloatingHUDSetupEditor : EditorWindow
{
    [MenuItem("Tools/Corvette Rig/Setup Floating HUD", false, 10)]
    public static void SetupFloatingHUD()
    {
        // 1. Find Player Car (CarController where isAI is false)
        CarController playerCar = null;
        CarController[] cars = FindObjectsByType<CarController>(FindObjectsSortMode.None);
        foreach (var car in cars)
        {
            if (!car.isAI)
            {
                playerCar = car;
                break;
            }
        }

        if (playerCar == null && cars.Length > 0)
        {
            playerCar = cars[0]; // Fallback to first car found
            Debug.LogWarning("[Floating HUD Setup] Active player car (isAI = false) not found. Falling back to: " + playerCar.gameObject.name);
        }

        if (playerCar == null)
        {
            EditorUtility.DisplayDialog("Setup Error", "No GameObject with CarController found in the scene. Please import or add a car with a CarController script first.", "OK");
            return;
        }

        Rigidbody carRb = playerCar.GetComponent<Rigidbody>();
        if (carRb == null)
        {
            EditorUtility.DisplayDialog("Setup Error", "Player car does not have a Rigidbody component. A Rigidbody is required on the car.", "OK");
            return;
        }

        // Automatically attach CarAudio to the player car
        CarAudio carAudio = playerCar.GetComponent<CarAudio>();
        if (carAudio == null)
        {
            carAudio = playerCar.gameObject.AddComponent<CarAudio>();
            Undo.RegisterCreatedObjectUndo(carAudio, "Add Car Audio");
            Debug.Log("[Floating HUD Setup] Added CarAudio component to " + playerCar.gameObject.name);
        }

        // 2. Find Waypoint Path Root
        Transform trackPathRoot = null;
        AICarController[] aiControllers = FindObjectsByType<AICarController>(FindObjectsSortMode.None);
        foreach (var ai in aiControllers)
        {
            if (ai.pathRoot != null)
            {
                trackPathRoot = ai.pathRoot;
                break;
            }
        }

        if (trackPathRoot == null)
        {
            // Try to find by name search
            GameObject pathGO = GameObject.Find("Path") ?? GameObject.Find("Waypoints") ?? GameObject.Find("TrackPath") ?? GameObject.Find("Track");
            if (pathGO != null)
            {
                trackPathRoot = pathGO.transform;
            }
        }

        if (trackPathRoot == null)
        {
            Debug.LogWarning("[Floating HUD Setup] Track path waypoints root not found. Minimap will need to be configured manually.");
        }

        // 3. Check for existing Canvas to avoid destructive overwrites
        GameObject canvasGO = GameObject.Find("FloatingHUDCanvas");
        bool updateExisting = false;

        if (canvasGO != null)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "Existing HUD Detected",
                "An existing 'FloatingHUDCanvas' was found in the scene.\n\n" +
                "What would you like to do?\n" +
                "• Update: Attaches the new Car Audio component to your car, hooks up any missing references, but preserves your custom offsets, colors, and panel positions.\n" +
                "• Recreate: Deletes the existing canvas and builds a fresh default layout.",
                "Update (Preserve Custom Tweaks)",
                "Recreate From Scratch",
                "Cancel"
            );

            if (choice == 2) // Cancel
            {
                return;
            }
            else if (choice == 0) // Update
            {
                updateExisting = true;
            }
            else if (choice == 1) // Recreate
            {
                Undo.DestroyObjectImmediate(canvasGO);
                canvasGO = null;
            }
        }

        // Create canvas if it doesn't exist
        if (canvasGO == null)
        {
            canvasGO = new GameObject("FloatingHUDCanvas");
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Floating HUD Canvas");
            
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Set RectTransform size & scale
            RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(960f, 450f);
            canvasRect.localScale = Vector3.one * 0.005f; // Makes it ~4.8 meters wide in the 3D scene
        }
        
        // Add/Get FloatingHUD component
        FloatingHUD floatingHUD = canvasGO.GetComponent<FloatingHUD>();
        if (floatingHUD == null)
        {
            floatingHUD = canvasGO.AddComponent<FloatingHUD>();
            floatingHUD.targetCar = playerCar.transform;
            floatingHUD.localOffset = new Vector3(0f, 0.4f, -2.6f); // Positioned closer and lower (just below taillights)
            floatingHUD.posSmoothTime = 0.08f;
            floatingHUD.rotSmoothTime = 0.1f;
            floatingHUD.SnapToTarget();
        }
        else
        {
            floatingHUD.targetCar = playerCar.transform;
        }

        // Add/Get Speedometer component
        Speedometer speedometer = canvasGO.GetComponent<Speedometer>();
        if (speedometer == null)
        {
            speedometer = canvasGO.AddComponent<Speedometer>();
        }
        speedometer.carRigidbody = carRb;
        speedometer.showMPH = false; // Real Racing 3 style defaults to km/h

        // UI references to wire up
        TextMeshProUGUI speedText = null;
        TextMeshProUGUI unitText = null;
        TextMeshProUGUI lapText = null;
        TextMeshProUGUI timeText = null;
        RectTransform dotRect = null;
        UIMinimapTrack minimapTrack = null;

        if (updateExisting)
        {
            // Auto-find references in the existing Canvas hierarchy to preserve them
            speedText = canvasGO.transform.Find("RightPanel_Speedometer/SpeedText")?.GetComponent<TextMeshProUGUI>();
            unitText = canvasGO.transform.Find("RightPanel_Speedometer/UnitText")?.GetComponent<TextMeshProUGUI>();
            lapText = canvasGO.transform.Find("LeftPanel_LapTime/LapText")?.GetComponent<TextMeshProUGUI>();
            timeText = canvasGO.transform.Find("LeftPanel_LapTime/TimeText")?.GetComponent<TextMeshProUGUI>();
            
            // Search minimap components
            var miniPanelTrans = canvasGO.transform.Find("RightPanel_Speedometer/MinimapPanel") ?? canvasGO.transform.Find("MinimapPanel");
            if (miniPanelTrans != null)
            {
                dotRect = miniPanelTrans.Find("PlayerDot")?.GetComponent<RectTransform>();
                var trackTrans = miniPanelTrans.Find("TrackPathRenderer");
                if (trackTrans != null)
                {
                    minimapTrack = trackTrans.GetComponent<UIMinimapTrack>();
                }
            }
            
            // Fallback: search anywhere in children if user renamed paths
            if (speedText == null) speedText = speedometer.speedText ?? canvasGO.GetComponentInChildren<TextMeshProUGUI>();
            if (minimapTrack == null) minimapTrack = speedometer.minimapTrack ?? canvasGO.GetComponentInChildren<UIMinimapTrack>();
        }
        else
        {
            // 4. Create Panels from scratch (fresh install)
            // Left Panel (Lap & Time)
            GameObject leftPanel = new GameObject("LeftPanel_LapTime", typeof(RectTransform));
            leftPanel.transform.SetParent(canvasGO.transform, false);
            RectTransform leftRect = leftPanel.GetComponent<RectTransform>();
            leftRect.anchoredPosition = new Vector2(-330f, -60f); // Widened spread
            leftRect.sizeDelta = new Vector2(250f, 150f);
            leftRect.localRotation = Quaternion.Euler(12f, 22f, 0f); // Inward tilt & backward tilt

            // Right Panel (Speedometer & Position)
            GameObject rightPanel = new GameObject("RightPanel_Speedometer", typeof(RectTransform));
            rightPanel.transform.SetParent(canvasGO.transform, false);
            RectTransform rightRect = rightPanel.GetComponent<RectTransform>();
            rightRect.anchoredPosition = new Vector2(330f, -60f); // Widened spread
            rightRect.sizeDelta = new Vector2(250f, 150f);
            rightRect.localRotation = Quaternion.Euler(12f, -22f, 0f); // Inward tilt & backward tilt

            // Minimap Panel (Child of Right Panel so it inherits the exact 3D tilt)
            GameObject minimapPanel = new GameObject("MinimapPanel", typeof(RectTransform));
            minimapPanel.transform.SetParent(rightPanel.transform, false);
            RectTransform miniRect = minimapPanel.GetComponent<RectTransform>();
            miniRect.anchoredPosition = new Vector2(0f, 130f); // Positioned directly above the speedometer text
            miniRect.sizeDelta = new Vector2(160f, 160f);

            // 5. Build Left Panel UI Elements
            // Lap Text (e.g. "lap 1/3")
            GameObject lapGO = new GameObject("LapText", typeof(RectTransform), typeof(TextMeshProUGUI));
            lapGO.transform.SetParent(leftPanel.transform, false);
            lapText = lapGO.GetComponent<TextMeshProUGUI>();
            lapText.text = "lap 1/3";
            lapText.fontSize = 28f;
            lapText.alignment = TextAlignmentOptions.Right;
            lapText.fontStyle = FontStyles.Bold | FontStyles.Italic;
            lapText.color = Color.white;
            RectTransform lapTextRect = lapGO.GetComponent<RectTransform>();
            lapTextRect.anchorMin = new Vector2(0, 0.5f);
            lapTextRect.anchorMax = new Vector2(1, 1);
            lapTextRect.offsetMin = Vector2.zero;
            lapTextRect.offsetMax = Vector2.zero;

            // Time Text (e.g. "13.604")
            GameObject timeGO = new GameObject("TimeText", typeof(RectTransform), typeof(TextMeshProUGUI));
            timeGO.transform.SetParent(leftPanel.transform, false);
            timeText = timeGO.GetComponent<TextMeshProUGUI>();
            timeText.text = "00.000";
            timeText.fontSize = 42f;
            timeText.alignment = TextAlignmentOptions.Right;
            timeText.fontStyle = FontStyles.Bold | FontStyles.Italic;
            timeText.color = Color.white;
            RectTransform timeTextRect = timeGO.GetComponent<RectTransform>();
            timeTextRect.anchorMin = new Vector2(0, 0);
            timeTextRect.anchorMax = new Vector2(1, 0.5f);
            timeTextRect.offsetMin = Vector2.zero;
            timeTextRect.offsetMax = Vector2.zero;

            // 6. Build Right Panel UI Elements
            // Speed Text (e.g. "191")
            GameObject speedGO = new GameObject("SpeedText", typeof(RectTransform), typeof(TextMeshProUGUI));
            speedGO.transform.SetParent(rightPanel.transform, false);
            speedText = speedGO.GetComponent<TextMeshProUGUI>();
            speedText.text = "0";
            speedText.fontSize = 56f;
            speedText.alignment = TextAlignmentOptions.Left;
            speedText.fontStyle = FontStyles.Bold | FontStyles.Italic;
            speedText.color = Color.white;
            RectTransform speedTextRect = speedGO.GetComponent<RectTransform>();
            speedTextRect.anchorMin = new Vector2(0, 0.3f);
            speedTextRect.anchorMax = new Vector2(1, 1);
            speedTextRect.offsetMin = Vector2.zero;
            speedTextRect.offsetMax = Vector2.zero;

            // Speed Unit Text (e.g. "km/h")
            GameObject unitGO = new GameObject("UnitText", typeof(RectTransform), typeof(TextMeshProUGUI));
            unitGO.transform.SetParent(rightPanel.transform, false);
            unitText = unitGO.GetComponent<TextMeshProUGUI>();
            unitText.text = "km/h";
            unitText.fontSize = 24f;
            unitText.alignment = TextAlignmentOptions.Left;
            unitText.fontStyle = FontStyles.Italic;
            unitText.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            RectTransform unitTextRect = unitGO.GetComponent<RectTransform>();
            unitTextRect.anchorMin = new Vector2(0, 0);
            unitTextRect.anchorMax = new Vector2(1, 0.3f);
            unitTextRect.offsetMin = Vector2.zero;
            unitTextRect.offsetMax = Vector2.zero;

            // 7. Build Minimap UI Elements
            // Track Line Renderer
            GameObject trackGO = new GameObject("TrackPathRenderer", typeof(RectTransform));
            trackGO.transform.SetParent(minimapPanel.transform, false);
            minimapTrack = trackGO.AddComponent<UIMinimapTrack>();
            minimapTrack.pathRoot = trackPathRoot;
            minimapTrack.lineWidth = 5f;
            minimapTrack.trackColor = new Color(1f, 1f, 1f, 0.65f); // Transparent white path
            minimapTrack.closedLoop = true;
            minimapTrack.CalculateBoundariesAndPoints();
            RectTransform trackRect = trackGO.GetComponent<RectTransform>();
            trackRect.anchorMin = Vector2.zero;
            trackRect.anchorMax = Vector2.one;
            trackRect.offsetMin = Vector2.zero;
            trackRect.offsetMax = Vector2.zero;

            // Player Indicator Dot
            GameObject dotGO = new GameObject("PlayerDot", typeof(RectTransform));
            dotGO.transform.SetParent(minimapPanel.transform, false);
            Image dotImage = dotGO.AddComponent<Image>();
            Sprite circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (circleSprite != null)
            {
                dotImage.sprite = circleSprite;
            }
            dotImage.color = new Color(1f, 0.65f, 0f, 1f); // Neon Orange player dot
            dotRect = dotGO.GetComponent<RectTransform>();
            dotRect.sizeDelta = new Vector2(14f, 14f);
        }

        // 8. Assign references to Speedometer component
        if (speedText != null) speedometer.speedText = speedText;
        if (unitText != null) speedometer.speedUnitText = unitText;
        if (lapText != null) speedometer.lapText = lapText;
        if (timeText != null) speedometer.timeText = timeText;
        if (dotRect != null) speedometer.minimapPlayerDot = dotRect;
        if (minimapTrack != null) speedometer.minimapTrack = minimapTrack;

        // Select the HUD Canvas in the Editor
        Selection.activeGameObject = canvasGO;
        
        string statusMessage = updateExisting 
            ? $"Floating HUD Canvas updated successfully!\nAttached Car Audio to player car and preserved all your custom offsets, colors, and tilts."
            : $"Floating 3D HUD has been successfully set up and attached to '{playerCar.gameObject.name}'!\nTrack path found: {(trackPathRoot != null ? trackPathRoot.name : "None")}";

        EditorUtility.DisplayDialog("Setup Status", statusMessage, "OK");
    }

    [MenuItem("Tools/Corvette Rig/Setup Opponent Indicators", false, 11)]
    public static void SetupOpponentIndicators()
    {
        // Find all CarControllers in the scene, including inactive ones
        CarController[] cars = FindObjectsByType<CarController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;

        foreach (var car in cars)
        {
            // Check if it is an AI car by checking the component or flag (essential for Edit Mode support)
            bool isAI = car.isAI || car.GetComponent<AICarController>() != null;
            if (isAI)
            {
                // Attach OpponentIndicator component if it doesn't already exist
                OpponentIndicator indicator = car.GetComponent<OpponentIndicator>();
                if (indicator == null)
                {
                    indicator = car.gameObject.AddComponent<OpponentIndicator>();
                    Undo.RegisterCreatedObjectUndo(indicator, "Add Opponent Indicator");
                }

                // Default settings
                indicator.heightOffset = 2.2f;
                indicator.scale = 0.015f;
                indicator.pinColor = new Color(1f, 0.2f, 0.2f, 0.9f); // Bright neon red/pink
                
                count++;
            }
        }

        EditorUtility.DisplayDialog("Opponent Indicators Setup", 
            $"Successfully attached opponent indicator pins to {count} AI cars in the scene!", "OK");
    }
}
#endif
