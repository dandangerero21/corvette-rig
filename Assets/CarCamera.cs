using UnityEngine;

public class CarCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;           // Drag your car root here
    public Vector3 pivotOffset = new Vector3(0, 0, 2.5f); // Shift target point (Z = positive is front of car)

    [Header("Position")]
    public Vector3 offset = new Vector3(0, 1.2f, -1f);  // Close offset
    public float positionDamping = 10f; // Snappy for close cams

    [Header("Rotation")]
    public float rotationDamping = 8f; 

    [Header("Look Ahead")]
    public float lookAheadDistance = 5f; 

    private Vector3 currentVelocity;
    private Rigidbody targetRb;

    void Start()
    {
        CacheRigidbody();
    }

    void OnValidate()
    {
        CacheRigidbody();
    }

    void CacheRigidbody()
    {
        if (target != null)
        {
            // Find Rigidbody on target or its parents
            targetRb = target.GetComponentInParent<Rigidbody>();
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Ignores pitch/roll of the car for camera comfort
        Quaternion flatRotation = Quaternion.Euler(0, target.eulerAngles.y, 0);

        // Follow point shifted by the pivotOffset
        Vector3 followPoint = target.position + flatRotation * pivotOffset;

        // Get target velocity to compensate for SmoothDamp lag (Lag = velocity * smoothTime)
        Vector3 targetVelocity = Vector3.zero;
        if (targetRb != null)
        {
            targetVelocity = targetRb.linearVelocity;
        }

        float smoothTime = 1f / positionDamping;
        
        // Feed-forward compensation: shifts the target point forward based on speed
        Vector3 compensatedFollowPoint = followPoint + targetVelocity * smoothTime;

        // Desired camera position relative to the compensated follow point
        Vector3 desiredPosition = compensatedFollowPoint + flatRotation * offset;

        // Smooth move to desired position
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref currentVelocity,
            smoothTime
        );

        // Look ahead relative to the follow point
        Vector3 lookTarget = followPoint + target.forward * lookAheadDistance;
        Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationDamping * Time.deltaTime);
    }
}
