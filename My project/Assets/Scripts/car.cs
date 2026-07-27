using System.Collections; // Добавьте эту строку!
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PickupController : MonoBehaviour
{
    [Header("Настройки трансмиссии")]
    [Tooltip("Скорость изменения оборотов. Чем выше, тем быстрее стрелка реагирует на переключения.")]
    public float rpmChangeSmoothness = 4000f;
    [Header("Настройки сцепления")]
    [Tooltip("Время выжима сцепления в секундах (например, 0.25 секунды)")]
    public float clutchDropDuration = 0.25f;
    private bool isClutchDisengaged = false; 
    [Header("Настройки АБС")]
    public bool useABS = true;               // Включить/выключить АБС
    [Range(0.1f, 1f)]
    public float absThreshold = 0.6f;        // Порог срабатывания проскальзывания (0.6 — колесо сильно скользит)
    [Range(0.01f, 0.2f)]
    public float absReleaseTime = 0.05f;     // На сколько секунд АБС «отпускает» тормоз при блокировке

    public bool isAutomatic = true;          // Автоматическая КПП (true) или ручная (false)
    public float[] gearRatios = { 3.5f, 2.7f, 1.8f, 1.3f, 1.0f, 0.8f }; // Передаточные числа (1-6 передачи)
    public float reverseGearRatio = 3.0f;    // Передаточное число задней передачи
    public float finalDriveRatio = 3.4f;     // Главная пара (дифференциал)
    public float maxEngineRPM = 6000f;       // Максимальные обороты двигателя
    public float minEngineRPM = 1000f;       // Холостые обороты двигателя
    public float maxMotorTorque = 400f;      // Базовый крутящий момент мотора (теперь умножается на передачи)

    [Header("Настройки скорости и управления")]
    public float maxAbsoluteSpeedKmH = 180f; // Абсолютный лимит скорости для машины
    public float maxSteeringAngle = 40f;
    public float minSteeringAngle = 12f;
    public float brakeForce = 4000f;

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

    // Внутренние переменные физики и трансмиссии
    private float currentSteeringAngle;
    private float targetSteeringAngle;
    private float smoothedVerticalInput;
    private Rigidbody rb;

    private int currentGear = 1;             // Текущая передача: -1 = Задняя, 0 = Нейтралка, 1-6 = Вперед
    private float currentRPM;                // Текущие обороты двигателя (RPM)
    private float currentSpeedKmH;           // Текущая скорость в км/ч

    // Свойства для чтения из других скриптов (например, для UI спидометра/тахометра)
    public int CurrentGear => currentGear;
    public float CurrentRPM => currentRPM;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (centerOfMass != null)
        {
            rb.centerOfMass = centerOfMass.localPosition;
        }
    }

    private void FixedUpdate()
    {
        currentSpeedKmH = rb.linearVelocity.magnitude * 3.6f;

        HandleGears(); // Логика работы коробки передач
        HandleSteering();
        HandleMotorAndBraking();
        UpdateWheels();
    }

    private void HandleGears()
    {
        float rearWheelsRPM = (rearLeftCollider.rpm + rearRightCollider.rpm) / 2f;
        float targetRPM = minEngineRPM;

        // Запоминаем передачу ПЕРЕД возможным переключением
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

        // Если сцепление выжато, заставляем обороты падать быстрее к целевым (имитация сброса газа)
        float currentSmoothness = isClutchDisengaged ? rpmChangeSmoothness * 1.5f : rpmChangeSmoothness;
        currentRPM = Mathf.MoveTowards(currentRPM, targetRPM, Time.fixedDeltaTime * currentSmoothness);

        // Логика переключения передач (Автомат)
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
            // Ручное переключение (E - Вверх, Q - Вниз) с защитой от спама во время выжима
            if (!isClutchDisengaged)
            {
                if (Input.GetKeyDown(KeyCode.E) && currentGear < gearRatios.Length) currentGear++;
                if (Input.GetKeyDown(KeyCode.Q) && currentGear > -1) currentGear--;
            }
        }

        // ⚡ ПРОВЕРКА: Если передача изменилась, запускаем паузу сцепления
        if (currentGear != previousGear && previousGear != 0 && currentGear != 0)
        {
            // Запускаем корутину через MonoBehaviour, так как FixedUpdate не умеет ждать
            StartCoroutine(ClutchShiftRoutine());
        }
    }

    // Корутина временного разрыва связи мотора и колес
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

        // Определяем, хочет ли игрок тормозить (зажат пробел или стрелка назад при движении вперед)
        bool isBrakingInput = Input.GetKey(KeyCode.Space) || (targetVerticalInput < 0 && forwardSpeed > 0.5f) || (targetVerticalInput > 0 && forwardSpeed < -0.5f);

        if (Input.GetKey(KeyCode.Space))
        {
            currentBrakeForce = brakeForce;
            smoothedVerticalInput = 0f;
        }
        else
        {
            if (targetVerticalInput > 0)
            {
                if (forwardSpeed < -0.5f) currentBrakeForce = brakeForce;
                else currentMotorTorque = CalculateTorque(smoothedVerticalInput);
            }
            else if (targetVerticalInput < 0)
            {
                if (forwardSpeed > 0.5f) currentBrakeForce = brakeForce;
                else currentMotorTorque = CalculateTorque(smoothedVerticalInput);
            }
            else
            {
                smoothedVerticalInput = Mathf.MoveTowards(smoothedVerticalInput, 0f, Time.fixedDeltaTime * accelerationSmoothness * 2f);
                currentBrakeForce = 20f;
            }
        }

        if (currentSpeedKmH >= maxAbsoluteSpeedKmH && currentMotorTorque * forwardSpeed > 0)
        {
            currentMotorTorque = 0f;
        }

        // --- РАБОТА СИСТЕМЫ АБС ---
        // Изначально распределяем тормозное усилие на все колеса
        float fL_Brake = currentBrakeForce;
        float fR_Brake = currentBrakeForce;
        float rL_Brake = currentBrakeForce;
        float rR_Brake = currentBrakeForce;

        // Если игрок нажал на тормоз и АБС включена в инспекторе
        if (isBrakingInput && useABS)
        {
            // Проверяем каждое колесо индивидуально. Если оно заблокировано — снижаем его тормоз до нуля
            if (CheckWheelLock(frontLeftCollider)) fL_Brake = 0f;
            if (CheckWheelLock(frontRightCollider)) fR_Brake = 0f;
            if (CheckWheelLock(rearLeftCollider)) rL_Brake = 0f;
            if (CheckWheelLock(rearRightCollider)) rR_Brake = 0f;
        }

        // Применяем крутящий момент (4WD)
        frontLeftCollider.motorTorque = currentMotorTorque;
        frontRightCollider.motorTorque = currentMotorTorque;
        rearLeftCollider.motorTorque = currentMotorTorque;
        rearRightCollider.motorTorque = currentMotorTorque;

        // Применяем финальное тормозное усилие (с учетом корректировок АБС)
        frontLeftCollider.brakeTorque = fL_Brake;
        frontRightCollider.brakeTorque = fR_Brake;
        rearLeftCollider.brakeTorque = rL_Brake;
        rearRightCollider.brakeTorque = rR_Brake;
    }

    // Вспомогательный метод для проверки блокировки конкретного колеса
    private bool CheckWheelLock(WheelCollider collider)
    {
        WheelHit hit;
        // Запрашиваем у Unity физические данные о контакте колеса с дорогой
        if (collider.GetGroundHit(out hit))
        {
            // forwardSlip уходит в минус при жестком торможении. 
            // Если абсолютное значение скольжения больше порога (absThreshold), колесо заблокировано юзом!
            if (Mathf.Abs(hit.forwardSlip) > absThreshold)
            {
                return true; // АБС должна вмешаться и отпустить тормоз
            }
        }
        return false;
    }

    // Расчет финального крутящего момента с учетом выбранной передачи
    private float CalculateTorque(float input)
    {
        // ⚡ ИСПРАВЛЕНИЕ: Если сцепление выжато, момент равен 0 (мотор отсоединен от колес)
        if (isClutchDisengaged) return 0f;

        if (currentRPM >= maxEngineRPM - 100f) return 0f;

        float totalRatio = 0f;

        if (currentGear == -1) totalRatio = reverseGearRatio * finalDriveRatio;
        else if (currentGear > 0) totalRatio = gearRatios[currentGear - 1] * finalDriveRatio;
        else return 0f;

        return input * maxMotorTorque * totalRatio;
    }


    private void HandleSteering()
    {
        float horizontalInput = Input.GetAxis("Horizontal");

        // Динамический угол поворота в зависимости от скорости автомобиля
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
