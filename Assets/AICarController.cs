using UnityEngine;

[RequireComponent(typeof(CarController))]
public class AICarController : MonoBehaviour
{
    [Header("Waypoints Path")]
    public Transform pathRoot;            // The parent GameObject containing all waypoint transforms
    public float waypointThreshold = 12f; // How close to a waypoint before targeting the next

    [Header("AI Driving Style")]
    [Range(0f, 1f)] public float throttleLimit = 1.0f; // Max throttle multiplier (0.8 = slow, 1.0 = fast)
    public float brakingSensitivity = 1.8f;           // Higher = brakes earlier/harder before turns
    public float steerSmoothing = 4f;                 // Lower = smoother/heavier steering (prevents wobble)
    public float steerDamping = 4f;                   // Prevents steering spikes when very close to a waypoint

    [Header("Speed Limits")]
    public float maxSpeedLimit = 150f;     // Absolute speed cap (km/h) to prevent overspeeding on straights

    private Transform[] waypoints;
    private int currentWaypointIndex = 0;
    private CarController carController;
    private Rigidbody rb;

    void Start()
    {
        carController = GetComponent<CarController>();
        rb = GetComponent<Rigidbody>();
        
        // Force the CarController into AI Mode
        carController.isAI = true;

        SetupWaypoints();
    }

    void SetupWaypoints()
    {
        if (pathRoot == null)
        {
            Debug.LogError("[AI] Path Root not assigned! AI will not drive.");
            return;
        }

        // Get all children transforms of pathRoot
        int childCount = pathRoot.childCount;
        waypoints = new Transform[childCount];
        for (int i = 0; i < childCount; i++)
        {
            waypoints[i] = pathRoot.GetChild(i);
        }
        
        Debug.Log($"[AI] Setup complete. Found {waypoints.Length} waypoints for {gameObject.name}.");
    }

    void FixedUpdate()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        float speedKmH = rb.linearVelocity.magnitude * 3.6f;

        // 1. Get current target waypoint
        Transform targetWaypoint = waypoints[currentWaypointIndex];

        // 2. Distance check: switch to next waypoint if close enough
        float distanceToWaypoint = Vector3.Distance(transform.position, targetWaypoint.position);
        if (distanceToWaypoint < waypointThreshold)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            targetWaypoint = waypoints[currentWaypointIndex];
        }

        // 3. Steering logic (calculate angle to waypoint in local space)
        Vector3 localTarget = transform.InverseTransformPoint(targetWaypoint.position);
        
        // Calculate raw steer target with damping to prevent close-range spikes (zigzagging)
        float steerInput = localTarget.x / (localTarget.magnitude + steerDamping);
        float targetSteer = Mathf.Clamp(steerInput, -1f, 1f);

        // Smooth the steering to prevent high-speed overcorrection wobbling
        carController.aiSteerInput = Mathf.Lerp(carController.aiSteerInput, targetSteer, steerSmoothing * Time.fixedDeltaTime);

        // 4. Look-Ahead Corner Detection
        // Scan the next 3 waypoints to see if a sharp turn is coming up ahead
        float upcomingTurnSharpness = 0f;
        int lookAheadCount = 3;

        for (int i = 1; i <= lookAheadCount; i++)
        {
            int nextIndex = (currentWaypointIndex + i) % waypoints.Length;
            Transform nextWP = waypoints[nextIndex];
            Vector3 nextLocal = transform.InverseTransformPoint(nextWP.position);
            float nextSteer = Mathf.Abs(nextLocal.x / nextLocal.magnitude);

            // Farther turns have less immediate weight (divided by distance index)
            float weightedSharpness = nextSteer / i;
            upcomingTurnSharpness = Mathf.Max(upcomingTurnSharpness, weightedSharpness);
        }

        // 5. Throttle & Braking Calculation
        float throttleInput = throttleLimit;
        bool handbrake = false;

        // A. Speed Cap Check
        if (speedKmH > maxSpeedLimit)
        {
            // Apply light braking to hold the speed limit cap
            throttleInput = -0.3f;
        }
        else
        {
            // B. Pre-corner Braking (Braking Zone)
            // Brake force scales up the faster the car is going
            float speedFactor = Mathf.Clamp01(speedKmH / 100f); 
            float cornerBrakeIntensity = upcomingTurnSharpness * speedFactor * brakingSensitivity;

            if (cornerBrakeIntensity > 0.15f)
            {
                // Lerp into negative throttle (which triggers brakes/reverse in CarController)
                throttleInput = Mathf.Lerp(throttleLimit, -0.8f, cornerBrakeIntensity);

                // Apply handbrake if going fast into a massive upcoming hairpin turn
                if (upcomingTurnSharpness > 0.5f && speedKmH > 80f)
                {
                    handbrake = true;
                }
            }
        }

        carController.aiMoveInput = throttleInput;
        carController.aiHandbrakeInput = handbrake;
    }

    private void OnDrawGizmos()
    {
        if (pathRoot == null) return;

        Gizmos.color = Color.red;
        Transform prev = null;
        
        for (int i = 0; i < pathRoot.childCount; i++)
        {
            Transform curr = pathRoot.GetChild(i);
            Gizmos.DrawSphere(curr.position, 1.5f);

            if (prev != null)
            {
                Gizmos.DrawLine(prev.position, curr.position);
            }
            prev = curr;
        }

        if (pathRoot.childCount > 1)
        {
            Gizmos.DrawLine(pathRoot.GetChild(pathRoot.childCount - 1).position, pathRoot.GetChild(0).position);
        }
    }
}
