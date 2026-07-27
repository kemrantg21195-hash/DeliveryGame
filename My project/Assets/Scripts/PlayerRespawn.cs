using UnityEngine;

public class PlayerRespawn : MonoBehaviour
{
    // Массив для хранения всех доступных точек спавна
    public Transform[] spawnPoints;

    void Start()
    {
        // Вызываем респавн при старте игры
        Respawn();
    }

    public void Respawn()
    {
        // Проверяем, добавили ли мы вообще точки в список
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("Список точек спавна пуст! Добавьте их в инспекторе машины.");
            return;
        }

        // 1. Выбираем случайную точку из массива
        int randomIndex = Random.Range(0, spawnPoints.Length);
        Transform selectedSpawnPoint = spawnPoints[randomIndex];

        // 2. Пытаемся получить физику машины
        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            // Временно гасим всю скорость машины
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 3. Мгновенно переносим координаты на выбранную случайную точку
        transform.position = selectedSpawnPoint.position;
        transform.rotation = selectedSpawnPoint.rotation;

        // 4. Синхронизируем физику
        Physics.SyncTransforms();

        // 5. Включаем физику обратно
        if (rb != null)
        {
            rb.isKinematic = false;
        }
    }
}

