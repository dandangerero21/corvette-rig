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
    public float antiRoll = 7000f;        // Base anti-roll (low speed)
    public float maxAntiRoll = 20000f;    // Anti-roll at full speed (stiff chassis)
    public float maxSteeringAngle = 35f;
    public float highSpeedSteerAngle = 5f;  // Angle at high speed
    public float steerLimitSpeed = 120f;    // Speed (km/h) at which limiting is fully applied
    public float maxSteeringSpeed = 6f;     // How fast steering moves at low speed
    public float minSteeringSpeed = 1.5f;   // How fast steering moves at high speed (sluggish)

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
        // SPEED-SENSITIVE STEERING
        // - Max angle shrinks as speed rises (less angle at high speed)
        // - Steering rate also slows at high speed (physically steers sluggishly)
        // Both prevent snap-roll when turning at high speeds.
        //
        float rawSteerInput = Input.GetAxis("Horizontal");

        // Curve kicks in fully at steerLimitSpeed, not maxSpeed
        float speedFactor = Mathf.Clamp01(speedKmH / steerLimitSpeed);
        float smoothFactor = speedFactor * speedFactor; // Quadratic: gentle at low speed, aggressive at high

        // Reduce max angle
        float targetMaxAngle = Mathf.Lerp(maxSteeringAngle, highSpeedSteerAngle, smoothFactor);
        float targetAngle = rawSteerInput * targetMaxAngle;

        // Reduce steering RATE too — so turning feels heavy and slow at speed
        float steeringSpeed = Mathf.Lerp(maxSteeringSpeed, minSteeringSpeed, smoothFactor);
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
        // ANTI-ROLL (speed-scaled: stiffer chassis at high speed to kill wobble)
        //
        float antiRollAtSpeed = Mathf.Lerp(antiRoll, maxAntiRoll, Mathf.Clamp01(speedKmH / steerLimitSpeed));
        ApplyAntiRoll(frontLeft, frontRight, antiRollAtSpeed);
        ApplyAntiRoll(rearLeft, rearRight, antiRollAtSpeed);

        //
        // UPDATE WHEEL MESHES
        //
        UpdateWheel(frontLeft, wheelFL);
        UpdateWheel(frontRight, wheelFR);
        UpdateWheel(rearLeft, wheelRL);
        UpdateWheel(rearRight, wheelRR);
    }

    void ApplyAntiRoll(WheelCollider left, WheelCollider right, float rollForce)
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

        float antiRollForce = (travelL - travelR) * rollForce;

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