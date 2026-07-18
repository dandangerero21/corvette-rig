using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CarController))]
public class CarAudio : MonoBehaviour
{
    [Header("Audio Clips")]
    [Tooltip("Looping engine idle/rev sound clip.")]
    public AudioClip engineClip;
    [Tooltip("Looping tire screech/skid sound clip.")]
    public AudioClip skidClip;
    [Tooltip("One-shot lap completion sound clip.")]
    public AudioClip lapCompleteClip;

    [Header("Engine Tuning")]
    public float minPitch = 0.48f;      // Idle pitch — lower = deeper V8 lope
    public float maxPitch = 1.68f;      // Redline pitch — SAME for every gear
    public float baseVolume = 0.70f;    // Volume at idle
    public float maxVolume = 1.0f;      // Volume at redline
    [Tooltip("Extra linear volume multiplier applied on top. Raise this if the engine is still too quiet.")]
    public float volumeBoost = 1.4f;
    public float pitchSmoothSpeed = 3.2f;
    [Range(0f, 1f)]
    [Tooltip("0 = full 2D (loud and present), 1 = full 3D. Keep at 0 for maximum volume.")]
    public float engineSpatialBlend = 0.0f;

    [Header("Low-Pass Filter (Depth)")]
    [Tooltip("Cutoff frequency (Hz) at idle. Lower = more bassy/muffled rumble. 800-1200 is a good V8 idle.")]
    public float filterCutoffIdle    = 900f;
    [Tooltip("Cutoff frequency (Hz) at full RPM. Should be near 22000 to let all frequencies through at redline.")]
    public float filterCutoffRedline = 18000f;
    [Tooltip("How quickly the filter opens/closes (higher = faster response).")]
    public float filterSmoothSpeed   = 4f;

    [Header("Gear RPM Shape")]
    [Tooltip("The RPM (0-1) at which 1st gear begins. Keep low so 1st gear has a wide rev range.")]
    public float baseGearRpm = 0.10f;
    [Tooltip("The RPM (0-1) at which the highest gear begins. Higher = higher idle tone in top gears.")]
    public float topGearBaseRpm = 0.68f;

    [Header("Transmission")]
    [Tooltip("Number of forward gears.")]
    public int numberOfGears = 8;
    [Tooltip("Speed (km/h) at which the TOP GEAR ends. Set to 0 to auto-read from the car's max speed. " +
             "Auto mode uses 1.05x max speed so all gears fit tightly within your top speed.")]
    public float transmissionTopSpeed = 0f;

    [Header("Gear Shift Timing")]
    [Tooltip("How long it takes to shift in 1st gear. Grows exponentially for each higher gear.")]
    public float baseUpshiftDuration   = 0.55f;
    public float baseDownshiftDuration = 0.65f;
    [Tooltip("How much longer each successive shift takes. 1.25 = 25% longer per gear.")]
    public float shiftDurationGrowthFactor = 1.25f;
    [Tooltip("The RPM level (0-1) the engine spikes to during a downshift blip.")]
    public float blipRpmTarget = 0.70f;
    [Tooltip("At max speed, how long does the engine hold before attempting another overdrive upshift (seconds, grows per overdrive gear).")]
    public float overdriveRevDuration = 3.5f;
    [Tooltip("Growth factor per overdrive gear (e.g. 1.3 = 30% longer each overdrive shift).")]
    public float overdriveRevGrowth = 1.30f;
    
    [Header("Skid Tuning")]
    public float skidThreshold = 0.28f; // Slip value at which screeching starts
    public float maxSkidVolume = 0.7f;

    private CarController carController;
    private Rigidbody rb;

    private AudioSource engineSource;
    private AudioSource engineSource2; // Dual source for +6dB volume boost!
    private AudioSource skidSource;
    private AudioSource hudSource;
    private AudioLowPassFilter engineFilter; // RPM-modulated low-pass for deep idle tone

    private float lastSpeedKmH;
    private float currentEnginePitch = 0.60f;
    
    public enum ShiftState { Driving, ShiftingUp, ShiftingDown }
    private ShiftState currentShiftState = ShiftState.Driving;
    private float shiftTimer = 0f;
    private float activeShiftDuration = 0.2f;
    private int currentGear = 1;
    private int lastGear = 1;

