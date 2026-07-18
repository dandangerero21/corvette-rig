using UnityEngine;

public class FloatingHUD : MonoBehaviour
{
    [Header("Target Follow")]
    public Transform targetCar;
    public Vector3 localOffset = new Vector3(0f, 0.4f, -2.6f); // Floating behind the car's rear bumper
    
    [Header("Smoothing")]
    public float posSmoothTime = 0.1f;
    public float rotSmoothTime = 0.12f;
    
    [Header("G-Force / Inertia Reaction")]
    [Tooltip("React strength to steering/cornering G-forces (swings left/right).")]
    public float lateralGForceStrength = 0.05f;
    [Tooltip("React strength to acceleration/braking G-forces (moves forward/backward).")]
    public float longitudinalGForceStrength = 0.015f; // Lower default to prevent straying too far
    [Tooltip("React strength to bumps/vertical G-forces (moves up/down).")]
    public float verticalGForceStrength = 0.02f;
    
    public float maxGForceOffset = 1.0f;
    
    [Header("Tilt Effect")]
    [Tooltip("Additional tilt/roll based on cornering G-forces.")]
    public float maxSteerTiltAngle = 10f;
    public float tiltSmoothSpeed = 5f;
    
    private Vector3 currentVelocity;
    private Rigidbody targetRb;
    private Vector3 lastVelocity;
    private Vector3 localGForce;
    private float currentTiltAngle;
    
    void Start()
    {
        CacheRigidbody();
        SnapToTarget();
    }

    void OnValidate()
    {
        CacheRigidbody();
    }

    void CacheRigidbody()
    {
        if (targetCar != null)
        {
            targetRb = targetCar.GetComponentInParent<Rigidbody>();
        }
    }

    public void SnapToTarget()
    {
        if (targetCar == null) return;
        transform.position = targetCar.TransformPoint(localOffset);
        transform.rotation = targetCar.rotation;
        if (targetRb != null)
        {
            lastVelocity = targetRb.linearVelocity;
        }
        localGForce = Vector3.zero;
        currentTiltAngle = 0f;
    }

    void LateUpdate()
    {
        if (targetCar == null) return;

        Vector3 targetPos = targetCar.TransformPoint(localOffset);
        Quaternion targetRot = targetCar.rotation;
        
        if (targetRb != null)
        {
            // Calculate vehicle linear acceleration in world space
            Vector3 currentVel = targetRb.linearVelocity;
            Vector3 acceleration = Time.deltaTime > 0f ? (currentVel - lastVelocity) / Time.deltaTime : Vector3.zero;
            lastVelocity = currentVel;
            
            // Convert acceleration to the local space of the car
            Vector3 localAccel = targetCar.InverseTransformDirection(acceleration);
            
            // Smooth G-force values to filter out extreme spikes
            localGForce = Vector3.Lerp(localGForce, localAccel, 10f * Time.deltaTime);
            
            // Apply axis-specific inertia offset: HUD drifts opposite to vehicle acceleration forces
            Vector3 gOffset = new Vector3(
                -localGForce.x * lateralGForceStrength, 
                -localGForce.y * verticalGForceStrength, 
                -localGForce.z * longitudinalGForceStrength
            );
            gOffset = Vector3.ClampMagnitude(gOffset, maxGForceOffset);
            
            targetPos = targetCar.TransformPoint(localOffset + gOffset);
            
            // Apply dynamic tilt when steering (roll opposite to steering G-forces)
            float targetTilt = -localGForce.x * 0.15f; // Scale factor for visual taste
            targetTilt = Mathf.Clamp(targetTilt, -maxSteerTiltAngle, maxSteerTiltAngle);
            currentTiltAngle = Mathf.Lerp(currentTiltAngle, targetTilt, tiltSmoothSpeed * Time.deltaTime);
            
            targetRot = targetRot * Quaternion.Euler(0f, 0f, currentTiltAngle);
        }

        // Smoothly interpolate position and rotation with velocity lag compensation
        Vector3 compensatedTargetPos = targetPos;
        if (targetRb != null)
        {
            // Feed-forward compensation: shifts target forward by (velocity * smoothTime)
            // to cancel out the natural lag of Vector3.SmoothDamp at constant speeds.
            compensatedTargetPos += targetRb.linearVelocity * posSmoothTime;
        }

        transform.position = Vector3.SmoothDamp(transform.position, compensatedTargetPos, ref currentVelocity, posSmoothTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-Time.deltaTime / rotSmoothTime));
    }
}
