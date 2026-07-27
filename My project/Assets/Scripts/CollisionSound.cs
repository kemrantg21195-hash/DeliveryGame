using UnityEngine;

public class CarCollisionSound : MonoBehaviour
{
    [Header("Настройки звука")]
    public AudioSource crashAudioSource; // Сюда перетаскиваем наш Audio Source удара

    [Header("Физика удара")]
    public float minCollisionForce = 2f; // Минимальная сила удара, чтобы звук вообще сработал
    public float maxCollisionForce = 15f; // Сила, при которой звук будет на максимальной громкости

    // Этот метод Unity вызывает автоматически, когда твердое тело (Rigidbody) машины во что-то врезается
    void OnCollisionEnter(Collision collision)
    {
        // Вычисляем относительную скорость удара
        float hitForce = collision.relativeVelocity.magnitude;

        // Если удар сильнее минимального порога
        if (hitForce > minCollisionForce)
        {
            // Рассчитываем громкость: чем сильнее удар, тем ближе к 1.0
            // Mathf.InverseLerp плавно переводит силу удара в диапазон от 0 до 1
            float targetVolume = Mathf.InverseLerp(minCollisionForce, maxCollisionForce, hitForce);

            // Принудительно задаем громкость компоненту
            crashAudioSource.volume = targetVolume;

            // Воспроизводим звук ОДИН раз (не сбивая текущий, если машина ударилась по касательной дважды)
            crashAudioSource.PlayOneShot(crashAudioSource.clip);
        }
    }
}
