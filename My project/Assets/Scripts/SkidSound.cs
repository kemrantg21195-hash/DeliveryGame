using UnityEngine;

public class WheelSkidSound : MonoBehaviour
{
    [Header("Ссылки на компоненты")]
    public WheelCollider[] wheelColliders;
    public AudioSource skidAudioSource;

    [Header("Настройки чувствительности")]
    public float slipThreshold = 0.4f;
    public float fadeSpeed = 5f;

    [Header("Зависимость от вращения колес")]
    public float maxWheelSpeedForAudio = 30f; // Скорость колес (в м/с), при которой звук на максимуме (30 м/с ≈ 108 км/ч)
    public float minPitch = 0.7f;
    public float maxPitch = 1.3f;

    void Update()
    {
        float maxSlip = 0f;
        float totalWheelSpeed = 0f;
        int activeWheelsCount = 0;

        // Проверяем каждое колесо
        foreach (WheelCollider wheel in wheelColliders)
        {
            // 1. Рассчитываем линейную скорость вращения конкретного колеса через его RPM и радиус
            // Формула: (RPM * 2 * PI * Radius) / 60 секунд
            float wheelLinearSpeed = Mathf.Abs((wheel.rpm * 2 * Mathf.PI * wheel.radius) / 60f);

            totalWheelSpeed += wheelLinearSpeed;
            activeWheelsCount++;

            // 2. Проверяем пробуксовку
            if (wheel.GetGroundHit(out WheelHit hit))
            {
                float currentSlip = Mathf.Max(Mathf.Abs(hit.forwardSlip), Mathf.Abs(hit.sidewaysSlip));
                if (currentSlip > maxSlip)
                {
                    maxSlip = currentSlip;
                }
            }
        }

        // Блокировка звука при нажатии на тормоз (стрелка вниз или пробел)
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.Space))
        {
            maxSlip = 0f;
        }

        // Если есть пробуксовка
        if (maxSlip > slipThreshold && activeWheelsCount > 0)
        {
            if (!skidAudioSource.isPlaying)
            {
                skidAudioSource.Play();
            }

            // Находим среднюю скорость вращения всех колес машины
            float averageWheelSpeed = totalWheelSpeed / activeWheelsCount;

            // Переводим скорость колес в коэффициент от 0.0 до 1.0
            float speedFactor = Mathf.Clamp01(averageWheelSpeed / maxWheelSpeedForAudio);

            // Громкость зависит от силы заноса и скорости вращения колес
            float targetVolume = Mathf.Clamp01(maxSlip) * speedFactor;
            skidAudioSource.volume = Mathf.Lerp(skidAudioSource.volume, targetVolume, Time.deltaTime * fadeSpeed);

            // Тон (Pitch) теперь напрямую зависит от того, насколько быстро крутятся колеса
            float targetPitch = Mathf.Lerp(minPitch, maxPitch, speedFactor);
            skidAudioSource.pitch = Mathf.Lerp(skidAudioSource.pitch, targetPitch, Time.deltaTime * fadeSpeed);
        }
        else
        {
            // Плавно глушим звук
            skidAudioSource.volume = Mathf.Lerp(skidAudioSource.volume, 0f, Time.deltaTime * fadeSpeed);

            if (skidAudioSource.volume <= 0.01f && skidAudioSource.isPlaying)
            {
                skidAudioSource.Stop();
            }
        }
    }
}

