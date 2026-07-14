using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Speedometer : MonoBehaviour
{
    [Header("Car")]
    public Rigidbody carRigidbody;

    [Header("UI References")]
    public TextMeshProUGUI speedNumberText;   // The big number
    public TextMeshProUGUI unitText;          // "km/h" label
    public Image speedArcFill;               // Optional arc fill image (Image Type: Filled)

    [Header("Settings")]
    public float maxDisplaySpeed = 260f;     // Speed at which arc is 100% full
    public bool showMPH = false;

    void Update()
    {
        if (carRigidbody == null) return;

        float speedMS = carRigidbody.linearVelocity.magnitude;
        float speedKmH = speedMS * 3.6f;
        float displaySpeed = showMPH ? speedKmH * 0.621371f : speedKmH;

        // Big number
        if (speedNumberText != null)
            speedNumberText.text = Mathf.FloorToInt(displaySpeed).ToString();

        // Unit label
        if (unitText != null)
            unitText.text = showMPH ? "mph" : "km/h";

        // Arc fill (0 to 1)
        if (speedArcFill != null)
            speedArcFill.fillAmount = Mathf.Clamp01(displaySpeed / maxDisplaySpeed);
    }
}
