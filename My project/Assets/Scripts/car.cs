using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PickupController : MonoBehaviour
{
    [Header("Настройки трансмиссии")]
    public float rpmChangeSmoothness = 4000f;
    [Header("Настройки сцепления")]
    public float clutchDropDuration = 0.25f;
    private bool isClutchDisengaged = false;

    [Header("Настройки АБС")]
    public bool useABS = true;
    [Tooltip("Минимальная скорость (км/ч), при которой работает АБС")]
    public float absMinSpeedKmH = 20f;
    [Range(0.1f, 1f)]
    public float absThreshold = 0.6f;
    [Range(0.01f, 0.2f)]
    public float absReleaseTime = 0.05f;

    public bool isAutomatic = true;
    public float[] gearRatios = { 3.5f, 2.7f, 1.8f, 1.3f, 1.0f, 0.8f };
    public float reverseGearRatio = 3.0f;
    public float finalDriveRatio = 3.4f;
    public float maxEngineRPM = 6000f;
    public float minEngineRPM = 1000f;
    public float maxMotorTorque = 400f;

    [Header("Настройки скорости и управления")]
    public float maxAbsoluteSpeedKmH = 180f;
    public float maxSteeringAngle = 40f;
    public float minSteeringAngle = 12f;

    [Header("Тормозная система")]
    [Tooltip("Сила торможения при нажатии S (обычный тормоз)")]
    public float normalBrakeForce = 2500f;
    [Tooltip("Сила торможения при нажатии Пробела (ручник/экстренное)")]
    public float handbrakeForce = 6000f;
    [Tooltip("Торможение двигателем (когда газ отпущен)")]
    public float idleBrakeForce = 400f;
    [Tooltip("Сила сопротивления двигателя при понижении передачи на высокой скорости")]
    public float downshiftBrakeForce = 3500f;

    [Header("Система веса (Груз)")]
    public CargoManager cargoManager;
    [Range(0.1f, 1f)]
    [Tooltip("На сколько процентов режется мощность при наличии груза (0.7 = 70% от мощности)")]
    public float loadedAccelerationMultiplier = 0.7f;

    [Header("Плавность движения")]
    public float accelerationSmoothness = 1.2f;
    public float steeringSmoothness = 5f;

    [Header("Центр тяжести")]
    public Transform centerOfMass;

    [Header("Wheel Colliders")]
    public WheelCollider frontLeftCollider;
    public WheelCollider frontRightCollider;
    public WheelCollider rearLeftCollider;
    public WheelCollider rearRightCollider;

    [Header("Wheel Meshes")]
    public Transform frontLeftMesh;
    public Transform frontRightMesh;
    public Transform rearLeftMesh;
    public Transform rearRightMesh;

    private float currentSteeringAngle;
    private float targetSteeringAngle;
    private float smoothedVerticalInput;
    private Rigidbody rb;

    private int currentGear = 1;
    private float currentRPM;
    private float currentSpeedKmH;

    private bool shiftUpPressed = false;
    private bool shiftDownPressed = false;

    public int CurrentGear => currentGear;
    public float CurrentRPM => currentRPM;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (centerOfMass != null)
        {
            rb.centerOfMass = centerOfMass.localPosition;
        }

        if (cargoManager == null)
        {
            cargoManager = GetComponent<CargoManager>();
            if (cargoManager == null) cargoManager = GetComponentInParent<CargoManager>();
        }
    }

    private void Update()
    {
        // Инпуты (GetKeyDown) всегда нужно считывать в Update!
        if (!isAutomatic)
        {
            if (Input.GetKeyDown(KeyCode.E)) shiftUpPressed = true;
            if (Input.GetKeyDown(KeyCode.Q)) shiftDownPressed = true;
        }
    }

    private void FixedUpdate()
    {
        currentSpeedKmH = rb.linearVelocity.magnitude * 3.6f;

        HandleGears();
        HandleSteering();
        HandleMotorAndBraking();
        UpdateWheels();
    }

    private void HandleGears()
    {
        float rearWheelsRPM = (rearLeftCollider.rpm + rearRightCollider.rpm) / 2f;
        float targetRPM = minEngineRPM;

        int previousGear = currentGear;

        if (currentGear == 0)
        {
            targetRPM = minEngineRPM + Mathf.Abs(Input.GetAxis("Vertical")) * (maxEngineRPM - minEngineRPM);
        }
        else if (currentGear == -1)
        {
            targetRPM = Mathf.Abs(rearWheelsRPM) * reverseGearRatio * finalDriveRatio;
        }
        else
        {
            targetRPM = Mathf.Abs(rearWheelsRPM) * gearRatios[currentGear - 1] * finalDriveRatio;
        }

        targetRPM = Mathf.Clamp(targetRPM, minEngineRPM, maxEngineRPM);

        float currentSmoothness = isClutchDisengaged ? rpmChangeSmoothness * 1.5f : rpmChangeSmoothness;
        currentRPM = Mathf.MoveTowards(currentRPM, targetRPM, Time.fixedDeltaTime * currentSmoothness);

        if (isAutomatic)
        {
            float forwardSpeed = transform.InverseTransformDirection(rb.linearVelocity).z;

            if (forwardSpeed >= 0)
            {
                if (currentGear == -1 && forwardSpeed < 0.5f) currentGear = 1;

                if (currentRPM > maxEngineRPM * 0.85f && currentGear > 0 && currentGear < gearRatios.Length && !isClutchDisengaged)
                {
                    currentGear++;
                }
                if (currentRPM < maxEngineRPM * 0.4f && currentGear > 1 && !isClutchDisengaged)
                {
                    currentGear--;
                }
            }
            else if (forwardSpeed < -0.5f && Input.GetAxis("Vertical") < 0)
            {
                currentGear = -1;
            }
        }
        else
        {
            if (!isClutchDisengaged)
            {
                if (shiftUpPressed && currentGear < gearRatios.Length) currentGear++;
                if (shiftDownPressed && currentGear > -1) currentGear--;
            }

            // Обязательно сбрасываем флаги, чтобы передача не перескочила дважды
            shiftUpPressed = false;
            shiftDownPressed = false;
        }

        if (currentGear != previousGear && previousGear != 0 && currentGear != 0)
        {
            StartCoroutine(ClutchShiftRoutine());
        }
    }

    private IEnumerator ClutchShiftRoutine()
    {
        isClutchDisengaged = true;
        yield return new WaitForSeconds(clutchDropDuration);
        isClutchDisengaged = false;
    }

    private void HandleMotorAndBraking()
    {
        float targetVerticalInput = Input.GetAxis("Vertical");
        smoothedVerticalInput = Mathf.MoveTowards(smoothedVerticalInput, targetVerticalInput, Time.fixedDeltaTime * accelerationSmoothness);

        float forwardSpeed = transform.InverseTransformDirection(rb.linearVelocity).z;
        float currentMotorTorque = 0f;
        float currentBrakeForce = 0f;

        bool isHandbrakePressed = Input.GetKey(KeyCode.Space);
        bool isBrakingInput = isHandbrakePressed || (targetVerticalInput < 0 && forwardSpeed > 0.5f) || (targetVerticalInput > 0 && forwardSpeed < -0.5f);

        // --- ЛОГИКА ТОРМОЖЕНИЯ ---
        if (isHandbrakePressed)
        {
            // Жесткий тормоз (ручник)
            currentBrakeForce = handbrakeForce;
            smoothedVerticalInput = 0f;
        }
        else
        {
            if (targetVerticalInput > 0)
            {
                if (forwardSpeed < -0.5f) currentBrakeForce = normalBrakeForce;
                else currentMotorTorque = CalculateTorque(smoothedVerticalInput);
            }
            else if (targetVerticalInput < 0)
            {
                if (forwardSpeed > 0.5f) currentBrakeForce = normalBrakeForce;
                else currentMotorTorque = CalculateTorque(smoothedVerticalInput);
            }
            else
            {
                smoothedVerticalInput = Mathf.MoveTowards(smoothedVerticalInput, 0f, Time.fixedDeltaTime * accelerationSmoothness * 2f);

                // ⚡ Теперь применяем сопротивление двигателя вместо слабого трения
                currentBrakeForce = idleBrakeForce;
            }
        }

        if (currentSpeedKmH >= maxAbsoluteSpeedKmH && currentMotorTorque * forwardSpeed > 0)
        {
            currentMotorTorque = 0f;
        }

        
        if (currentGear > 0 && currentRPM >= maxEngineRPM * 0.95f && !isClutchDisengaged)
        {
            
            if (currentBrakeForce < downshiftBrakeForce)
            {
                currentBrakeForce = downshiftBrakeForce;
            }
            currentMotorTorque = 0f; // Глушим тягу, мотор занят торможением
        }

        float fL_Brake = currentBrakeForce;
        float fR_Brake = currentBrakeForce;
        float rL_Brake = currentBrakeForce;
        float rR_Brake = currentBrakeForce;

        // --- РАБОТА СИСТЕМЫ АБС ---
        // АБС работает только если: включена в настройках + игрок тормозит + скорость выше минимальной + ручник НЕ нажат
        bool canUseABS = useABS && isBrakingInput && (currentSpeedKmH >= absMinSpeedKmH) && !isHandbrakePressed;

        if (canUseABS)
        {
            if (CheckWheelLock(frontLeftCollider)) fL_Brake = 0f;
            if (CheckWheelLock(frontRightCollider)) fR_Brake = 0f;
            if (CheckWheelLock(rearLeftCollider)) rL_Brake = 0f;
            if (CheckWheelLock(rearRightCollider)) rR_Brake = 0f;
        }

        frontLeftCollider.motorTorque = currentMotorTorque;
        frontRightCollider.motorTorque = currentMotorTorque;
        rearLeftCollider.motorTorque = currentMotorTorque;
        rearRightCollider.motorTorque = currentMotorTorque;

        frontLeftCollider.brakeTorque = fL_Brake;
        frontRightCollider.brakeTorque = fR_Brake;
        rearLeftCollider.brakeTorque = rL_Brake;
        rearRightCollider.brakeTorque = rR_Brake;
    }

    private bool CheckWheelLock(WheelCollider collider)
    {
        WheelHit hit;
        if (collider.GetGroundHit(out hit))
        {
            if (Mathf.Abs(hit.forwardSlip) > absThreshold)
            {
                return true;
            }
        }
        return false;
    }

    private float CalculateTorque(float input)
    {
        if (isClutchDisengaged) return 0f;
        if (currentRPM >= maxEngineRPM - 100f) return 0f;

        float totalRatio = 0f;

        if (currentGear == -1) totalRatio = reverseGearRatio * finalDriveRatio;
        else if (currentGear > 0) totalRatio = gearRatios[currentGear - 1] * finalDriveRatio;
        else return 0f;

        float rawTorque = input * maxMotorTorque * totalRatio;

        if (cargoManager != null && cargoManager.HasCargo())
        {
            rawTorque *= loadedAccelerationMultiplier;
        }

        return rawTorque;
    }

    private void HandleSteering()
    {
        float horizontalInput = Input.GetAxis("Horizontal");

        float speedFactor = Mathf.Clamp01(currentSpeedKmH / 80f);
        float dynamicMaxSteerAngle = Mathf.Lerp(maxSteeringAngle, minSteeringAngle, speedFactor);

        targetSteeringAngle = horizontalInput * dynamicMaxSteerAngle;
        currentSteeringAngle = Mathf.MoveTowards(currentSteeringAngle, targetSteeringAngle, Time.fixedDeltaTime * steeringSmoothness * maxSteeringAngle);

        frontLeftCollider.steerAngle = currentSteeringAngle;
        frontRightCollider.steerAngle = currentSteeringAngle;
    }

    private void UpdateWheels()
    {
        UpdateSingleWheel(frontLeftCollider, frontLeftMesh);
        UpdateSingleWheel(frontRightCollider, frontRightMesh);
        UpdateSingleWheel(rearLeftCollider, rearLeftMesh);
        UpdateSingleWheel(rearRightCollider, rearRightMesh);
    }

    private void UpdateSingleWheel(WheelCollider collider, Transform meshTransform)
    {
        if (meshTransform == null) return;
        Vector3 position;
        Quaternion rotation;
        collider.GetWorldPose(out position, out rotation);
        meshTransform.position = position;
        meshTransform.rotation = rotation;
    }
}