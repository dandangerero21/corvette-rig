using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Speedometer : MonoBehaviour
{
    [Header("Car & Race Settings")]
    public Rigidbody carRigidbody;
    public int currentPosition = 22;
    public int currentLap = 1;
    public int totalLaps = 3;
    public float currentLapTime = 0.0f;
    public bool showMPH = true;

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
    
    [HideInInspector]
    public Vector2 worldMapMin;
    [HideInInspector]
    public Vector2 worldMapMax;

    private void Start()
    {

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
        if (minimapContainer == null || minimapPlayerDot == null || carRigidbody == null)
            return;

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
        int seconds = Mathf.FloorToInt(timeInSeconds);
        int milliseconds = Mathf.FloorToInt((timeInSeconds - seconds) * 1000f);
        return $"{seconds}.{milliseconds:D3}";
    }
}
