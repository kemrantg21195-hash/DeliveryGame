using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro; // ОБЯЗАТЕЛЬНО для работы с TextMeshPro

public class CargoManager : MonoBehaviour
{
    [Header("Ссылки на объекты")]
    public GameObject[] cargoPrefabs; // Массив разных грузов
    public Transform cargoSpawnPoint;
    private bool isLate = false; // Отслеживает, опоздали мы или нет

    [Header("Зоны на сцене")]
    public GameObject pickupZoneObject;
    public GameObject dropoffZoneObject;
    public GameObject dropoffVisual;

    [Header("Точки появления (Spawn Points)")]
    public Transform[] pickupSpawnPoints;
    public Transform[] dropoffSpawnPoints;

    [Header("Настройки утери")]
    public float lossDistance = 2.5f;

    [Header("Очки и Таймер")]
    public int currentScore = 0;
    public int deliveryReward = 200;
    public int lossPenalty = 100;
    public TMP_Text scoreText;
    public TMP_Text timerText;

    [Header("Умный таймер")]
    public float secondsPerMeter = 0.5f; // Сколько секунд даем за каждый метр пути
    public float baseTimeBuffer = 15f;   // Несгораемый запас времени (на разгон и парковку)
    private float deliveryTimeLimit;     // Теперь это скрытая переменная, скрипт считает ее сам

    [Header("Настройки погрузки")]
    public float pickupHoldTime = 2f; // Сколько секунд нужно простоять в зоне
    private Coroutine pickupCoroutine;  // Ссылка на запущенный таймер
    private bool isLoadingCargo = false; // Флаг, что прямо сейчас идет погрузка

    [Header("Опоздание")]
    public int penaltyPerSecond = 5; // Штраф за каждую секунду
    private int appliedTimePenalty = 0; // Сколько штрафа уже начислено в этом рейсе

    [Header("Защита от багов")]
    public float spawnCooldown = 2f;

    private GameObject spawnedCargoRoot;
    private List<GameObject> activeCargoPieces = new List<GameObject>(); // Список всех коробок в кузове
    private int initialPieceCount = 1; // Сколько коробок было изначально

    private bool isCooldown = false;
    private Rigidbody truckRigidbody;
    private float currentTimer;
    private bool isTimerRunning = false;

    private Transform currentPickupPoint;
    private Transform lastDropoffPoint;

    // Временная переменная для очистки сообщений
    private Coroutine clearMessageCoroutine;

    private void Start()
    {
        truckRigidbody = GetComponent<Rigidbody>();

        if (dropoffZoneObject != null) dropoffZoneObject.SetActive(false);
        if (dropoffVisual != null) dropoffVisual.SetActive(false);
        SetFirstPickupZone();

        if (timerText != null) timerText.text = "";
        UpdateScoreUI();
    }