    // Overdrive: virtual gears stacked on top once physical top speed is reached
    private int   overdriveGear = 0;
    private float overdriveRpm  = 0f;
    private bool  inOverdrive   = false;

    // Auto-calculated gear speed table — built once in Start()
    private float[] gearSpeeds;

    private WheelCollider[] wheelsCache;

    void Awake()
    {
        carController = GetComponent<CarController>();
        rb = GetComponent<Rigidbody>();

        // Cache the wheels list to completely eliminate garbage collection allocations in Update()
        if (carController != null)
        {
            wheelsCache = new WheelCollider[] { 
                carController.frontLeft, 
                carController.frontRight, 
                carController.rearLeft, 
                carController.rearRight 
            };
        }

        SetupAudioSources();
    }

    void OnEnable()
    {
        // Subscribe to the lap completion event to play HUD chime
        Speedometer.OnLapCompleted += PlayLapChime;
    }

    void OnDisable()
    {
        // Unsubscribe to avoid memory leaks
        Speedometer.OnLapCompleted -= PlayLapChime;
    }

    void SetupAudioSources()
    {
        // 1. Engine Audio Source (Looping 3D Spatial Audio at exhaust position)
        engineSource = gameObject.AddComponent<AudioSource>();
        engineSource.clip = engineClip;
        engineSource.loop = true;
        engineSource.spatialBlend = engineSpatialBlend; // Mostly 2D for chase cam loudness and punch
        engineSource.minDistance = 3f;
        engineSource.maxDistance = 40f;
        engineSource.playOnAwake = false;

        // 1b. Second Engine Audio Source (Dual source for +6dB volume boost!)
        engineSource2 = gameObject.AddComponent<AudioSource>();
        engineSource2.clip = engineClip;
        engineSource2.loop = true;
        engineSource2.spatialBlend = engineSpatialBlend;
        engineSource2.minDistance = 3f;
        engineSource2.maxDistance = 40f;
        engineSource2.playOnAwake = false;

        // 1c. Low-pass filter on engineSource for deep bassy idle rumble
        //     At idle: only low frequencies pass (deep V8 throb)
        //     At redline: filter fully open (all harmonics, full scream)
        engineFilter = engineSource.gameObject.AddComponent<AudioLowPassFilter>();
        engineFilter.cutoffFrequency = filterCutoffIdle;
        engineFilter.lowpassResonanceQ = 1.2f; // Slight resonance bump adds presence

        // 2. Skid Audio Source (Looping 3D Spatial Audio)
        skidSource = gameObject.AddComponent<AudioSource>();
        skidSource.clip = skidClip;
        skidSource.loop = true;
        skidSource.spatialBlend = 1.0f; // Full 3D
        skidSource.minDistance = 2f;
        skidSource.maxDistance = 30f;
        skidSource.playOnAwake = false;

        // 3. HUD Notification Audio Source (2D Stereo Chime)
        hudSource = gameObject.AddComponent<AudioSource>();
        hudSource.clip = lapCompleteClip;
        hudSource.loop = false;
        hudSource.spatialBlend = 0.0f; // Full 2D Stereo (ignores distance/position)
        hudSource.playOnAwake = false;
    }

    void Start()
    {
        BuildGearTable();
        currentEnginePitch = minPitch;
        if (engineClip != null)
        {
            engineSource.Play();
            if (engineSource2 != null) engineSource2.Play();
        }
        else
        {
            Debug.LogWarning($"[Car Audio] No engine audio clip assigned on '{gameObject.name}'!");
        }
    }

