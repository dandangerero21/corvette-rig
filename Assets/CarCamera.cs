using UnityEngine;

public class CarCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;           // Drag your car root here

    [Header("Position")]
    public Vector3 offset = new Vector3(0, 2.5f, -6f);  // Behind and above
    public float positionDamping = 5f; // Higher = snappier, lower = floaty

    [Header("Rotation")]
    public float rotationDamping = 4f; // How fast camera rotates to match car direction

    [Header("Look Ahead")]
    public float lookAheadDistance = 3f; // Camera looks slightly ahead of the car

    private Vector3 currentVelocity;

    void LateUpdate()
    {
        if (target == null) return;

        // Desired position: offset rotated by car's Y rotation (ignores car pitch/roll)
        Quaternion flatRotation = Quaternion.Euler(0, target.eulerAngles.y, 0);
        Vector3 desiredPosition = target.position + flatRotation * offset;

        // Smooth move to desired position
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref currentVelocity,
            1f / positionDamping
        );

        // Look at car + slight ahead offset so camera leads the movement
        Vector3 lookTarget = target.position + target.forward * lookAheadDistance;
        Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationDamping * Time.deltaTime);
    }
}
