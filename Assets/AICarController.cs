using UnityEngine;

[RequireComponent(typeof(CarController))]
public class AICarController : MonoBehaviour
{
    [Header("Waypoints Path")]
    public Transform pathRoot;            // The parent GameObject containing all waypoint transforms
    public float waypointThreshold = 12f; // How close to a waypoint before targeting the next

    [Header("AI Driving Style")]
    [Range(0f, 1f)] public float throttleLimit = 1.0f; // Max throttle multiplier (0.8 = slow, 1.0 = fast)
    // ponytail: bumped braking sensitivity from 3.5 to 5.5 so it scrubs speed earlier before sharp hairpins
    public float brakingSensitivity = 5.5f;           // Higher = brakes earlier/harder before turns
    public float steerSmoothing = 10f;                // Higher = faster steering response (AI doesn't need human 'heavy feel')
    public float steerDamping = 4f;                   // Prevents steering spikes when very close to a waypoint

    [Header("Speed Limits")]
    public float maxSpeedLimit = 250f;     // Absolute speed cap (km/h) — set to match CarController maxSpeed

    [Header("Look-Ahead Steering")]
    [Tooltip("Base carrot distance at low speed. Scales up automatically with speed.")]
    public float steerLookAheadDistance = 20f;  // metres at low speed
    [Tooltip("How much the steer look-ahead grows per 100 km/h of speed.\n" +
             "e.g. 1.5 → at 200 km/h the carrot is 20 + 1.5×2×20 = 80 m ahead.")]
    public float steerLookAheadSpeedScale = 1.5f;

    [Header("Braking Look-Ahead")]
    [Tooltip("Fixed distance (m) of path scanned ahead for corners.\n" +
             "Do NOT scale this with speed — a larger window inflates the accumulated\n" +
             "sharpness sum and causes false braking on straights at high speed.")]
    // ponytail: bumped look-ahead from 80 to 100 so it sees hairpins sooner and starts braking in time
    public float brakeLookAheadDistance = 100f;  // fixed metres, independent of speed
    [Tooltip("Minimum sharpness a road segment must have before it contributes to braking.\n" +
             "Filters out tiny waypoint misalignments on 'straight' sections.\n" +
             "0.025 ≈ 18°  |  0.038 ≈ 22°  |  0.067 ≈ 30°\n" +
             "Raise this if the car false-brakes on straights. Lower it if it misses gentle curves.")]
    // ponytail: lowered to 0.005 (approx 8 degrees). The hairpin in the image has so many waypoints that each segment is less than 18 degrees, causing the AI to completely ignore the 180-degree turn!
    public float minSegmentSharpness = 0.005f;

    private Transform[] waypoints;
    private int currentWaypointIndex = 0;
    private CarController carController;
    private Rigidbody rb;

    [Header("Stuck Recovery (AI)")]
    private float stuckTimer = 0f;
    private float reverseTimer = 0f;
    private float recoveryCooldown = 0f;
    private float debugLogTimer = 0f;
    
    [HideInInspector] public float externalBrakeOverride = 0f;

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

