using UnityEngine;
using UnityEngine.UI; // ОБЯЗАТЕЛЬНО: добавляем эту строчку для работы с UI

public class CarFuelSystem : MonoBehaviour
{
    [Header("Параметры топлива")]
    public float maxFuel = 100f;
    public float currentFuel;

    [Header("Настройки расхода")]
    public float idleConsumption = 0.05f;
    public float maxGasConsumption = 0.5f;
    public float rpmFactor = 0.0005f;

    [Header("Ссылки на компоненты")]
    public WheelCollider[] wheelColliders;
    public Slider fuelSlider;             // Сюда перетащим наш UI Slider из Canvas

    private bool isOutofFuel = false;

    void Start()
    {
        currentFuel = maxFuel;

        // Инициализируем UI шкалу при старте
        if (fuelSlider != null)
        {
            fuelSlider.maxValue = maxFuel;
            fuelSlider.value = currentFuel;
        }
    }

    void Update()
    {
        if (isOutofFuel) return;

        // Расход заведенного мотора
        float currentConsumption = idleConsumption;

        // Считываем обороты
        float totalRPM = 0f;
        foreach (WheelCollider wheel in wheelColliders)
        {
            totalRPM += Mathf.Abs(wheel.rpm);
        }
        float averageRPM = totalRPM / (wheelColliders.Length > 0 ? wheelColliders.Length : 1);
        currentConsumption += averageRPM * rpmFactor;

        // Расход при нажатии газа
        if (Input.GetKey(KeyCode.UpArrow))
        {
            currentConsumption += maxGasConsumption;
        }

        // Уменьшаем топливо
        currentFuel -= currentConsumption * Time.deltaTime;
        currentFuel = Mathf.Clamp(currentFuel, 0f, maxFuel);

        // ОБНОВЛЯЕМ ВЕРТИКАЛЬНУЮ ШКАЛУ НА ЭКРАНЕ
        if (fuelSlider != null)
        {
            fuelSlider.value = currentFuel;
        }

        // Проверяем, не закончилось ли топливо
        if (currentFuel <= 0f)
        {
            FuelEmpty();
        }
    }

    void FuelEmpty()
    {
        isOutofFuel = true;
        Debug.LogWarning("Топливо закончилось! Машина заглохла.");

        // 1. Находим менеджер паузы на сцене
        PauseManager pauseManager = Object.FindFirstObjectByType<PauseManager>();

        // 2. Находим ваш менеджер грузов на сцене
        CargoManager cargoManager = Object.FindFirstObjectByType<CargoManager>();

        int scoresAtEnd = 0;

        // Если менеджер грузов успешно найден, забираем из него очки
        if (cargoManager != null)
        {
            scoresAtEnd = cargoManager.currentScore; // Подставляем вашу переменную очков
        }

        // 3. Отправляем очки в менеджер паузы и включаем экран Game Over
        if (pauseManager != null)
        {
            pauseManager.TriggerGameOver(scoresAtEnd);
        }
    }


    public void RefuelToMax()
    {
        currentFuel = maxFuel;
        isOutofFuel = false;

        // Обновляем шкалу при заправке
        if (fuelSlider != null)
        {
            fuelSlider.value = currentFuel;
        }
        Debug.Log("Бак полностью заправлен!");
    }
}