    void BuildGearTable()
    {
        // Determine the top speed to use for the gear table.
        float topSpeed = transmissionTopSpeed;
        if (topSpeed <= 0f)
        {
            float carMaxSpeed = carController != null ? carController.maxSpeed : 250f;
            // 1.05x so 8th gear ends just past the car's physical limit.
            // At max speed the car sits near the top of its final gear.
            topSpeed = carMaxSpeed * 1.05f;
            Debug.Log($"[CarAudio] Auto-detected max speed: {carMaxSpeed} km/h. Gear table top: {topSpeed:F0} km/h.");
        }

        // Build a table of N+1 speed boundaries (0 to numberOfGears)
        // Gear N spans from gearSpeeds[N-1] to gearSpeeds[N]
        gearSpeeds = new float[numberOfGears + 1];
        gearSpeeds[0] = 0f;

        // Square-root curve: lower gears cover MORE speed, higher gears cover LESS.
        // This matches real transmissions: 1st covers 0-45, 8th covers 235-262.
        //   sqrt(0.125) = 0.354 -> G1 ends at 35% of topSpeed (~93 km/h for 262)
        //   sqrt(0.250) = 0.500 -> G2 ends at 50%
        //   sqrt(0.875) = 0.935 -> G7 ends at 93%
        //   sqrt(1.000) = 1.000 -> G8 ends at 100%
        for (int i = 1; i <= numberOfGears; i++)
        {
            float t = (float)i / numberOfGears;
            gearSpeeds[i] = topSpeed * Mathf.Sqrt(t);
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append($"[CarAudio] Gear table ({numberOfGears} gears, top={topSpeed:F0} km/h): ");
        for (int i = 1; i <= numberOfGears; i++)
            sb.Append($"G{i}:{gearSpeeds[i - 1]:F0}-{gearSpeeds[i]:F0} ");
        Debug.Log(sb.ToString());
    }

    void Update()
    {
        UpdateEngineAudio();
        UpdateSkidAudio();
    }

    void UpdateEngineAudio()
    {
        if (engineSource == null || !engineSource.isPlaying || gearSpeeds == null) return;

        float speedKmH = rb.linearVelocity.magnitude * 3.6f;

        // Detect reverse
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        bool isReversing = localVel.z < -0.15f;

        float throttle = GetThrottleInput();
        float rpmPercent = 0f;

        if (isReversing)
        {
            // Reverse: single gear, tops at ~40 km/h
            rpmPercent = Mathf.Clamp01(speedKmH / 40f);
            currentGear = 1;
            overdriveGear = 0;
            overdriveRpm  = 0f;
            inOverdrive   = false;
            currentShiftState = ShiftState.Driving;
        }
        else
        {
            // ── 1. Determine target gear from speed ─────────────────────────────
            int targetGear = 1;
            for (int i = 1; i < numberOfGears; i++)
            {
                if (speedKmH > gearSpeeds[i]) targetGear = i + 1;
            }

            // ── 2. Check if we are at the physical top speed ────────────────────
            float carMaxSpeed = carController != null ? carController.maxSpeed : 250f;
            bool  atMaxSpeed  = speedKmH >= carMaxSpeed - 8f;

            // If speed dropped below overdrive threshold, reset overdrive
            if (inOverdrive && !atMaxSpeed)
            {
                overdriveGear = 0;
                overdriveRpm  = 0f;
                inOverdrive   = false;
                currentShiftState = ShiftState.Driving;
            }

            // ── 3. Speed-based shift state machine (normal driving) ─────────────
            if (!inOverdrive && currentShiftState == ShiftState.Driving)
            {
                if (targetGear > currentGear)
                {
                    currentShiftState = ShiftState.ShiftingUp;
                    activeShiftDuration = baseUpshiftDuration * Mathf.Pow(shiftDurationGrowthFactor, currentGear - 1);
                    shiftTimer = activeShiftDuration;
                }
                else if (targetGear < currentGear)
                {
                    inOverdrive   = false;
                    overdriveGear = 0;
                    overdriveRpm  = 0f;
                    currentShiftState = ShiftState.ShiftingDown;
                    activeShiftDuration = baseDownshiftDuration * Mathf.Pow(shiftDurationGrowthFactor, targetGear - 1);
                    shiftTimer = activeShiftDuration;
                }
            }

            // Count down normal shift timer
            if (!inOverdrive && currentShiftState != ShiftState.Driving)
            {
                shiftTimer -= Time.deltaTime;
                if (shiftTimer <= 0f)
                {
                    currentGear = targetGear;
                    currentShiftState = ShiftState.Driving;

                    // Immediately enter overdrive if we just settled into top gear at max speed
                    if (atMaxSpeed && currentGear == numberOfGears)
                        inOverdrive = true;
                }
            }

            // If we just hit max speed and settled in top gear with no pending shift, enter overdrive
            if (!inOverdrive && atMaxSpeed && currentGear == numberOfGears && currentShiftState == ShiftState.Driving)
                inOverdrive = true;

            // ── 4. Overdrive logic (time-based infinite shifting at max speed) ──
            if (inOverdrive)
            {
                int  totalGear  = numberOfGears + overdriveGear; // display/sound gear
                float revDur   = overdriveRevDuration * Mathf.Pow(overdriveRevGrowth, overdriveGear);

                if (currentShiftState == ShiftState.Driving)
                {
                    if (throttle > 0.05f)
                    {
                        overdriveRpm += Time.deltaTime / revDur;

                        if (overdriveRpm >= 1.0f) // Redline → shift up
                        {
                            overdriveRpm = 1.0f;
                            currentShiftState = ShiftState.ShiftingUp;
                            activeShiftDuration = baseUpshiftDuration * Mathf.Pow(shiftDurationGrowthFactor, totalGear - 1);
                            shiftTimer = activeShiftDuration;
                        }
                    }
                    else
                    {
                        overdriveRpm -= 2f * Time.deltaTime;
                        if (overdriveRpm < 0f) overdriveRpm = 0f;
                    }

                    // Map overdriveRpm through the same rising-base system
                    float odGearT   = Mathf.Clamp01((float)(totalGear - 1) / (numberOfGears - 1));
                    float odBase    = Mathf.Lerp(baseGearRpm, topGearBaseRpm, Mathf.Min(odGearT, 1f));
                    rpmPercent = Mathf.Lerp(odBase, 1.0f, overdriveRpm);
                }
                else if (currentShiftState == ShiftState.ShiftingUp)
                {
                    shiftTimer -= Time.deltaTime;
                    float sf = Mathf.Clamp01(shiftTimer / activeShiftDuration);

                    int  nextTotal   = totalGear + 1;
                    float nextGearT  = Mathf.Clamp01((float)(nextTotal - 1) / (numberOfGears - 1));
                    float nextBase   = Mathf.Lerp(baseGearRpm, topGearBaseRpm, Mathf.Min(nextGearT, 1f));

                    float curGearT   = Mathf.Clamp01((float)(totalGear - 1) / (numberOfGears - 1));
                    float curBase    = Mathf.Lerp(baseGearRpm, topGearBaseRpm, Mathf.Min(curGearT, 1f));
                    float curEffRpm  = Mathf.Lerp(curBase, 1.0f, overdriveRpm);

                    rpmPercent = Mathf.Lerp(nextBase, curEffRpm, sf * sf);

                    if (shiftTimer <= 0f)
                    {
                        overdriveGear++;
                        overdriveRpm = 0f;
                        currentShiftState = ShiftState.Driving;
                    }
                }
            }
            else // ── 5. Normal speed-based RPM ────────────────────────────────
            {
                float gMin      = gearSpeeds[currentGear - 1];
                float gMax      = gearSpeeds[currentGear];
                float rawRpm    = Mathf.Clamp01((speedKmH - gMin) / (gMax - gMin));

                float gearT     = Mathf.Clamp01((float)(currentGear - 1) / (numberOfGears - 1));
                float gearBase  = Mathf.Lerp(baseGearRpm, topGearBaseRpm, gearT);
                float effRpm    = Mathf.Lerp(gearBase, 1.0f, rawRpm);

                float shiftFact = Mathf.Clamp01(shiftTimer / activeShiftDuration);

                if (currentShiftState == ShiftState.ShiftingUp)
                {
                    float nextGearT  = Mathf.Clamp01((float)currentGear / (numberOfGears - 1));
                    float nextBase   = Mathf.Lerp(baseGearRpm, topGearBaseRpm, nextGearT);
                    rpmPercent = Mathf.Lerp(nextBase, effRpm, shiftFact * shiftFact);
                }
                else if (currentShiftState == ShiftState.ShiftingDown)
                {
                    float blipSpike = Mathf.Sin(shiftFact * Mathf.PI) * blipRpmTarget;
                    rpmPercent = Mathf.Max(effRpm, blipSpike);
                }
                else
                {
                    rpmPercent = effRpm;
                }
            }
        }

        // Coasting: RPM settles to the gear's natural base RPM (not near-zero)
        // Higher gears coast at a higher RPM — exactly like a real car
        int coastGear = inOverdrive ? (numberOfGears + overdriveGear) : currentGear;
        float coastGearT   = Mathf.Clamp01((float)(coastGear - 1) / (numberOfGears - 1));
        float coastFloor   = Mathf.Lerp(baseGearRpm, topGearBaseRpm, Mathf.Min(coastGearT, 1f));

        float targetRpm = rpmPercent;
        if (throttle <= 0.05f && currentShiftState == ShiftState.Driving)
            targetRpm = Mathf.Lerp(rpmPercent, coastFloor, 0.75f);

        // Constant redline pitch — same maxPitch every gear
        float targetPitch  = Mathf.Lerp(minPitch, maxPitch, targetRpm);
        float targetVolume = Mathf.Lerp(baseVolume, maxVolume, targetRpm) * volumeBoost;
        targetVolume = Mathf.Clamp(targetVolume, 0f, 1f);
        if (throttle <= 0.05f && currentShiftState == ShiftState.Driving)
            targetVolume *= 0.70f;

        currentEnginePitch = Mathf.Lerp(currentEnginePitch, targetPitch, pitchSmoothSpeed * Time.deltaTime);

        engineSource.pitch  = currentEnginePitch;
        engineSource.volume = Mathf.Lerp(engineSource.volume, targetVolume, 8f * Time.deltaTime);
        if (engineSource2 != null)
        {
            engineSource2.pitch  = currentEnginePitch;
            engineSource2.volume = engineSource.volume;
        }

        // Modulate low-pass filter: deep/muffled at idle, fully open at redline
        if (engineFilter != null)
        {
            float targetCutoff = Mathf.Lerp(filterCutoffIdle, filterCutoffRedline, targetRpm * targetRpm);
            engineFilter.cutoffFrequency = Mathf.Lerp(engineFilter.cutoffFrequency, targetCutoff, filterSmoothSpeed * Time.deltaTime);
        }
    }

    void UpdateSkidAudio()
    {
        if (skidSource == null) return;

        float maxSlip = GetMaxWheelSlip();

        if (maxSlip > skidThreshold && skidClip != null)
        {
            // Modulate screech volume based on how aggressively the car is sliding
            float volumeFactor = Mathf.InverseLerp(skidThreshold, skidThreshold + 0.4f, maxSlip);
            skidSource.volume = volumeFactor * maxSkidVolume;

            if (!skidSource.isPlaying)
            {
                skidSource.Play();
            }
        }
        else
        {
            // Smoothly fade out the screech sound when traction is regained
            skidSource.volume = Mathf.Lerp(skidSource.volume, 0f, 15f * Time.deltaTime);
            if (skidSource.volume < 0.01f && skidSource.isPlaying)
            {
                skidSource.Stop();
            }
        }
    }

    float GetMaxWheelSlip()
    {
        if (wheelsCache == null) return 0f;

        float maxSlip = 0f;
        int len = wheelsCache.Length;
        for (int i = 0; i < len; i++)
        {
            var wheel = wheelsCache[i];
            if (wheel != null && wheel.GetGroundHit(out WheelHit hit))
            {
                // Combine forward and sideways slips
                float slip = Mathf.Max(Mathf.Abs(hit.forwardSlip), Mathf.Abs(hit.sidewaysSlip));
                if (slip > maxSlip)
                {
                    maxSlip = slip;
                }
            }
        }
        return maxSlip;
    }

    void PlayLapChime()
    {
        // Plays a nice notification sound when crossing the lap line
        if (hudSource != null && lapCompleteClip != null)
        {
            hudSource.PlayOneShot(lapCompleteClip);
            Debug.Log("[Car Audio] Lap complete chime played!");
        }
    }

    private float GetThrottleInput()
    {
        if (carController == null) return 0f;
        
        if (carController.isAI)
        {
            return carController.aiMoveInput;
        }

        // Check gamepad first if present
        if (Gamepad.current != null)
        {
            float throttle = Gamepad.current.rightTrigger.ReadValue();
            float brake = Gamepad.current.leftTrigger.ReadValue();
            return throttle - brake;
        }
        
        // Keyboard fallback
        return Input.GetAxis("Vertical");
    }
}
