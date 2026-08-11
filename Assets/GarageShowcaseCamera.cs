using UnityEngine;

public class GarageShowcaseCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Drag your Corvette GameObject here")]
    public Transform target;

    [Tooltip("Target focus offset (Z positive moves focus point forward toward headlights)")]
    public Vector3 targetOffset = new Vector3(0f, 0.65f, 0.8f);

    [Header("Camera Distance & Height")]
    [Tooltip("Distance from the car center")]
    public float distance = 3.8f;

    [Tooltip("Height of camera relative to ground/car center (0.65 = Headlight height)")]
    public float height = 0.65f;

    [Header("Orbit Animation")]
    [Tooltip("Enable continuous slow rotation around the car")]
    public bool autoRotate = true;

    [Tooltip("Speed of auto rotation in degrees per second")]
    public float autoRotateSpeed = 12f;

    [Header("Headlight Sweep Mode (Optional)")]
    [Tooltip("If true, sweeps back and forth around the front headlights instead of full 360 rotation")]
    public bool sweepHeadlightOnly = false;

    [Tooltip("Max angle offset for headlight sweep mode (e.g. 45 degrees left/right from front)")]
    public float sweepAngleMax = 45f;

    [Tooltip("Speed of headlight sweep oscillation")]
    public float sweepSpeed = 0.5f;

    [Header("Mouse Controls")]
    [Tooltip("Allow player to click and drag to rotate manually")]
    public bool allowMouseControl = true;
    public float mouseSensitivity = 3f;

    [Header("Smoothing")]
    public float smoothSpeed = 5f;

    private float currentAngle = 30f;
    private float currentPitch = 5f; // Slight low pitch aiming up at car

    void Start()
    {
        if (target != null && !sweepHeadlightOnly)
        {
            // Initial angle facing 3/4 front of car
            currentAngle = target.eulerAngles.y - 35f;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Auto Rotation or Sweep
        if (autoRotate && (!allowMouseControl || !Input.GetMouseButton(0)))
        {
            if (sweepHeadlightOnly)
            {
                float baseFrontAngle = target.eulerAngles.y;
                float offsetAngle = Mathf.Sin(Time.time * sweepSpeed) * sweepAngleMax;
                currentAngle = baseFrontAngle + offsetAngle;
            }
            else
            {
                currentAngle += autoRotateSpeed * Time.deltaTime;
            }
        }

        // Mouse Drag Orbit Control
        if (allowMouseControl && Input.GetMouseButton(0))
        {
            currentAngle += Input.GetAxis("Mouse X") * mouseSensitivity;
            currentPitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            currentPitch = Mathf.Clamp(currentPitch, -5f, 25f);
        }

        // Calculate focus target position (headlight level)
        Vector3 focusPoint = target.position + target.rotation * targetOffset;

        // Calculate desired camera position
        Quaternion rotation = Quaternion.Euler(currentPitch, currentAngle, 0);
        Vector3 negDistance = new Vector3(0.0f, 0.0f, -distance);
        Vector3 desiredPosition = focusPoint + (rotation * negDistance);
        desiredPosition.y = target.position.y + height;

        // Smooth move and look at target
        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * smoothSpeed);
        
        // Aim camera directly at the focus point (headlight level)
        Quaternion lookRotation = Quaternion.LookRotation(focusPoint - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * smoothSpeed);
    }
}
