using UnityEngine;
using UnityEngine.UI;

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
    public Slider fuelSlider;

    [Header("Предупреждение о низком уровне")]
    public GameObject lowFuelWarningUI;   // ⚡ Сюда перетащим текст или иконку предупреждения из Canvas
    private float flashTimer = 0f;        // Таймер для мигания

    private bool isOutofFuel = false;

    void Start()
    {
        currentFuel = maxFuel;

        if (fuelSlider != null)
        {
            fuelSlider.maxValue = maxFuel;
            fuelSlider.value = currentFuel;
        }

        // Выключаем предупреждение на старте
        if (lowFuelWarningUI != null) lowFuelWarningUI.SetActive(false);
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

        if (fuelSlider != null)
        {
            fuelSlider.value = currentFuel;
        }

        // ⚡ ЛОГИКА МИГАНИЯ ПРЕДУПРЕЖДЕНИЯ
        if (lowFuelWarningUI != null)
        {
            // Проверяем, осталось ли меньше 30% топлива
            if (currentFuel / maxFuel < 0.3f)
            {
                flashTimer += Time.deltaTime;

                // Переключаем состояние каждую 1 секунду
                if (flashTimer >= 1f)
                {
                    flashTimer = 0f;
                    lowFuelWarningUI.SetActive(!lowFuelWarningUI.activeSelf);
                }
            }
            else
            {
                // Если топлива больше 30%, точно выключаем предупреждение и сбрасываем таймер
                if (lowFuelWarningUI.activeSelf) lowFuelWarningUI.SetActive(false);
                flashTimer = 0f;
            }
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

        // Отключаем мигалку, чтобы она не висела на экране Game Over
        if (lowFuelWarningUI != null) lowFuelWarningUI.SetActive(false);

        Debug.LogWarning("Топливо закончилось! Машина заглохла.");

        PauseManager pauseManager = Object.FindFirstObjectByType<PauseManager>();
        CargoManager cargoManager = Object.FindFirstObjectByType<CargoManager>();

        int scoresAtEnd = 0;

        if (cargoManager != null)
        {
            scoresAtEnd = cargoManager.currentScore;
        }

        if (pauseManager != null)
        {
            pauseManager.TriggerGameOver(scoresAtEnd);
        }
    }

    public void RefuelToMax()
    {
        currentFuel = maxFuel;
        isOutofFuel = false;

        if (fuelSlider != null)
        {
            fuelSlider.value = currentFuel;
        }

        // При заправке сразу убираем предупреждение
        if (lowFuelWarningUI != null) lowFuelWarningUI.SetActive(false);
        flashTimer = 0f;

        Debug.Log("Бак полностью заправлен!");
    }
}