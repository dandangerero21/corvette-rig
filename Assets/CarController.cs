using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.DualShock;

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
    [Tooltip("How aggressively torque falls off with speed. 1.0 = linear falloff, 2.0 = quadratic.")]
    public float torqueFalloffPower = 1.8f;

    [Header("Steering")]
    public float antiRoll = 7000f;
    public float maxAntiRoll = 20000f;
    public float maxSteeringAngle = 35f;
    public float highSpeedSteerAngle = 18f;
    public float steerLimitSpeed = 120f;
    public float maxSteeringSpeed = 6f;
    public float minSteeringSpeed = 1.5f;
    public float aiSteeringSpeed = 8f;
    [Range(0.01f, 0.2f)] public float stickDeadzone = 0.08f;

    private float currentSteerAngle;

    [Header("Brakes")]
    public float brakeForce = 3000f;

    [Header("Physics")]
    public float downforceCoefficient = 5f;
    public Vector3 centerOfMass = new Vector3(0, -0.5f, 0);
    [Range(0f, 1f)] public float rearDownforceBias = 0.70f;

    [Header("AI Control")]
    public bool isAI = false;
    [HideInInspector] public float aiMoveInput = 0f;
    [HideInInspector] public float aiSteerInput = 0f;
    [HideInInspector] public bool aiHandbrakeInput = false;

    private float moveInputBuffer;
    private float steerInputBuffer;
    private bool handbrakeInputBuffer;

    private Rigidbody rb;
    private InputDevice targetAndroidDevice;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = centerOfMass;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        FindAndroidDualShockDevice();
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    void OnDestroy()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected)
        {
            FindAndroidDualShockDevice();
        }
    }

    void FindAndroidDualShockDevice()
    {
        targetAndroidDevice = null;
        foreach (var dev in InputSystem.devices)
        {
            // Specifically target the exact Android aggregate device from the logcat list
            if (dev.layout.Contains("DualShock") ||
                (dev.displayName.Contains("Wireless Controller") && !dev.layout.Contains("Mouse")))
            {
                targetAndroidDevice = dev;
                break;
            }
        }
    }

    void Update()
    {
        if (isAI)
        {
            moveInputBuffer = aiMoveInput;
            steerInputBuffer = aiSteerInput;
            handbrakeInputBuffer = aiHandbrakeInput;
            return;
        }

        bool inputAcquired = false;

        // Route 1: Target the specific Android DualShock device directly (bypassing Gamepad.current null bug)
        if (targetAndroidDevice != null)
        {
            float stickX = ReadAxis(targetAndroidDevice, "leftStick/x", "stick/x", "x");
            float triggerR = ReadAxis(targetAndroidDevice, "rightTrigger", "r2", "z");
            float triggerL = ReadAxis(targetAndroidDevice, "leftTrigger", "l2", "rz");

            bool btnCross = ReadButton(targetAndroidDevice, "buttonSouth", "a");
            bool btnCircle = ReadButton(targetAndroidDevice, "buttonEast", "b");

            steerInputBuffer = Mathf.Abs(stickX) > stickDeadzone ? stickX : 0f;
            moveInputBuffer = Mathf.Clamp01(triggerR) - Mathf.Clamp01(triggerL);
            handbrakeInputBuffer = btnCross || btnCircle;

            inputAcquired = true;
        }

        // Route 2: Standard Gamepad API fallback
        if (!inputAcquired && Gamepad.current != null)
        {
            var pad = Gamepad.current;
            float rawStickX = pad.leftStick.x.ReadValue();
            steerInputBuffer = Mathf.Abs(rawStickX) > stickDeadzone ? rawStickX : 0f;

            float throttle = Mathf.Clamp01(pad.rightTrigger.ReadValue());
            float brake = Mathf.Clamp01(pad.leftTrigger.ReadValue());
            moveInputBuffer = throttle - brake;

            handbrakeInputBuffer = pad.buttonSouth.isPressed || pad.buttonEast.isPressed;
            inputAcquired = true;
        }

        // Route 3: Desktop Keyboard fallback
        if (!inputAcquired)
        {
            moveInputBuffer = Input.GetAxis("Vertical");
            steerInputBuffer = Input.GetAxis("Horizontal");
            handbrakeInputBuffer = Input.GetKey(KeyCode.Space);
        }
    }

    float ReadAxis(InputDevice dev, params string[] controlNames)
    {
        for (int i = 0; i < controlNames.Length; i++)
        {
            var ctrl = dev.TryGetChildControl<AxisControl>(controlNames[i]);
            if (ctrl != null)
            {
                return ctrl.ReadValue();
            }
        }
        return 0f;
    }

    bool ReadButton(InputDevice dev, params string[] controlNames)
    {
        for (int i = 0; i < controlNames.Length; i++)
        {
            var ctrl = dev.TryGetChildControl<ButtonControl>(controlNames[i]);
            if (ctrl != null && ctrl.isPressed)
            {
                return true;
            }
        }
        return false;
    }

    void FixedUpdate()
    {
        float move = moveInputBuffer;
        float rawSteerInput = steerInputBuffer;
        bool handbrake = handbrakeInputBuffer;

        float speedKmH = rb.linearVelocity.magnitude * 3.6f;
        float forwardSpeedKmH = Vector3.Dot(rb.linearVelocity, transform.forward) * 3.6f;

        // SPEED-SENSITIVE STEERING
        float speedFactor = Mathf.Clamp01(speedKmH / steerLimitSpeed);
        float smoothFactor = speedFactor * speedFactor;

        float targetMaxAngle = Mathf.Lerp(maxSteeringAngle, highSpeedSteerAngle, smoothFactor);
        float targetAngle = rawSteerInput * targetMaxAngle;

        float steeringSpeed = isAI
            ? aiSteeringSpeed
            : Mathf.Lerp(maxSteeringSpeed, minSteeringSpeed, smoothFactor);
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetAngle, steeringSpeed * Time.fixedDeltaTime);

        frontLeft.steerAngle = currentSteerAngle;
        frontRight.steerAngle = currentSteerAngle;

        // ENGINE & REVERSE
        if (speedKmH < maxSpeed && move >= 0)
        {
            float speedRatio = Mathf.Clamp01(speedKmH / maxSpeed);
            float torqueMultiplier = Mathf.Pow(1f - speedRatio, torqueFalloffPower);
            torqueMultiplier = Mathf.Max(torqueMultiplier, 0.15f);

            float effectiveTorque = move * motorForce * torqueMultiplier;
            rearLeft.motorTorque = effectiveTorque;
            rearRight.motorTorque = effectiveTorque;
        }
        else if (move < 0)
        {
            if (forwardSpeedKmH > 5f)
            {
                float brakeAmount = Mathf.Abs(move);
                frontLeft.brakeTorque = brakeAmount * brakeForce;
                frontRight.brakeTorque = brakeAmount * brakeForce;
                rearLeft.brakeTorque = brakeAmount * brakeForce * 0.7f;
                rearRight.brakeTorque = brakeAmount * brakeForce * 0.7f;
                rearLeft.motorTorque = 0;
                rearRight.motorTorque = 0;
            }
            else
            {
                rearLeft.motorTorque = move * motorForce * 1.5f;
                rearRight.motorTorque = move * motorForce * 1.5f;

                rearLeft.brakeTorque = 0;
                rearRight.brakeTorque = 0;
                frontLeft.brakeTorque = 0;
                frontRight.brakeTorque = 0;
            }
        }
        else
        {
            rearLeft.motorTorque = 0;
            rearRight.motorTorque = 0;
        }

        // BRAKES & AUTO-HOLD
        if (handbrake)
        {
            rearLeft.brakeTorque = brakeForce;
            rearRight.brakeTorque = brakeForce;
            frontLeft.brakeTorque = brakeForce * 0.5f;
            frontRight.brakeTorque = brakeForce * 0.5f;
            rearLeft.motorTorque = 0;
            rearRight.motorTorque = 0;
        }
        else if (move == 0)
        {
            float autoBrake = speedKmH < 2f ? 150f : 50f;
            rearLeft.brakeTorque = autoBrake;
            rearRight.brakeTorque = autoBrake;
            frontLeft.brakeTorque = 0f;
            frontRight.brakeTorque = 0f;
        }
        else if (move > 0)
        {
            rearLeft.brakeTorque = 0;
            rearRight.brakeTorque = 0;
            frontLeft.brakeTorque = 0;
            frontRight.brakeTorque = 0;
        }

        // DOWNFORCE
        float totalDownforce = downforceCoefficient * rb.linearVelocity.sqrMagnitude;
        float rearForce = totalDownforce * rearDownforceBias;
        float frontForce = totalDownforce * (1f - rearDownforceBias);

        Vector3 frontAxlePos = (frontLeft.transform.position + frontRight.transform.position) * 0.5f;
        Vector3 rearAxlePos = (rearLeft.transform.position + rearRight.transform.position) * 0.5f;

        rb.AddForceAtPosition(-transform.up * frontForce, frontAxlePos);
        rb.AddForceAtPosition(-transform.up * rearForce, rearAxlePos);

        // ANTI-ROLL
        float antiRollAtSpeed = Mathf.Lerp(antiRoll, maxAntiRoll, Mathf.Clamp01(speedKmH / steerLimitSpeed));
        float lowSpeedFactor = Mathf.Clamp01(speedKmH / 5f);
        antiRollAtSpeed *= lowSpeedFactor;

        ApplyAntiRoll(frontLeft, frontRight, antiRollAtSpeed);
        ApplyAntiRoll(rearLeft, rearRight, antiRollAtSpeed);

        // WHEEL MESHES
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
        {
            rb.AddForceAtPosition(left.transform.up * -antiRollForce, left.transform.position);
        }

        if (groundedR)
        {
            rb.AddForceAtPosition(right.transform.up * antiRollForce, right.transform.position);
        }
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