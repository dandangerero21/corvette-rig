using UnityEngine;

public class CarController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider frontLeft;
    public WheelCollider frontRight;
    public WheelCollider rearLeft;
    public WheelCollider rearRight;

    [Header("Wheel Meshes")]
    public Transform wheelFL;
    public Transform wheelFR;
    public Transform wheelRL;
    public Transform wheelRR;

    [Header("Engine")]
    public float motorForce = 1500f; // 15000 was absurdly high unless your mass is 20,000kg
    public float maxSpeed = 250f;    // 1000 km/h is Mach 0.8. Let's be reasonable.

    [Header("Steering")]
    public float antiRoll = 5000f;
    public float maxSteeringAngle = 35f;
    public float highSpeedSteerAngle = 10f; // Angle at max speed
    public float steeringSpeed = 5f;

    private float currentSteerAngle;

    [Header("Brakes")]
    public float brakeForce = 3000f;

    [Header("Physics")]
    public float downforceCoefficient = 5f;
    public Vector3 centerOfMass = new Vector3(0, -0.5f, 0); // -1 often clips into the floor

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = centerOfMass;

        // Let Unity handle these properly without aggressive manual damping
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    void FixedUpdate()
    {
        float move = Input.GetAxis("Vertical");
        float speedKmH = rb.linearVelocity.magnitude * 3.6f;

        //
        // SPEED-SENSITIVE STEERING (Done Once, Properly)
        //
        float rawSteerInput = Input.GetAxis("Horizontal");
        float speedFactor = Mathf.Clamp01(speedKmH / maxSpeed);

        float targetMaxAngle = Mathf.Lerp(maxSteeringAngle, highSpeedSteerAngle, speedFactor);
        float targetAngle = rawSteerInput * targetMaxAngle;

        currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetAngle, steeringSpeed * Time.fixedDeltaTime);

        frontLeft.steerAngle = currentSteerAngle;
        frontRight.steerAngle = currentSteerAngle;

        //
        // ENGINE
        //
        if (speedKmH < maxSpeed && move >= 0)
        {
            rearLeft.motorTorque = move * motorForce;
            rearRight.motorTorque = move * motorForce;
        }
        else if (move < 0) // Reverse
        {
            rearLeft.motorTorque = move * motorForce;
            rearRight.motorTorque = move * motorForce;
        }
        else
        {
            rearLeft.motorTorque = 0;
            rearRight.motorTorque = 0;
        }

        //
        // BRAKES
        //
        if (Input.GetKey(KeyCode.Space))
        {
            rearLeft.brakeTorque = brakeForce;
            rearRight.brakeTorque = brakeForce;
            rearLeft.motorTorque = 0; // Don't gas and brake simultaneously 
            rearRight.motorTorque = 0;
        }
        else
        {
            rearLeft.brakeTorque = 0;
            rearRight.brakeTorque = 0;
        }

        //
        // DOWNFORCE (Quadratic, as physics intended)
        //
        float lift = -downforceCoefficient * rb.linearVelocity.sqrMagnitude;
        rb.AddForce(transform.up * lift);

        //
        // ANTI-ROLL
        //
        ApplyAntiRoll(frontLeft, frontRight);
        ApplyAntiRoll(rearLeft, rearRight);

        //
        // UPDATE WHEEL MESHES
        //
        UpdateWheel(frontLeft, wheelFL);
        UpdateWheel(frontRight, wheelFR);
        UpdateWheel(rearLeft, wheelRL);
        UpdateWheel(rearRight, wheelRR);
    }

    void ApplyAntiRoll(WheelCollider left, WheelCollider right)
    {
        WheelHit hit;
        float travelL = 1.0f;
        float travelR = 1.0f;

        bool groundedL = left.GetGroundHit(out hit);
        if (groundedL)
        {
            travelL = (-left.transform.InverseTransformPoint(hit.point).y - left.radius) / left.suspensionDistance;
        }

        bool groundedR = right.GetGroundHit(out hit);
        if (groundedR)
        {
            travelR = (-right.transform.InverseTransformPoint(hit.point).y - right.radius) / right.suspensionDistance;
        }

        float antiRollForce = (travelL - travelR) * antiRoll;

        if (groundedL)
            rb.AddForceAtPosition(left.transform.up * -antiRollForce, left.transform.position);

        if (groundedR)
            rb.AddForceAtPosition(right.transform.up * antiRollForce, right.transform.position);
    }

    void UpdateWheel(WheelCollider collider, Transform wheelMesh)
    {
        collider.GetWorldPose(out Vector3 pos, out Quaternion rot);
        wheelMesh.position = pos;

        if (wheelMesh == wheelFR || wheelMesh == wheelRR)
        {
            wheelMesh.rotation = rot * Quaternion.Euler(0, 180, 0);
        }
        else
        {
            wheelMesh.rotation = rot;
        }
    }
}