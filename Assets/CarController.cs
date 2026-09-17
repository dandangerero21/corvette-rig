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
    public float maxSteeringSpeed = 10f;
    public float minSteeringSpeed = 3f;
    public float aiSteeringSpeed = 8f;
    [Range(0.01f, 0.2f)] public float stickDeadzone = 0.08f;

    [Header("Controller Mapping")]
    [Tooltip("If true: L2 = Accelerate (Forward), R2 = Brake / Reverse. If false: R2 = Gas, L2 = Brake.")]
    public bool l2AccelerateR2Brake = true;

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

    public float MoveInput => moveInputBuffer;
    public float SteerInput => steerInputBuffer;
    public bool HandbrakeInput => handbrakeInputBuffer;

    private float moveInputBuffer;
    private float steerInputBuffer;
    private bool handbrakeInputBuffer;

    private Rigidbody rb;
    private string connectedControllerName = null;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = centerOfMass;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Auto-instantiate TouchControlsCanvas for mobile phone if not present
        if (!isAI && TouchControls.Instance == null)
        {
            GameObject touchCanvasObj = new GameObject("TouchControlsCanvas");
            touchCanvasObj.AddComponent<TouchControls>();
        }
    }

    private bool ReadControllerInputs(out float steer, out float throttle, out float brake, out bool handbrake)
    {
        steer = 0f;
        throttle = 0f;
        brake = 0f;
        handbrake = false;
        connectedControllerName = null;

        // 1. Scan all devices in InputSystem.devices
        for (int i = 0; i < InputSystem.devices.Count; i++)
        {
            var dev = InputSystem.devices[i];
            if (dev == null || !dev.added) continue;
            if (dev is Mouse || dev is Sensor || (dev is Touchscreen && !(dev is Gamepad))) continue;

            string dName = (dev.displayName + " " + dev.layout).ToLower();
            if (dName.Contains("mouse") || dName.Contains("sensor") || (dName.Contains("touchscreen") && !dName.Contains("controller"))) continue;

            bool isGamepadDev = dev is Gamepad || dev is Joystick ||
                                dName.Contains("controller") || dName.Contains("wireless") ||
                                dName.Contains("dualshock") || dName.Contains("gamepad") || dName.Contains("joystick");

            if (isGamepadDev && string.IsNullOrEmpty(connectedControllerName))
            {
                connectedControllerName = dev.displayName;
            }

            float curSteer = 0f;
            float curThrottle = 0f;
            float curBrake = 0f;
            bool curHb = false;

            // --- A. Left Stick Steering ---
            if (dev is Gamepad pad)
            {
                Vector2 ls = pad.leftStick.ReadValue();
                if (Mathf.Abs(ls.x) > stickDeadzone) curSteer = ls.x;
                else if (Mathf.Abs(pad.dpad.ReadValue().x) > 0.05f) curSteer = pad.dpad.ReadValue().x;
            }
            else if (dev is Joystick joy)
            {
                Vector2 s = joy.stick.ReadValue();
                if (Mathf.Abs(s.x) > stickDeadzone) curSteer = s.x;
            }

            if (Mathf.Abs(curSteer) < 0.01f)
            {
                var vStick = dev.TryGetChildControl<Vector2Control>("leftStick") ?? dev.TryGetChildControl<Vector2Control>("stick");
                if (vStick != null)
                {
                    Vector2 v = vStick.ReadValue();
                    if (Mathf.Abs(v.x) > stickDeadzone) curSteer = v.x;
                }
            }

            if (Mathf.Abs(curSteer) < 0.01f)
            {
                var ax = dev.TryGetChildControl<AxisControl>("leftStick/x") ??
                         dev.TryGetChildControl<AxisControl>("stick/x") ??
                         dev.TryGetChildControl<AxisControl>("horizontal");
                if (ax != null && !(ax is ButtonControl))
                {
                    float val = ax.ReadValue();
                    if (Mathf.Abs(val) > stickDeadzone) curSteer = val;
                }
            }

            // --- B. L2 (Throttle / Move Forward) ---
            if (dev is Gamepad g1)
            {
                curThrottle = g1.leftTrigger.ReadValue();
                if (curThrottle < 0.05f && g1.leftTrigger.isPressed) curThrottle = 1f;
            }

            if (curThrottle < 0.05f)
            {
                var axL2 = dev.TryGetChildControl<AxisControl>("leftTrigger") ??
                           dev.TryGetChildControl<AxisControl>("l2") ??
                           dev.TryGetChildControl<AxisControl>("triggerL") ??
                           dev.TryGetChildControl<AxisControl>("brake"); // Linux/Android hid-sony uses AXIS_BRAKE for L2
                if (axL2 != null && !(axL2 is ButtonControl))
                {
                    float v = axL2.ReadValue();
                    if (v > 0.05f) curThrottle = Mathf.Clamp01(v);
                }

                var btnL2 = dev.TryGetChildControl<ButtonControl>("leftTrigger") ??
                            dev.TryGetChildControl<ButtonControl>("l2") ??
                            dev.TryGetChildControl<ButtonControl>("buttonL2") ??
                            dev.TryGetChildControl<ButtonControl>("button104") ??
                            dev.TryGetChildControl<ButtonControl>("leftShoulder");
                if (btnL2 != null && btnL2.isPressed) curThrottle = 1f;
            }

            // --- C. R2 (Brake / Reverse) ---
            if (dev is Gamepad g2)
            {
                curBrake = g2.rightTrigger.ReadValue();
                if (curBrake < 0.05f && g2.rightTrigger.isPressed) curBrake = 1f;
            }

            if (curBrake < 0.05f)
            {
                var axR2 = dev.TryGetChildControl<AxisControl>("rightTrigger") ??
                           dev.TryGetChildControl<AxisControl>("r2") ??
                           dev.TryGetChildControl<AxisControl>("triggerR") ??
                           dev.TryGetChildControl<AxisControl>("gas"); // Linux/Android hid-sony uses AXIS_GAS for R2
                if (axR2 != null && !(axR2 is ButtonControl))
                {
                    float v = axR2.ReadValue();
                    if (v > 0.05f) curBrake = Mathf.Clamp01(v);
                }

                var btnR2 = dev.TryGetChildControl<ButtonControl>("rightTrigger") ??
                            dev.TryGetChildControl<ButtonControl>("r2") ??
                            dev.TryGetChildControl<ButtonControl>("buttonR2") ??
                            dev.TryGetChildControl<ButtonControl>("button105") ??
                            dev.TryGetChildControl<ButtonControl>("rightShoulder");
                if (btnR2 != null && btnR2.isPressed) curBrake = 1f;
            }

            // --- D. Circle (Handbrake) ---
            if (dev is Gamepad g3)
            {
                curHb = g3.buttonEast.isPressed;
            }
            if (!curHb)
            {
                var btnEast = dev.TryGetChildControl<ButtonControl>("buttonEast") ??
                              dev.TryGetChildControl<ButtonControl>("circle") ??
                              dev.TryGetChildControl<ButtonControl>("b") ??
                              dev.TryGetChildControl<ButtonControl>("button97");
                if (btnEast != null && btnEast.isPressed) curHb = true;
            }

            if (Mathf.Abs(curSteer) > 0.01f || curThrottle > 0.01f || curBrake > 0.01f || curHb)
            {
                steer = curSteer;
                throttle = curThrottle;
                brake = curBrake;
                handbrake = curHb;
                connectedControllerName = dev.displayName;
                return true;
            }
        }

        // 2. Legacy Input Fallback (if activeInputHandler is Both)
        try
        {
            string[] jNames = Input.GetJoystickNames();
            if (jNames != null && jNames.Length > 0 && !string.IsNullOrEmpty(jNames[0]))
            {
                if (string.IsNullOrEmpty(connectedControllerName))
                    connectedControllerName = jNames[0];

                float legH = Input.GetAxis("Horizontal");
                if (Mathf.Abs(legH) > stickDeadzone) steer = legH;

                if (Input.GetKey(KeyCode.JoystickButton6) || Input.GetKey(KeyCode.JoystickButton4)) throttle = 1f;
                if (Input.GetKey(KeyCode.JoystickButton7) || Input.GetKey(KeyCode.JoystickButton5)) brake = 1f;
                if (Input.GetKey(KeyCode.JoystickButton1) || Input.GetKey(KeyCode.JoystickButton2)) handbrake = true;

                if (Mathf.Abs(steer) > 0.01f || throttle > 0.01f || brake > 0.01f || handbrake)
                {
                    return true;
                }
            }
        }
        catch { }

        return !string.IsNullOrEmpty(connectedControllerName);
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

        // 1. Controller Input
        bool isControllerConnected = ReadControllerInputs(out float padSteer, out float padThrottle, out float padBrake, out bool padHb);

        // 2. Touchscreen Controls
        float touchSteer = 0f;
        float touchThrottle = 0f;
        float touchBrake = 0f;
        bool touchHb = false;

        if (TouchControls.Instance != null)
        {
            touchSteer = TouchControls.Instance.GetSteerInput();
            touchThrottle = TouchControls.Instance.GetThrottleInput();
            touchBrake = TouchControls.Instance.GetBrakeInput();
            touchHb = TouchControls.Instance.GetHandbrakeInput();

            string diag;
            if (!string.IsNullOrEmpty(connectedControllerName))
            {
                diag = $"[CONTROLLER: {connectedControllerName}] L2: {padThrottle:F2} | R2: {padBrake:F2} | Steer: {padSteer:F2}";
            }
            else
            {
                diag = "[TOUCH ACTIVE] No Controller Found (Check Bluetooth / Accessibility)";
            }

            TouchControls.Instance.UpdateDiagnostics(diag);
        }

        // 3. Desktop Keyboard Fallback
        float kbSteer = 0f;
        float kbThrottle = 0f;
        float kbBrake = 0f;
        bool kbHb = false;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) kbSteer -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) kbSteer += 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) kbThrottle = 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) kbBrake = 1f;
            if (Keyboard.current.spaceKey.isPressed) kbHb = true;
        }

        // Combine inputs seamlessly
        float finalSteer = Mathf.Clamp(padSteer + touchSteer + kbSteer, -1f, 1f);
        float finalThrottle = Mathf.Max(padThrottle, touchThrottle, kbThrottle);
        float finalBrake = Mathf.Max(padBrake, touchBrake, kbBrake);
        bool finalHb = padHb || touchHb || kbHb;

        steerInputBuffer = finalSteer;
        moveInputBuffer = Mathf.Clamp(finalThrottle - finalBrake, -1f, 1f);
        handbrakeInputBuffer = finalHb;
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