        // ── 1. WAYPOINT ADVANCE ──────────────────────────────────────────────
        Transform targetWaypoint = waypoints[currentWaypointIndex];
        float distanceToWaypoint = Vector3.Distance(transform.position, targetWaypoint.position);
        if (distanceToWaypoint < waypointThreshold)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            targetWaypoint = waypoints[currentWaypointIndex];
        }


        float speedScale = speedKmH / 100f;
        float effectiveSteerDist = steerLookAheadDistance * (1f + steerLookAheadSpeedScale * speedScale);

        Vector3 steerTarget = GetLookAheadPoint(currentWaypointIndex, effectiveSteerDist);

        Vector3 localTarget = transform.InverseTransformPoint(steerTarget);

        float steerInput = localTarget.x / (localTarget.magnitude + steerDamping);
        float targetSteer = Mathf.Clamp(steerInput, -1f, 1f);

        // If reversing to get unstuck, steer opposite to target direction 
        // to swing the rear end of the car away from the obstacle/wall.
        if (reverseTimer > 0f)
        {
            targetSteer = Mathf.Clamp(-targetSteer * 1.5f, -0.8f, 0.8f);
        }

        carController.aiSteerInput = Mathf.Lerp(carController.aiSteerInput, targetSteer, steerSmoothing * Time.fixedDeltaTime);

        float upcomingTurnSharpness = 0f;
        float walkedDist = Vector3.Distance(transform.position, waypoints[currentWaypointIndex].position);
        int scanIdx = currentWaypointIndex;

        for (int safety = 0; safety < waypoints.Length && walkedDist < brakeLookAheadDistance; safety++)
        {
            int idxA = scanIdx;
            int idxB = (scanIdx + 1) % waypoints.Length;
            int idxC = (scanIdx + 2) % waypoints.Length;

            Vector3 posA = waypoints[idxA].position;
            Vector3 posB = waypoints[idxB].position;
            Vector3 posC = waypoints[idxC].position;

            float segLen = Vector3.Distance(posA, posB);
            walkedDist += Mathf.Max(segLen, 0.01f);

            Vector3 dirAB = (posB - posA).normalized;
            Vector3 dirBC = (posC - posB).normalized;
            float dot       = Vector3.Dot(dirAB, dirBC);  // 1 = straight, -1 = U-turn
            float sharpness = (1f - dot) * 0.5f;          // remap → [0, 1] per segment

            if (sharpness < minSegmentSharpness)
            {
                scanIdx = idxB;
                continue;
            }


            float distFraction = Mathf.Clamp01(walkedDist / brakeLookAheadDistance);
            float proximity = (1f - distFraction) * (1f - distFraction);
            upcomingTurnSharpness += sharpness * proximity; // ACCUMULATE

            scanIdx = idxB;
        }

        // Clamp so we stay in [0, 1] for the throttle math below
        upcomingTurnSharpness = Mathf.Clamp01(upcomingTurnSharpness);

        // Square the sharpness to suppress low-amplitude noise (gentle sweeps)
        // while keeping high-amplitude signals (sharp hairpins) strong.
        float finalSharpness = upcomingTurnSharpness * upcomingTurnSharpness;

        float throttleInput = throttleLimit;
        bool handbrake = false;

        if (speedKmH > maxSpeedLimit)
        {
            throttleInput = -0.3f;
        }
        else if (finalSharpness > 0f)
        {
            float speedFactor = Mathf.Clamp01(speedKmH / 100f);
            float response = finalSharpness * speedFactor * brakingSensitivity;

            // Stage 1: lift off throttle
            float liftOff = Mathf.Clamp01(response / 0.6f);
            throttleInput = Mathf.Lerp(throttleLimit, 0f, liftOff);

            // Stage 2: apply brakes
            if (response > 0.6f)
            {
                float brakeT = Mathf.Clamp01((response - 0.6f) / 0.8f);
                throttleInput = Mathf.Lerp(0f, -0.9f, brakeT);
            }

            // Handbrake for genuine sharp hairpins at speed
            if (finalSharpness > 0.6f && speedKmH > 80f)
            {
                handbrake = true;
            }
        }

        // DEBUG: Log live braking values every 0.5s so we can diagnose hairpin braking
        debugLogTimer -= Time.fixedDeltaTime;
        if (debugLogTimer <= 0f)
        {
            debugLogTimer = 0.5f;
            Debug.Log($"[AI Brake Debug] speed={speedKmH:F1} upcomingSharpness={upcomingTurnSharpness:F3} finalSharpness={finalSharpness:F3} throttle={throttleInput:F2}");
        }

        // ── 5. STUCK DETECTION & RECOVERY STATE MACHINE ───────────────────────
        if (reverseTimer > 0f)
        {
            // Currently reversing to get unstuck
            reverseTimer -= Time.fixedDeltaTime;
            throttleInput = -0.8f; // Reverse throttle
            handbrake = false;

            // Steer input is handled smoothly via targetSteer inversion in step 2.

            if (reverseTimer <= 0f)
            {
                // Recovery complete, start cooldown to let the car gain speed
                recoveryCooldown = 1.5f;
                stuckTimer = 0f;
            }
        }
        else
        {
            // Normal driving
            if (recoveryCooldown > 0f)
            {
                recoveryCooldown -= Time.fixedDeltaTime;
            }

            // If attempting to go forward but speed is near zero (e.g. wall collision)
            if (throttleInput > 0.1f && speedKmH < 3.0f && recoveryCooldown <= 0f)
            {
                stuckTimer += Time.fixedDeltaTime;
                if (stuckTimer > 1.5f)
                {
                    // Stuck for 1.5s -> Trigger 2 seconds of reverse recovery
                    reverseTimer = 2.0f;
                }
            }
            else
            {
                stuckTimer = Mathf.Max(0f, stuckTimer - Time.fixedDeltaTime); // bleed off stuck timer
            }
        }

        carController.aiMoveInput = throttleInput;
        carController.aiHandbrakeInput = handbrake;
        
        // External override for custom braking zones (consumes the value safely)
        if (externalBrakeOverride > 0f)
        {
            carController.aiMoveInput = -externalBrakeOverride;
            externalBrakeOverride = 0f;
        }
    }

    /// <summary>
    /// Walks <paramref name="distance"/> metres forward along the waypoint path
    /// starting from waypoint <paramref name="fromIndex"/> and returns the
    /// interpolated world-space position (the "carrot" point).
    /// </summary>
    private Vector3 GetLookAheadPoint(int fromIndex, float distance)
    {
        float remaining = distance;
        int idx = fromIndex;

        for (int safety = 0; safety < waypoints.Length; safety++)
        {
            int nextIdx = (idx + 1) % waypoints.Length;
            Vector3 segStart = waypoints[idx].position;
            Vector3 segEnd   = waypoints[nextIdx].position;
            float segLen     = Vector3.Distance(segStart, segEnd);

            if (remaining <= segLen)
            {
                // The carrot sits on this segment — interpolate exactly
                return Vector3.Lerp(segStart, segEnd, remaining / segLen);
            }

            remaining -= segLen;
            idx = nextIdx;
        }

        // Fallback: just return the current waypoint if path is very short
        return waypoints[fromIndex].position;
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

        // Draw the live carrot (look-ahead) point in green during Play mode
        if (Application.isPlaying && waypoints != null && waypoints.Length > 0)
        {
            Gizmos.color = Color.green;
            Vector3 carrot = GetLookAheadPoint(currentWaypointIndex, steerLookAheadDistance);
            Gizmos.DrawSphere(carrot, 2f);
            Gizmos.DrawLine(transform.position, carrot);
        }
    }
}