    private void SetFirstPickupZone()
    {
        if (pickupSpawnPoints != null && pickupSpawnPoints.Length > 0 && pickupZoneObject != null)
        {
            int index = Random.Range(0, pickupSpawnPoints.Length);
            currentPickupPoint = pickupSpawnPoints[index];

            pickupZoneObject.transform.position = currentPickupPoint.position;
            pickupZoneObject.transform.rotation = currentPickupPoint.rotation;
            pickupZoneObject.SetActive(true);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Pickup") && activeCargoPieces.Count == 0 && !isCooldown && !isLoadingCargo)
        {
            isTimerRunning = false;
            isLate = false;

            pickupCoroutine = StartCoroutine(PickupHoldRoutine());
        }
        else if (other.CompareTag("Dropoff") && activeCargoPieces.Count > 0)
        {
            DeliverCargo();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Pickup"))
        {
            if (pickupCoroutine != null)
            {
                StopCoroutine(pickupCoroutine);
                pickupCoroutine = null;
                isLoadingCargo = false;

                ShowTemporaryMessage("Загрузка отменена!", 2f);
            }
        }
    }

    private IEnumerator PickupHoldRoutine()
    {
        isLoadingCargo = true;
        float timer = pickupHoldTime;
        while (timer > 0)
        {
            if (timerText != null)
            {
                timerText.text = "Загрузка: " + timer.ToString("F1") + "с";
            }
            timer -= Time.deltaTime;
            yield return null;
        }

        if (timerText != null) timerText.text = "";
        isLoadingCargo = false;
        pickupCoroutine = null;

        StartCoroutine(SpawnCargoRoutine());
    }

    // ⚡ ИСПРАВЛЕННЫЙ МЕТОД ПОГРУЗКИ (БЕЗЗВУЧНЫЙ СПАВН)
    private IEnumerator SpawnCargoRoutine()
    {
        isCooldown = true;
        int randomCargoIndex = Random.Range(0, cargoPrefabs.Length);

        // Спавним корневой объект груза
        spawnedCargoRoot = Instantiate(cargoPrefabs[randomCargoIndex], cargoSpawnPoint.position, cargoSpawnPoint.rotation);

        Rigidbody[] pieces = spawnedCargoRoot.GetComponentsInChildren<Rigidbody>();
        activeCargoPieces.Clear();

        foreach (Rigidbody rb in pieces)
        {
            // ЗАЩИТА 1: Полностью выключаем звук удара на коробке во время спавна, чтобы она не гремела
            AudioSource boxAudio = rb.gameObject.GetComponent<AudioSource>();
            if (boxAudio != null) boxAudio.enabled = false;

            // Также выключим кастомный скрипт звука удара (если он у вас есть на коробках)
            MonoBehaviour impactScript = rb.gameObject.GetComponent("CarCollisionSound") as MonoBehaviour;
            if (impactScript != null) impactScript.enabled = false;

            // ЗАЩИТА 2: Временно включаем кинематику, чтобы коробки не падали рывком, создавая физический удар
            rb.isKinematic = true;
            activeCargoPieces.Add(rb.gameObject);

            // Мягко привязываем суставом к кузову машины
            FixedJoint joint = rb.gameObject.AddComponent<FixedJoint>();
            joint.connectedBody = truckRigidbody;

            // Отключаем кинематику обратно, теперь они плавно соединены с машиной без падения
            rb.isKinematic = false;
        }

        initialPieceCount = activeCargoPieces.Count;
        if (initialPieceCount == 0) initialPieceCount = 1;
        if (pickupZoneObject != null) pickupZoneObject.SetActive(false);
        SetRandomDropoffZone();

        float distance = 0f;
        if (lastDropoffPoint != null)
        {
            distance = Vector3.Distance(transform.position, lastDropoffPoint.position);
        }

        if (baseTimeBuffer <= 0) baseTimeBuffer = 15f;
        if (secondsPerMeter <= 0) secondsPerMeter = 0.5f;
        deliveryTimeLimit = baseTimeBuffer + (distance * secondsPerMeter);
        Debug.Log("🚚 Дистанция: " + Mathf.RoundToInt(distance) + "м. | Выдано времени: " + Mathf.RoundToInt(deliveryTimeLimit) + "с.");

        isLate = false;
        appliedTimePenalty = 0;
        currentTimer = deliveryTimeLimit;

        // Ждем 1 секунду утряски груза на суставах в полной тишине
        yield return new WaitForSeconds(1f);

        foreach (GameObject piece in activeCargoPieces)
        {
            if (piece != null)
            {
                FixedJoint joint = piece.GetComponent<FixedJoint>();
                if (joint != null) Destroy(joint);

                // ⚡ ВКЛЮЧАЕМ ФИЗИЧЕСКИЙ ЗВУК ОБРАТНО: Теперь, если машина врежется во время езды, коробки загремят!
                AudioSource boxAudio = piece.GetComponent<AudioSource>();
                if (boxAudio != null) boxAudio.enabled = true;

                MonoBehaviour impactScript = piece.GetComponent("CargoCollisionSound") as MonoBehaviour;
                if (impactScript != null) impactScript.enabled = true;
            }
        }

        isTimerRunning = true;
        float remainingCooldown = Mathf.Max(0, spawnCooldown - 1f);
        if (remainingCooldown > 0) yield return new WaitForSeconds(remainingCooldown);

        isCooldown = false;
    }

    private void Update()
    {
        if (isTimerRunning)
        {
            currentTimer -= Time.deltaTime;

            if (currentTimer > 0)
            {
                if (timerText != null) timerText.text = "Время: " + Mathf.CeilToInt(currentTimer) + "с";
            }
            else
            {
                isLate = true;
                float secondsLate = -currentTimer;
                int totalExpectedPenalty = Mathf.FloorToInt(secondsLate * penaltyPerSecond);

                if (totalExpectedPenalty > deliveryReward)
                {
                    totalExpectedPenalty = deliveryReward;
                }

                int penaltyToApply = totalExpectedPenalty - appliedTimePenalty;

                if (penaltyToApply > 0)
                {
                    currentScore -= penaltyToApply;
                    appliedTimePenalty = totalExpectedPenalty;
                    UpdateScoreUI();
                }

                if (timerText != null) timerText.text = "Опоздание: -" + appliedTimePenalty;
            }
        }

        if (activeCargoPieces.Count > 0)
        {
            for (int i = activeCargoPieces.Count - 1; i >= 0; i--)
            {
                GameObject piece = activeCargoPieces[i];
                if (piece == null) continue;

                if (Vector3.Distance(cargoSpawnPoint.position, piece.transform.position) > lossDistance)
                {
                    LoseSinglePiece(piece);
                    activeCargoPieces.RemoveAt(i);
                }
            }

            if (activeCargoPieces.Count == 0 && spawnedCargoRoot != null)
            {
                isTimerRunning = false;
                ShowTemporaryMessage("Груз полностью утерян!", 2f);
                if (spawnedCargoRoot != null) Destroy(spawnedCargoRoot);
                ResetZones();
            }
        }
    }



    private void LoseSinglePiece(GameObject piece)
    {
        // Вычисляем штраф за один кусочек
        int piecePenalty = lossPenalty / initialPieceCount;
        currentScore -= piecePenalty;
        UpdateScoreUI();

        Destroy(piece); // Удаляем упавшую коробку
    }

    private void TimeOut()
    {
        isTimerRunning = false;
        if (timerText != null) timerText.text = "Время вышло!";

        // Штрафуем за те коробки, которые остались в кузове
        int piecePenalty = lossPenalty / initialPieceCount;
        currentScore -= (piecePenalty * activeCargoPieces.Count);
        UpdateScoreUI();

        if (spawnedCargoRoot != null) Destroy(spawnedCargoRoot);
        activeCargoPieces.Clear();

        ResetZones();
    }

    private void DeliverCargo()
    {
        isTimerRunning = false;

        // Считаем награду только за ТЕ КОРОБКИ, КОТОРЫЕ ДОЕХАЛИ
        int pieceReward = deliveryReward / initialPieceCount;
        int totalRewardEarned = pieceReward * activeCargoPieces.Count;

        currentScore += totalRewardEarned;

        ShowTemporaryMessage("Сдано: " + activeCargoPieces.Count + " из " + initialPieceCount, 2f);
        UpdateScoreUI();

        if (spawnedCargoRoot != null) Destroy(spawnedCargoRoot);
        activeCargoPieces.Clear();

        ResetZones();
    }

    private void ResetZones()
    {
        if (dropoffZoneObject != null) dropoffZoneObject.SetActive(false);
        if (dropoffVisual != null) dropoffVisual.SetActive(false);

        SetRandomPickupZone();
    }

    private void SetRandomPickupZone()
    {
        if (pickupSpawnPoints != null && pickupSpawnPoints.Length > 0 && pickupZoneObject != null)
        {
            int index = Random.Range(0, pickupSpawnPoints.Length);
            Transform selectedPickupPoint = pickupSpawnPoints[index];

            if (pickupSpawnPoints.Length > 1 && lastDropoffPoint != null)
            {
                int attempts = 0;
                while (selectedPickupPoint == lastDropoffPoint && attempts < 20)
                {
                    index = Random.Range(0, pickupSpawnPoints.Length);
                    selectedPickupPoint = pickupSpawnPoints[index];
                    attempts++;
                }
            }

            currentPickupPoint = selectedPickupPoint;
            pickupZoneObject.transform.position = currentPickupPoint.position;
            pickupZoneObject.transform.rotation = currentPickupPoint.rotation;
            pickupZoneObject.SetActive(true);
        }
    }

    private void SetRandomDropoffZone()
    {
        if (dropoffSpawnPoints != null && dropoffSpawnPoints.Length > 0 && dropoffZoneObject != null)
        {
            int index = Random.Range(0, dropoffSpawnPoints.Length);
            Transform selectedDropoffPoint = dropoffSpawnPoints[index];

            if (dropoffSpawnPoints.Length > 1)
            {
                int attempts = 0;
                while (selectedDropoffPoint == currentPickupPoint && attempts < 20)
                {
                    index = Random.Range(0, dropoffSpawnPoints.Length);
                    selectedDropoffPoint = dropoffSpawnPoints[index];
                    attempts++;
                }
            }

            lastDropoffPoint = selectedDropoffPoint;
            dropoffZoneObject.transform.position = lastDropoffPoint.position;
            dropoffZoneObject.transform.rotation = lastDropoffPoint.rotation;

            dropoffZoneObject.SetActive(true);
            if (dropoffVisual != null) dropoffVisual.SetActive(true);
        }
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null) scoreText.text = "Очки: " + currentScore;
    }

    // Сообщаем навигатору, что у нас еще есть хотя бы одна коробка в кузове
    public bool HasCargo() { return activeCargoPieces.Count > 0; }

    public void ForceDropCargo()
    {
        if (activeCargoPieces.Count > 0)
        {
            TimeOut(); // Используем метод TimeOut для снятия очков за оставшийся груз
        }
    }

    // Умная функция для временных сообщений
    private void ShowTemporaryMessage(string message, float delay = 2f)
    {
        if (timerText != null)
        {
            timerText.text = message;

            // Если предыдущее сообщение еще висит - отменяем его удаление
            if (clearMessageCoroutine != null) StopCoroutine(clearMessageCoroutine);

            // Запускаем таймер удаления нового сообщения
            clearMessageCoroutine = StartCoroutine(ClearMessageRoutine(delay));
        }
    }

    private IEnumerator ClearMessageRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Очищаем текст только если сейчас не идет доставка и не идет загрузка
        if (timerText != null && !isTimerRunning && !isLoadingCargo)
        {
            timerText.text = "";
        }
    }
}