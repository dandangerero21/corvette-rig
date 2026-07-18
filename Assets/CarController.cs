using UnityEngine;
using UnityEngine.InputSystem;

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
    public float motorForce = 1500f;
    public float maxSpeed = 250f;
    [Tooltip("How aggressively torque falls off with speed. 1.0 = linear falloff, 2.0 = quadratic (snappy low-end, lazy top-end). Higher = more realistic.")]
    public float torqueFalloffPower = 1.8f;

    [Header("Steering")]
    public float antiRoll = 7000f;        // Base anti-roll (low speed)
    public float maxAntiRoll = 20000f;    // Anti-roll at full speed (stiff chassis)
    public float maxSteeringAngle = 35f;
    public float highSpeedSteerAngle = 18f; // Angle at high speed (was 5° — too low for hairpins)
    public float steerLimitSpeed = 120f;    // Speed (km/h) at which limiting is fully applied
    public float maxSteeringSpeed = 6f;     // How fast steering moves at low speed
    public float minSteeringSpeed = 1.5f;   // How fast steering moves at high speed (sluggish, human feel)
    public float aiSteeringSpeed = 8f;      // AI-only steering rate — bypasses minSteeringSpeed so wheels
                                            // reach their target before the car overshoots the corner

    private float currentSteerAngle;

    [Header("Brakes")]
    public float brakeForce = 3000f;

    [Header("Physics")]
    public float downforceCoefficient = 5f;
    public Vector3 centerOfMass = new Vector3(0, -0.5f, 0); // -1 often clips into the floor
    [Tooltip("0 = all downforce at front (nosedive), 1 = all at rear (wheelie). 0.65–0.75 prevents nosedive at speed.")]
    [Range(0f, 1f)] public float rearDownforceBias = 0.70f; // Rear-biased to resist nosedive

    [Header("AI Control")]
    public bool isAI = false;
    [HideInInspector] public float aiMoveInput = 0f;
    [HideInInspector] public float aiSteerInput = 0f;
    [HideInInspector] public bool aiHandbrakeInput = false;

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
        float move = 0f;
        float rawSteerInput = 0f;
        bool handbrake = false;

        // Hybrid Input System: Checks for Gamepad first, falls back to Keyboard
        if (isAI)
        {
            move = aiMoveInput;
            rawSteerInput = aiSteerInput;
            handbrake = aiHandbrakeInput;
        }
        else if (Gamepad.current != null)
        {
            // Left Stick X for steering
            rawSteerInput = Gamepad.current.leftStick.x.ReadValue();

            // Right Trigger (R2) for Gas, Left Trigger (L2) for Brake/Reverse
            float throttle = Gamepad.current.rightTrigger.ReadValue();
            float brake = Gamepad.current.leftTrigger.ReadValue();
            move = throttle - brake;

            // Cross (South) or Circle (East) button for Handbrake
            handbrake = Gamepad.current.buttonSouth.isPressed || Gamepad.current.buttonEast.isPressed;
        }
        else
        {
            // Keyboard Fallback
            move = Input.GetAxis("Vertical");
            rawSteerInput = Input.GetAxis("Horizontal");
            handbrake = Input.GetKey(KeyCode.Space);
        }

        float speedKmH = rb.linearVelocity.magnitude * 3.6f;

        //
        // SPEED-SENSITIVE STEERING
        // - Max angle shrinks as speed rises (less angle at high speed)
        // - Steering rate also slows at high speed (physically steers sluggishly)
        // Both prevent snap-roll when turning at high speeds.
        //
        // Curve kicks in fully at steerLimitSpeed, not maxSpeed
        float speedFactor = Mathf.Clamp01(speedKmH / steerLimitSpeed);
        float smoothFactor = speedFactor * speedFactor; // Quadratic: gentle at low speed, aggressive at high

        // Reduce max angle
        float targetMaxAngle = Mathf.Lerp(maxSteeringAngle, highSpeedSteerAngle, smoothFactor);
        float targetAngle = rawSteerInput * targetMaxAngle;

        // Reduce steering RATE too — so turning feels heavy and slow at speed
        // AI uses a fixed faster rate so it can physically reach the target angle
        // before overshooting the corner; the human rate-limiter is kept for players.
        float steeringSpeed = isAI
            ? aiSteeringSpeed
            : Mathf.Lerp(maxSteeringSpeed, minSteeringSpeed, smoothFactor);
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetAngle, steeringSpeed * Time.fixedDeltaTime);

        frontLeft.steerAngle = currentSteerAngle;
        frontRight.steerAngle = currentSteerAngle;

        //
        // ENGINE
        //
        if (speedKmH < maxSpeed && move >= 0)
        {
            // Torque falls off with speed: full power at 0 km/h, nearly zero at maxSpeed.
            // This mimics real gear ratios + aero drag — punchy off the line, sluggish at top speed.
            float speedRatio = Mathf.Clamp01(speedKmH / maxSpeed);
            float torqueMultiplier = Mathf.Pow(1f - speedRatio, torqueFalloffPower);
            // Ensure a small residual (~15%) so the car can still creep towards its absolute max
            torqueMultiplier = Mathf.Max(torqueMultiplier, 0.15f);

            float effectiveTorque = move * motorForce * torqueMultiplier;
            rearLeft.motorTorque = effectiveTorque;
            rearRight.motorTorque = effectiveTorque;
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
        // BRAKES & AUTO-HOLD
        //
        if (handbrake)
        {
            rearLeft.brakeTorque = brakeForce;
            rearRight.brakeTorque = brakeForce;
            frontLeft.brakeTorque = brakeForce * 0.5f; // Apply some front brakes too for handbrake stability
            frontRight.brakeTorque = brakeForce * 0.5f;
            rearLeft.motorTorque = 0; // Don't gas and brake simultaneously 
            rearRight.motorTorque = 0;
        }
        else if (move == 0)
        {
            // Auto-braking: Applies drag torque if rolling, and locks wheels if stationary to prevent sliding/creeping.
            float autoBrake = speedKmH < 2f ? 600f : 50f; // 600Nm torque when stationary holds it firmly, 50Nm engine brake
            rearLeft.brakeTorque = autoBrake;
            rearRight.brakeTorque = autoBrake;
            frontLeft.brakeTorque = autoBrake * 0.5f;
            frontRight.brakeTorque = autoBrake * 0.5f;
        }
        else
        {
            rearLeft.brakeTorque = 0;
            rearRight.brakeTorque = 0;
            frontLeft.brakeTorque = 0;
            frontRight.brakeTorque = 0;
        }

        //
        // DOWNFORCE (Quadratic, as physics intended)
        // Split between front and rear axles to control pitch attitude.
        // rearDownforceBias = 0.7 means 70% goes to rear → resists nosedive.
        //
        float totalDownforce = downforceCoefficient * rb.linearVelocity.sqrMagnitude;
        float rearForce  = totalDownforce * rearDownforceBias;
        float frontForce = totalDownforce * (1f - rearDownforceBias);

        Vector3 frontAxlePos = (frontLeft.transform.position + frontRight.transform.position) * 0.5f;
        Vector3 rearAxlePos  = (rearLeft.transform.position  + rearRight.transform.position)  * 0.5f;

        rb.AddForceAtPosition(-transform.up * frontForce, frontAxlePos);
        rb.AddForceAtPosition(-transform.up * rearForce,  rearAxlePos);

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