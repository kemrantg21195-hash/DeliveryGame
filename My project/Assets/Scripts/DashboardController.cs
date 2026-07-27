using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DashboardController : MonoBehaviour
{
    [Header("Ссылка на контроллер машины")]
    public PickupController carController;

    [Header("Элементы Интерфейса (UI)")]
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI gearText;
    public Image rpmFillImage;

    [Header("Настройки цвета тахометра")]
    public Color normalRpmColor = Color.green;
    public Color redlineRpmColor = Color.red;
    public float redlinePercentage = 0.8f;

    [Header("Настройки звука двигателя")]
    [Tooltip("Компонент Audio Source на машине")]
    public AudioSource engineAudioSource;
    [Tooltip("Минимальная высота звука на холостых оборотах")]
    public float minPitch = 0.5f;
    [Tooltip("Максимальная высота звука на отсечке")]
    public float maxPitch = 2.0f;

    private Rigidbody carRigidbody;

    void Start()
    {
        if (carController == null)
        {
            Debug.LogError("Dashboard: Не назначена ссылка на PickupController машины!");
            return;
        }

        carRigidbody = carController.GetComponent<Rigidbody>();

        // Если вы забыли перетащить Audio Source в инспекторе, попробуем найти его на машине автоматически
        if (engineAudioSource == null)
        {
            engineAudioSource = carController.GetComponent<AudioSource>();
        }
    }

    void Update()
    {
        if (carController == null || carRigidbody == null) return;

        // Вычисляем процент заполнения мотора (от 0.0 до 1.0)
        float rpmRatio = carController.CurrentRPM / carController.maxEngineRPM;

        // 1. ОБНОВЛЕНИЕ СКОРОСТИ
        float speedKmH = carRigidbody.linearVelocity.magnitude * 3.6f;
        if (speedText != null)
        {
            speedText.text = Mathf.RoundToInt(speedKmH).ToString() + " km/h";
        }

        // 2. ОБНОВЛЕНИЕ ПЕРЕДАЧИ
        if (gearText != null)
        {
            int currentGear = carController.CurrentGear;
            if (currentGear == -1) { gearText.text = "R"; gearText.color = Color.red; }
            else if (currentGear == 0) { gearText.text = "N"; gearText.color = Color.white; }
            else { gearText.text = currentGear.ToString(); gearText.color = Color.yellow; }
        }

        // 3. ОБНОВЛЕНИЕ ТАХОМЕТРА
        if (rpmFillImage != null)
        {
            rpmFillImage.fillAmount = rpmRatio;

            if (rpmRatio < redlinePercentage)
            {
                rpmFillImage.color = normalRpmColor;
            }
            else
            {
                float redlineRatio = (rpmRatio - redlinePercentage) / (1f - redlinePercentage);
                rpmFillImage.color = Color.Lerp(normalRpmColor, redlineRpmColor, redlineRatio);
            }
        }

        // 4. ДИНАМИЧЕСКОЕ ИЗМЕНЕНИЕ ЗВУКА МОТОРА
        if (engineAudioSource != null)
        {
            // Плавно меняем параметр Pitch (высоту/тональность звука) в зависимости от оборотов мотора.
            // На холостых оборотах (rpmRatio = 0) Pitch будет равен minPitch (низкий бас).
            // На максимальных оборотах (rpmRatio = 1) Pitch станет равен maxPitch (высокий рев).
            engineAudioSource.pitch = Mathf.Lerp(minPitch, maxPitch, rpmRatio);
        }
    }
}

