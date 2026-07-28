using UnityEngine;

public class WheelSkidSound : MonoBehaviour
{
    [Header("Ссылки на компоненты")]
    public WheelCollider[] wheelColliders;
    public AudioSource skidAudioSource;

    [Header("Аудиоклипы поверхностей")]
    public AudioClip asphaltSkidClip;
    public AudioClip gravelSkidClip;

    [Header("Настройки цикла Гравия (Crossfade)")]
    [Tooltip("Длина проигрываемого куска гравия в секундах до перезапуска следующего слоя")]
    public float gravelLoopDuration = 2.5f;
    private float gravelTimer = 0f;

    [Header("Настройки чувствительности")]
    public float slipThreshold = 0.4f;
    public float fadeSpeed = 5f;

    [Header("Зависимость от вращения колес")]
    public float maxWheelSpeedForAudio = 30f;
    public float minPitch = 0.7f;
    public float maxPitch = 1.3f;

    private DynamicWeatherSystem weatherSystem;
    private bool isOffRoad = false;
    private float currentVolumeTarget = 0f;

    void Start()
    {
        weatherSystem = Object.FindFirstObjectByType<DynamicWeatherSystem>();

        if (skidAudioSource != null)
        {
            skidAudioSource.loop = false; // Выключаем стандартный Loop, управление полностью в коде
        }
    }

    void Update()
    {
        float maxSlip = 0f;
        float totalWheelSpeed = 0f;
        int activeWheelsCount = 0;
        bool currentFrameOffRoad = false;

        // 1. Проверяем физику колес и поверхность Plane
        foreach (WheelCollider wheel in wheelColliders)
        {
            if (wheel == null) continue;

            float wheelLinearSpeed = Mathf.Abs((wheel.rpm * 2 * Mathf.PI * wheel.radius) / 60f);
            totalWheelSpeed += wheelLinearSpeed;
            activeWheelsCount++;

            if (wheel.GetGroundHit(out WheelHit hit))
            {
                float currentSlip = Mathf.Max(Mathf.Abs(hit.forwardSlip), Mathf.Abs(hit.sidewaysSlip));
                if (currentSlip > maxSlip) maxSlip = currentSlip;

                if (hit.collider != null && hit.collider.name.Contains("Plane"))
                {
                    currentFrameOffRoad = true;
                }
            }
        }

        isOffRoad = currentFrameOffRoad;

        // Блокировка при торможении
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.Space))
        {
            maxSlip = 0f;
        }

        // 2. РАСЧЕТ ЦЕЛЕВОЙ ГРОМКОСТИ И ТОНАЛЬНОСТИ
        if (maxSlip > slipThreshold && activeWheelsCount > 0)
        {
            float averageWheelSpeed = totalWheelSpeed / activeWheelsCount;
            float speedFactor = Mathf.Clamp01(averageWheelSpeed / maxWheelSpeedForAudio);

            currentVolumeTarget = Mathf.Clamp01(maxSlip) * speedFactor;

            if (skidAudioSource != null)
            {
                float targetPitch = Mathf.Lerp(minPitch, maxPitch, speedFactor);
                skidAudioSource.pitch = Mathf.Lerp(skidAudioSource.pitch, targetPitch, Time.deltaTime * fadeSpeed);
            }
        }
        else
        {
            currentVolumeTarget = 0f;
        }

        // Плавно меняем общую громкость источника звука
        if (skidAudioSource != null)
        {
            skidAudioSource.volume = Mathf.Lerp(skidAudioSource.volume, currentVolumeTarget, Time.deltaTime * fadeSpeed);
        }

        // 3. УМНАЯ СИСТЕМА ВОСПРОИЗВЕДЕНИЯ БЕЗ ПАУЗ
        if (currentVolumeTarget > 0.01f && skidAudioSource != null)
        {
            if (isOffRoad)
            {
                // --- ГРАВИЙ (Бесшовная склейка на лету) ---
                gravelTimer += Time.deltaTime;

                // Если таймер круга истек или звук вообще не играет, накладываем новый слой гравия
                if (gravelTimer >= gravelLoopDuration || !skidAudioSource.isPlaying)
                {
                    // PlayOneShot запускает звук параллельным слоем, не обрывая прошлый шуршащий хвост!
                    skidAudioSource.PlayOneShot(gravelSkidClip);
                    gravelTimer = 0f; // Сбрасываем таймер для следующей склейки
                }
            }
            else
            {
                // --- АСФАЛЬТ (Классический бесшовный перезапуск) ---
                gravelTimer = 0f; // Обнуляем гравийный таймер

                if (skidAudioSource.clip != asphaltSkidClip)
                {
                    skidAudioSource.Stop();
                    skidAudioSource.clip = asphaltSkidClip;
                }

                if (!skidAudioSource.isPlaying && asphaltSkidClip != null)
                {
                    skidAudioSource.timeSamples = 0;
                    skidAudioSource.Play();
                }

                // Ручной бесшовный сброс для длинного трека асфальта
                if (skidAudioSource.clip == asphaltSkidClip && skidAudioSource.timeSamples >= asphaltSkidClip.samples - 5)
                {
                    skidAudioSource.timeSamples = 0;
                }
            }
        }
        else
        {
            // Если машина больше не буксует, плавно гасим остатки звуков
            gravelTimer = 0f;
            if (skidAudioSource != null && skidAudioSource.volume <= 0.01f && skidAudioSource.isPlaying)
            {
                skidAudioSource.Stop();
                skidAudioSource.clip = null;
            }
        }
    }
}
