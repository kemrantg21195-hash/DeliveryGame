using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AudioSource))]
public class CarCollisionSound : MonoBehaviour
{
    [Header("Ссылки на компоненты")]
    public AudioSource impactAudioSource; // Сюда перетащите Audio Source со звуком удара машины

    [Header("Настройки фильтрации ударов")]
    [Tooltip("Минимальная сила удара для срабатывания звука (чтобы заглушить мелкую тряску подвески)")]
    public float minImpactForce = 3.0f;

    [Tooltip("Тег, который установлен на префабах ваших коробок груза")]
    public string cargoTag = "Cargo";

    void Start()
    {
        // Автоматически находим AudioSource, если забыли привязать в инспекторе
        if (impactAudioSource == null)
        {
            impactAudioSource = GetComponent<AudioSource>();
        }

        // Принудительно отключаем Play On Awake, чтобы звук не стрелял при старте сцены
        if (impactAudioSource != null)
        {
            impactAudioSource.playOnAwake = false;
        }

        // Проверяем, что на машине (этом объекте) стоит правильный тег Player
        if (!gameObject.CompareTag("Player"))
        {
            Debug.LogWarning($"[ВНИМАНИЕ] На объекте машины {gameObject.name} не установлен тег 'Player'! Пожалуйста, установите его в самом верху инспектора.");
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // Страховка: если столкнулись с пустым объектом, выходим
        if (collision.gameObject == null) return;

        // Рассчитываем силу физического столкновения машины
        float impactForce = collision.relativeVelocity.magnitude;

        // Если сила удара превышает порог чувствительности (машина во что-то врезалась)
        if (impactForce >= minImpactForce)
        {
            // По умолчанию громкость рассчитывается на 100% (коэффициент = 1)
            float volumeMultiplier = 1.0f;

            // ⚡ ЖЕЛЕЗНАЯ ПРОВЕРКА ТЕГОВ МЕЖДУ PLAYER И CARGO:
            // Проверяем, есть ли тег Cargo у объекта, его непосредственного родителя или в самом корне префаба
            if (collision.gameObject.CompareTag(cargoTag) ||
                collision.gameObject.tag == "Cargo" ||
                (collision.transform.parent != null && collision.transform.parent.CompareTag(cargoTag)) ||
                (collision.transform.root != null && collision.transform.root.CompareTag(cargoTag)))
            {
                // Если машина (Player) столкнулась с грузом (Cargo) — полностью ОБНУЛЯЕМ громкость звука
                volumeMultiplier = 0f;
            }

            // Воспроизводим звук только если итоговый коэффициент громкости выше нуля (то есть это стена, столб или забор)
            if (impactAudioSource != null && impactAudioSource.clip != null && volumeMultiplier > 0.01f)
            {
                // Рассчитываем базовую громкость от силы удара (чем сильнее авария — тем громче звук)
                float calculatedVolume = Mathf.Clamp01((impactForce - minImpactForce) / 15f);

                // Немного меняем тональность (pitch), чтобы звуки ударов не были монотонными
                impactAudioSource.pitch = Random.Range(0.85f, 1.15f);

                // Воспроизводим финальный отфильтрованный звук
                impactAudioSource.PlayOneShot(impactAudioSource.clip, calculatedVolume);
            }
        }
    }
}
