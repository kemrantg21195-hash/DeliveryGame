using UnityEngine;

public class CarBrakeLights : MonoBehaviour
{
    [Header("Ссылки на источники света")]
    public Light[] brakeLights;

    [Header("Настройки моделей фар")]
    public MeshRenderer[] carMeshRenderers;
    public int materialIndex = 0;

    [Header("Интенсивность свечения (Emission)")]
    public float idleIntensity = 0.5f;
    public float brakeIntensity = 3.0f;

    [Header("Звук тормозов (Аудиозапись)")]
    public AudioSource brakeAudioSource;       // Сюда перетащите Audio Source тормозов
    public float audioFadeSpeed = 3f;          // Скорость постепенного нарастания звука тормозов
    [Range(0f, 1f)] public float maxVolume = 0.8f; // Максимально возможная громкость скрипа

    [Header("Настройки обрезки трека (в секундах)")]
    public float loopStartSeconds = 1.0f;      // С какой секунды играть чистый скрипт колодок
    public float loopEndSeconds = 4.5f;        // На какой секунде возвращать дорожку назад

    private Material[] brakeMaterials;
    private Color baseEmissionColor = Color.red;
    private Rigidbody carRigidbody;            // Для проверки, движется ли машина
    private int startSamples;
    private int endSamples;
    private bool sampleLimitsCalculated = false;
    void Start()
    {
        carRigidbody = GetComponent<Rigidbody>();

        if (carMeshRenderers != null && carMeshRenderers.Length > 0)
        {
            brakeMaterials = new Material[carMeshRenderers.Length];
            for (int i = 0; i < carMeshRenderers.Length; i++)
            {
                if (carMeshRenderers[i] != null && carMeshRenderers[i].materials.Length > materialIndex)
                {
                    brakeMaterials[i] = carMeshRenderers[i].materials[materialIndex];
                    brakeMaterials[i].EnableKeyword("_EMISSION");
                }
            }
        }

        // Страховка параметров звука
        if (brakeAudioSource != null)
        {
            brakeAudioSource.loop = false; // Выключаем встроенный цикл Unity для работы нашей обрезки
            brakeAudioSource.volume = 0f;
        }

        SetBrakeLightsState(false);
    }

    void Update()
    {
        // Проверяем, нажата ли кнопка тормоза (стрелка вниз / пробел)
        bool isBrakePressed = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.Space);

        // Проверяем, движется ли машина вообще (чтобы тормоза не скрипели, когда машина намертво стоит)
        bool isMoving = carRigidbody != null && carRigidbody.linearVelocity.magnitude > 0.5f;

        if (isBrakePressed)
        {
            SetBrakeLightsState(true); // Фары загораются мгновенно при нажатии

            // Звук включается ТОЛЬКО если машина в движении
            if (isMoving)
            {
                ManageBrakeSound(true);
            }
            else
            {
                ManageBrakeSound(false); // Глушим звук, если затормозили до полной остановки
            }
        }
        else
        {
            SetBrakeLightsState(false);
            ManageBrakeSound(false);
        }
    }

    void ManageBrakeSound(bool shouldPlay)
    {
        if (brakeAudioSource == null || brakeAudioSource.clip == null) return;

        // Автоматически переводим секунды в точные аудио-семплы при первом запуске звука
        if (!sampleLimitsCalculated)
        {
            int frequency = brakeAudioSource.clip.frequency;
            startSamples = Mathf.FloorToInt(loopStartSeconds * frequency);
            endSamples = Mathf.FloorToInt(loopEndSeconds * frequency);

            // Защита от выхода за границы самого аудиофайла
            endSamples = Mathf.Min(endSamples, brakeAudioSource.clip.samples - 1);
            sampleLimitsCalculated = true;
        }

        if (shouldPlay)
        {
            if (!brakeAudioSource.isPlaying)
            {
                // Запускаем звук строго с нужного семпла
                brakeAudioSource.timeSamples = startSamples;
                brakeAudioSource.Play();
            }

            // 1. Постепенное усиление звука тормозов
            brakeAudioSource.volume = Mathf.Lerp(brakeAudioSource.volume, maxVolume, Time.deltaTime * audioFadeSpeed);

            // 2. БЕСШОВНОЕ ПЕРЕКЛЮЧЕНИЕ ЧЕРЕЗ СЕМПЛЫ:
            // Считываем текущий проигрываемый семпл. Это работает в разы точнее, чем секунды.
            if (brakeAudioSource.timeSamples >= endSamples || brakeAudioSource.timeSamples < startSamples)
            {
                // Если файл дошел до границы, плавно перематываем на стартовый семпл
                brakeAudioSource.timeSamples = startSamples;
            }
        }
        else
        {
            // Плавно глушим звук, если педаль отпущена
            brakeAudioSource.volume = Mathf.Lerp(brakeAudioSource.volume, 0f, Time.deltaTime * audioFadeSpeed * 2f);

            if (brakeAudioSource.volume <= 0.01f && brakeAudioSource.isPlaying)
            {
                brakeAudioSource.Stop();
            }
        }
    }

    void SetBrakeLightsState(bool isBraking)
    {
        if (brakeLights != null)
        {
            foreach (Light light in brakeLights)
            {
                if (light != null) light.enabled = isBraking;
            }
        }

        if (brakeMaterials != null)
        {
            float currentIntensity = isBraking ? brakeIntensity : idleIntensity;
            Color finalEmissionColor = baseEmissionColor * currentIntensity;

            foreach (Material mat in brakeMaterials)
            {
                if (mat != null) mat.SetColor("_EmissionColor", finalEmissionColor);
            }
        }
    }
}
