using UnityEngine;

public class FixedCameraController : MonoBehaviour
{
    private enum CameraMode { Default, Alternate, LookBack }
    private CameraMode currentMode = CameraMode.Default;
    private CameraMode previousModeBeforeLookBack = CameraMode.Default;

    [Header("Объект для слежения")]
    public Transform target;

    [Header("Навигация")]
    public Transform navigationArrow;

    [Header("Положение 1: Стандартный вид сзади")]
    public Vector3 defaultOffset = new Vector3(0f, 2.5f, -6f);
    public float defaultXRotation = 20f;
    public Vector3 arrowDefaultLocalPos = new Vector3(0f, 3f, 0f);

    [Header("Положение 2: Альтернативный вид (Капот)")]
    public Vector3 alternateOffset = new Vector3(0f, 1.5f, 1.2f);
    public float alternateXRotation = 15f;
    public Vector3 arrowAlternateLocalPos = new Vector3(0f, 1.2f, 3.5f);

    [Header("Положение 3: Взгляд назад (Удержание B)")]
    public Vector3 lookBackOffset = new Vector3(0f, 2f, 5f);
    public float lookBackXRotation = 10f;

    [Header("Текущие настройки плавности камеры")]
    public float positionSmoothTime = 0f;
    public float rotationSmoothTime = 0f;

    [Header("Плавность парения стрелки")]
    [SerializeField] private float arrowPositionSmooth = 12f; // Скорость гашения прыжков стрелки (выше = быстрее возвращается к машине)

    [Header("Настройки амортизатора стрелки")]
    [Tooltip("Время сглаживания высоты в секундах. Чем БОЛЬШЕ значение, тем ЛЕНИВЕЕ и плавнее стрелка ходит вверх/вниз.")]
    public float arrowYSmoothTime = 0.3f; // 0.3 секунды на гашение удара
    private float arrowYVelocity; // Переменная для хранения технической скорости (нужна для SmoothDamp)
    private Vector3 positionVelocity;
    private float rotationVelocity;

    // Внутренняя переменная для расчета плавной мировой позиции стрелки
    private Vector3 targetArrowWorldPos;

    void Update()
    {
        if (!target) return;

        // Обработка клавиш B и C
        if (Input.GetKeyDown(KeyCode.B))
        {
            previousModeBeforeLookBack = currentMode;
            currentMode = CameraMode.LookBack;
        }
        else if (Input.GetKeyUp(KeyCode.B))
        {
            currentMode = previousModeBeforeLookBack;
        }

        if (currentMode != CameraMode.LookBack && Input.GetKeyDown(KeyCode.C))
        {
            if (currentMode == CameraMode.Default) currentMode = CameraMode.Alternate;
            else currentMode = CameraMode.Default;
        }

        // Управление плавностью камеры
        if (currentMode == CameraMode.Default)
        {
            positionSmoothTime = 0.1f;
            rotationSmoothTime = 0.05f;
        }
        else
        {
            positionSmoothTime = 0f;
            rotationSmoothTime = 0f;
        }

        // УМНОЕ ПАРЕНИЕ СТРЕЛКИ НАВИГАТОРА (Идеальный масляный амортизатор по Y):
        if (navigationArrow != null)
        {
            // 1. Рассчитываем идеальные координаты, где стрелка ДОЛЖНА БЫТЬ в мировом пространстве
            Vector3 desiredLocalPos = (currentMode == CameraMode.Alternate) ? arrowAlternateLocalPos : arrowDefaultLocalPos;
            Vector3 desiredWorldPos = target.TransformPoint(desiredLocalPos);

            // 2. РАЗДЕЛЯЕМ МИРОВЫЕ И ЛОКАЛЬНЫЕ ОСИ:
            // Координаты X и Z привязываем жестко к мировым, чтобы стрелка НИКОГДА не отставала при разгоне
            float instantWorldX = desiredWorldPos.x;
            float instantWorldZ = desiredWorldPos.z;

            // Высоту (Y) продолжаем плавно сглаживать, чтобы погасить тряску от неровностей дороги
            float smoothY = Mathf.Lerp(navigationArrow.position.y, desiredWorldPos.y, Time.deltaTime * arrowPositionSmooth);

            // 3. ПРИМЕНЯЕМ СВЕРХПЛАВНЫЙ АМОРТИЗАТОР ДЛЯ ВЕРТИКАЛИ (ОСЬ Y):
            // Метод SmoothDamp сглаживает МИРОВУЮ высоту.
            // Вместо arrowPositionSmooth здесь используется arrowYSmoothTime (время сглаживания в секундах)
            float smoothWorldY = Mathf.SmoothDamp(
                navigationArrow.position.y,
                desiredWorldPos.y,
                ref arrowYVelocity,
                arrowYSmoothTime
            );

            // Применяем собранные мировые координаты
            navigationArrow.position = new Vector3(instantWorldX, smoothWorldY, instantWorldZ);

            // 4. НАМЕРТВО УНИЧТОЖАЕМ ТАНГАЖ И КРЕН НА КОЧКАХ:
            Vector3 currentEuler = navigationArrow.eulerAngles;
            navigationArrow.rotation = Quaternion.Euler(0f, currentEuler.y, 0f);
        }

    }

    void LateUpdate()
    {
        if (!target) return;

        Vector3 activeOffset = defaultOffset;
        float targetXRotation = defaultXRotation;
        float rotationAngleOffset = 0f;

        switch (currentMode)
        {
            case CameraMode.Default: activeOffset = defaultOffset; targetXRotation = defaultXRotation; break;
            case CameraMode.Alternate: activeOffset = alternateOffset; targetXRotation = alternateXRotation; break;
            case CameraMode.LookBack: activeOffset = lookBackOffset; targetXRotation = lookBackXRotation; rotationAngleOffset = 180f; break;
        }

        Vector3 targetPosition = target.TransformPoint(activeOffset);
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref positionVelocity, positionSmoothTime);

        float targetRotationAngle = target.eulerAngles.y + rotationAngleOffset;
        float currentRotationAngle = transform.eulerAngles.y;
        currentRotationAngle = Mathf.SmoothDampAngle(currentRotationAngle, targetRotationAngle, ref rotationVelocity, rotationSmoothTime);

        transform.rotation = Quaternion.Euler(targetXRotation, currentRotationAngle, 0f);
    }
}