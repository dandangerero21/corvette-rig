using UnityEngine;

/// <summary>
/// Place this on an Empty GameObject with a Box Collider (set to Trigger)
/// positioned before a hairpin. When the AI car enters, it forces hard braking.
/// Set the target speed you want the AI to scrub down to before exiting the zone.
/// </summary>
public class AIBrakingZone : MonoBehaviour
{
    [Tooltip("The AI will brake until it reaches this speed (km/h) inside the zone.")]
    public float targetSpeedKmH = 60f;

    [Tooltip("How hard to brake. 1.0 = full brakes.")]
    [Range(0f, 1f)]
    public float brakeStrength = 1.0f;

    void OnTriggerStay(Collider other)
    {
        // Use GetComponentInParent because the trigger might hit a wheel or body mesh child collider!
        var ai = other.GetComponentInParent<AICarController>();
        if (ai == null) return;

        var rb = ai.GetComponent<Rigidbody>();
        if (rb == null) return;

        float speedKmH = rb.linearVelocity.magnitude * 3.6f;
        if (speedKmH > targetSpeedKmH)
        {
            // Safely inject brake command into the AI's logic
            ai.externalBrakeOverride = brakeStrength;
        }
    }
}
