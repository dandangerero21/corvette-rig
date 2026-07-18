using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Speedometer : MonoBehaviour
{
    // Event fired when a lap is completed
    public static System.Action OnLapCompleted;

    [Header("Car & Race Settings")]
    public Rigidbody carRigidbody;
    public int currentPosition = 22;
    public int currentLap = 1;
    public int totalLaps = 3;
    public float currentLapTime = 0.0f;
    public bool showMPH = true;

    [Header("Automatic Lap Counting")]
    public Transform lapPathRoot; // Optional path root transform (falls back to minimapTrack.pathRoot)
    public bool showDebugHUD = true; // Toggle on-screen debug display of the lap counting variables
    private Transform[] lapWaypoints;
    private int lastClosestWaypointIdx = -1;
    private bool halfTrackPassed = false;
    private float nextWaypointsScanTime = 0f;

    [Header("UI Speedometer (Right Side)")]
    public TextMeshProUGUI speedText;         // e.g. "89"
    public TextMeshProUGUI speedUnitText;     // e.g. "mph" (italic)
    public TextMeshProUGUI positionText;      // e.g. "22"
    public TextMeshProUGUI positionSuffixText; // e.g. "nd"

    [Header("UI Lap & Time (Left Side)")]
    public TextMeshProUGUI lapText;           // e.g. "lap 1/3"
    public TextMeshProUGUI timeText;          // e.g. "11.767"

    [Header("UI Progress Bar (Left Side)")]
    public RectTransform progressIndicatorDot; // The dot showing progress
    public RectTransform progressBarLine;     // The vertical line UI parent
    public Transform trackStartPoint;         // Start line of track
    public float totalTrackLength = 5400f;    // Length of track in meters

    [Header("Minimap Settings")]
    public RectTransform minimapContainer;    // The UI Image of the white map outline
    public RectTransform minimapPlayerDot;     // The small dot representing the player
    public Collider trackCollider;            // Drag your track collider here to auto-calculate boundaries!
    public UIMinimapTrack minimapTrack;        // Dynamic path-based minimap component
    
    [HideInInspector]
    public Vector2 worldMapMin;
    [HideInInspector]
    public Vector2 worldMapMax;

    private void Start()
    {
        InitializeLapWaypoints();

        // Automatically calculate track boundaries from the collider bounds
        if (trackCollider != null)
        {
            Bounds bounds = trackCollider.bounds;
            worldMapMin = new Vector2(bounds.min.x, bounds.min.z);
            worldMapMax = new Vector2(bounds.max.x, bounds.max.z);
            Debug.Log($"[Minimap] Calculated boundaries: Min {worldMapMin}, Max {worldMapMax}");
        }
        else
        {
            Debug.LogWarning("[Minimap] No Track Collider assigned! Minimap dot will not move correctly.");
        }
    }

    private void Update()
    {
        UpdateSpeedometerHUD();
        UpdateRaceStatsHUD();
        UpdateProgressBarHUD();
        UpdateMinimapHUD();
    }

    private void UpdateSpeedometerHUD()
    {
        if (carRigidbody == null) return;

        // Speed calculation
        float speedMS = carRigidbody.linearVelocity.magnitude;
        float speedKmH = speedMS * 3.6f;
        float displaySpeed = showMPH ? speedKmH * 0.621371f : speedKmH;

        if (speedText != null)
            speedText.text = Mathf.FloorToInt(displaySpeed).ToString();

        if (speedUnitText != null)
            speedUnitText.text = showMPH ? "mph" : "km/h";

        // Position calculation
        if (positionText != null)
            positionText.text = currentPosition.ToString();

        if (positionSuffixText != null)
            positionSuffixText.text = GetOrdinalSuffix(currentPosition);
    }

    private void UpdateRaceStatsHUD()
    {
        // Increment time
        if (Application.isPlaying)
        {
            currentLapTime += Time.deltaTime;
        }

        UpdateLapTracking();

        if (lapText != null)
            lapText.text = "lap " + currentLap + "/" + totalLaps;

        if (timeText != null)
            timeText.text = FormatTime(currentLapTime);
    }

    private void UpdateProgressBarHUD()
    {
        if (progressIndicatorDot == null || progressBarLine == null || carRigidbody == null || trackStartPoint == null)
            return;

        // Calculate progress percentage around the track
        float distanceTravelled = Vector3.Distance(carRigidbody.transform.position, trackStartPoint.position);
        float progressFactor = Mathf.Clamp01(distanceTravelled / totalTrackLength);

        // Move the indicator dot vertically along the progress bar line
        float barHeight = progressBarLine.rect.height;
        float newY = Mathf.Lerp(-barHeight / 2f, barHeight / 2f, progressFactor);
        progressIndicatorDot.anchoredPosition = new Vector2(progressIndicatorDot.anchoredPosition.x, newY);
    }

    private void UpdateMinimapHUD()
    {
        if (minimapPlayerDot == null || carRigidbody == null)
            return;

        if (minimapTrack != null)
        {
            // Use the vector track map to compute precise local coordinates
            minimapPlayerDot.anchoredPosition = minimapTrack.WorldToMinimapPosition(carRigidbody.transform.position);
        }
        else if (minimapContainer != null)
        {
            Vector3 carPos = carRigidbody.transform.position;

            // Normalize car position within the defined world boundaries (0 to 1)
            float normX = Mathf.InverseLerp(worldMapMin.x, worldMapMax.x, carPos.x);
            float normY = Mathf.InverseLerp(worldMapMin.y, worldMapMax.y, carPos.z); // Z is depth/Y in 2D map

            // Convert normalized position to UI container size
            float mapWidth = minimapContainer.rect.width;
            float mapHeight = minimapContainer.rect.height;

            float uiX = (normX - 0.5f) * mapWidth;
            float uiY = (normY - 0.5f) * mapHeight;

            minimapPlayerDot.anchoredPosition = new Vector2(uiX, uiY);
        }
    }

    private string GetOrdinalSuffix(int number)
    {
        if (number <= 0) return "";
        switch (number % 100)
        {
            case 11:
            case 12:
            case 13:
                return "th";
        }
        switch (number % 10)
        {
            case 1: return "st";
            case 2: return "nd";
            case 3: return "rd";
            default: return "th";
        }
    }

    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        int milliseconds = Mathf.FloorToInt((timeInSeconds - Mathf.Floor(timeInSeconds)) * 1000f);
        return $"{minutes}:{seconds:D2}.{milliseconds:D3}";
    }

    private void InitializeLapWaypoints()
    {
        Transform path = lapPathRoot;
        
        // Fallback 1: Use minimapTrack's path root
        if (path == null && minimapTrack != null)
        {
            path = minimapTrack.pathRoot;
        }
        
        // Fallback 2: Scan the scene for AICarController (which always has the track path assigned)
        if (path == null)
        {
            AICarController[] aiControllers = FindObjectsByType<AICarController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var ai in aiControllers)
            {
                if (ai.pathRoot != null)
                {
                    path = ai.pathRoot;
                    break;
                }
            }
        }
        
        // Fallback 3: Search the scene for standard path naming conventions
        if (path == null)
        {
            GameObject pathGO = GameObject.Find("Path") ?? GameObject.Find("Waypoints") ?? GameObject.Find("TrackPath") ?? GameObject.Find("Track");
            if (pathGO != null)
            {
                path = pathGO.transform;
            }
        }

        // Auto-recovery: If the path assigned has 0 children but has a parent,
        // it means the user likely assigned a single waypoint (child) instead of the path root (parent)!
        // We automatically step up to the parent to get the full waypoint list.
        if (path != null && path.childCount == 0 && path.parent != null)
        {
            path = path.parent;
        }

        if (path != null && path.childCount > 0)
        {
            lapWaypoints = new Transform[path.childCount];
            for (int i = 0; i < path.childCount; i++)
            {
                lapWaypoints[i] = path.GetChild(i);
            }
            lastClosestWaypointIdx = GetClosestWaypointIndex();
            halfTrackPassed = false;
            Debug.Log($"[Lap Tracker] Auto-initialized with {lapWaypoints.Length} waypoints from '{path.gameObject.name}'.");
        }
    }

    private void UpdateLapTracking()
    {
        if (lapWaypoints == null || lapWaypoints.Length == 0)
        {
            if (Time.time < nextWaypointsScanTime) return;
            nextWaypointsScanTime = Time.time + 2.0f; // Only scan once every 2 seconds
            
            // Try to initialize on-the-fly if path wasn't ready at Start
            InitializeLapWaypoints();
            if (lapWaypoints == null || lapWaypoints.Length == 0) return;
        }

        int closestIdx = GetClosestWaypointIndex();
        if (closestIdx == -1 || closestIdx == lastClosestWaypointIdx) return;

        int waypointCount = lapWaypoints.Length;

        // Check if we passed the halfway mark area of the track (middle 50% of track)
        // Using a range prevents skips due to high speeds.
        if (closestIdx > waypointCount * 0.25f && closestIdx < waypointCount * 0.75f)
        {
            halfTrackPassed = true;
        }

        // Lap crossing check: transition from the last part of waypoints back to the start.
        // Calculates the index change. If we jump from a high index (near end of track) to a low index (near start)
        // it registers as a finish line crossing. This is mathematically N-independent.
        int indexChange = closestIdx - lastClosestWaypointIdx;
        if (indexChange < -waypointCount / 2)
        {
            if (halfTrackPassed)
            {
                currentLap++;
                currentLapTime = 0f; // Reset lap time on new lap!
                halfTrackPassed = false;
                OnLapCompleted?.Invoke();
                Debug.Log($"[Lap Tracker] Lap completed! Current Lap: {currentLap}");
            }
        }

        lastClosestWaypointIdx = closestIdx;
    }

    private int GetClosestWaypointIndex()
    {
        if (lapWaypoints == null || lapWaypoints.Length == 0 || carRigidbody == null) return -1;

        Vector3 carPos = carRigidbody.transform.position;
        float minDst = float.MaxValue;
        int closestIdx = -1;

        for (int i = 0; i < lapWaypoints.Length; i++)
        {
            float dst = Vector3.Distance(carPos, lapWaypoints[i].position);
            if (dst < minDst)
            {
                minDst = dst;
                closestIdx = i;
            }
        }
        return closestIdx;
    }

    private void OnGUI()
    {
        if (!showDebugHUD) return;

        GUI.Box(new Rect(10, 10, 320, 170), "Lap Tracker Diagnostic HUD");
        GUI.Label(new Rect(20, 30, 300, 20), $"Waypoints Count: {(lapWaypoints != null ? lapWaypoints.Length.ToString() : "0 (No path assigned!)")}");
        GUI.Label(new Rect(20, 50, 300, 20), $"Current Closest Waypoint: {GetClosestWaypointIndex()}");
        GUI.Label(new Rect(20, 70, 300, 20), $"Last Closest Waypoint: {lastClosestWaypointIdx}");
        GUI.Label(new Rect(20, 90, 300, 20), $"Half Track Passed: {halfTrackPassed}");
        GUI.Label(new Rect(20, 110, 300, 20), $"Current Lap: {currentLap}");
        GUI.Label(new Rect(20, 130, 300, 20), $"Car Rigidbody: {(carRigidbody != null ? carRigidbody.gameObject.name : "Null!")}");
        GUI.Label(new Rect(20, 150, 300, 20), $"Car Position: {(carRigidbody != null ? carRigidbody.transform.position.ToString("F1") : "N/A")}");
    }
}
