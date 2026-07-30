using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Collections;

public enum GameMode
{
    Endless,
    Challenge
}

public class CargoManager : MonoBehaviour
{
    [Header("Режим игры")]
    public GameMode currentMode = GameMode.Endless;
    [Tooltip("Сколько заказов нужно выполнить в режиме Challenge")]
    public int maxOrders = 5;

    [Header("Настройки Медалей")]
    public int scoreForGold = 1200;
    public int scoreForSilver = 800;
    public int scoreForBronze = 500;
    public int scoreForWood = 200;

    private int ordersCompleted = 0;

    [Header("Финальный UI (для Challenge)")]
    public GameObject gameOverPanel;
    public UnityEngine.UI.Image gameOverBGImage; // ⚡ Ссылка на Image фона панели
    public TMP_Text resultText;
    public GameObject medalImage;

    [Header("Цвета фона для медалей")]
    public Color goldColor = new Color(1f, 0.84f, 0f, 0.9f);         // Золотой
    public Color silverColor = new Color(0.75f, 0.75f, 0.75f, 0.9f);   // Серебряный
    public Color bronzeColor = new Color(0.8f, 0.5f, 0.2f, 0.9f);     // Бронзовый
    public Color woodColor = new Color(0.45f, 0.25f, 0.1f, 0.9f);     // Деревянный
    public Color failColor = new Color(0.4f, 0f, 0f, 0.9f);           // Темно-красный (Провал)

    [Header("Ссылки на объекты")]
    public GameObject[] cargoPrefabs;
    public Transform cargoSpawnPoint;

    [Header("Зоны на сцене")]
    public GameObject pickupZoneObject;
    public GameObject dropoffZoneObject;
    public GameObject dropoffVisual;

    [Header("Точки появления")]
    public Transform[] pickupSpawnPoints;
    public Transform[] dropoffSpawnPoints;

    [Header("Настройки утери")]
    public float lossDistance = 2.5f;

    [Header("Очки и UI")]
    public int currentScore = 0;
    public int deliveryReward = 200;
    public int lossPenalty = 100;
    public TMP_Text scoreText;
    public TMP_Text timerText;

    [Header("Умный таймер")]
    public float secondsPerMeter = 0.5f;
    public float baseTimeBuffer = 15f;
    private float deliveryTimeLimit;

    [Header("Опоздание")]
    public int penaltyPerSecond = 5;
    private bool isLate = false;

    [Header("Настройки погрузки")]
    public float pickupHoldTime = 2f;
    public float spawnCooldown = 2f;

    private Coroutine pickupCoroutine;
    private Coroutine clearMessageCoroutine;
    private bool isLoadingCargo = false;

    private GameObject spawnedCargoRoot;
    private List<GameObject> activeCargoPieces = new List<GameObject>();
    private int initialPieceCount = 1;

    private bool isCooldown = false;
    private Rigidbody truckRigidbody;
    private float currentTimer;
    private bool isTimerRunning = false;

    private Transform currentPickupPoint;
    private Transform lastDropoffPoint;

    // --- НОВЫЕ ПЕРЕМЕННЫЕ ДЛЯ БУФЕРА ШТРАФОВ ---
    private int currentOrderLostPenalty = 0;
    private int currentOrderTimePenalty = 0;

    private void Start()
    {
        truckRigidbody = GetComponent<Rigidbody>();

        if (dropoffZoneObject != null) dropoffZoneObject.SetActive(false);
        if (dropoffVisual != null) dropoffVisual.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

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
        if (other.CompareTag("Pickup"))
        {
            isTimerRunning = false;
            isLate = false;

            if (activeCargoPieces.Count == 0 && !isCooldown && !isLoadingCargo)
            {
                pickupCoroutine = StartCoroutine(PickupHoldRoutine());
            }
        }
        else if (other.CompareTag("Dropoff") && activeCargoPieces.Count > 0)
        {
            DeliverCargo();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Pickup") && pickupCoroutine != null)
        {
            StopCoroutine(pickupCoroutine);
            pickupCoroutine = null;
            isLoadingCargo = false;
            ShowTemporaryMessage("Загрузка отменена!", 2f);
        }
    }

    private IEnumerator PickupHoldRoutine()
    {
        isLoadingCargo = true;
        float timer = pickupHoldTime;

        while (timer > 0)
        {
            if (timerText != null) timerText.text = "Загрузка: " + timer.ToString("F1") + "с";
            timer -= Time.deltaTime;
            yield return null;
        }

        if (timerText != null) timerText.text = "";
        isLoadingCargo = false;
        pickupCoroutine = null;

        StartCoroutine(SpawnCargoRoutine());
    }

    private IEnumerator SpawnCargoRoutine()
    {
        isCooldown = true;

        // Сбрасываем штрафы при новом заказе
        currentOrderLostPenalty = 0;
        currentOrderTimePenalty = 0;

        int randomCargoIndex = Random.Range(0, cargoPrefabs.Length);
        spawnedCargoRoot = Instantiate(cargoPrefabs[randomCargoIndex], cargoSpawnPoint.position, cargoSpawnPoint.rotation);

        Rigidbody[] pieces = spawnedCargoRoot.GetComponentsInChildren<Rigidbody>();
        activeCargoPieces.Clear();

        foreach (Rigidbody rb in pieces)
        {
            rb.isKinematic = false;
            activeCargoPieces.Add(rb.gameObject);

            FixedJoint joint = rb.gameObject.AddComponent<FixedJoint>();
            joint.connectedBody = truckRigidbody;
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

        isLate = false;
        currentTimer = deliveryTimeLimit;

        yield return new WaitForSeconds(1f);

        foreach (GameObject piece in activeCargoPieces)
        {
            if (piece != null)
            {
                FixedJoint joint = piece.GetComponent<FixedJoint>();
                if (joint != null) Destroy(joint);
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
                // Время вышло: просто записываем штраф в буфер, но НЕ отнимаем от общего счета
                isLate = true;
                float secondsLate = -currentTimer;

                currentOrderTimePenalty = Mathf.FloorToInt(secondsLate * penaltyPerSecond);

                // Ограничиваем максимальный штраф за время размером награды
                if (currentOrderTimePenalty > deliveryReward) currentOrderTimePenalty = deliveryReward;

                if (timerText != null) timerText.text = "Штраф за время: -" + currentOrderTimePenalty;
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

                // Считаем итоги перед удалением
                CalculateAndApplyOrderScore();

                ShowTemporaryMessage("Груз полностью утерян!", 2f);
                if (spawnedCargoRoot != null) Destroy(spawnedCargoRoot);

                ProcessOrderCompletion();
            }
        }
    }

    private void LoseSinglePiece(GameObject piece)
    {
        // Не трогаем общий счет! Просто записываем штраф в буфер текущего рейса
        int piecePenalty = lossPenalty / initialPieceCount;
        currentOrderLostPenalty += piecePenalty;

        Destroy(piece);
    }

    private void DeliverCargo()
    {
        isTimerRunning = false;

        // Сводим дебет с кредитом
        CalculateAndApplyOrderScore();

        ShowTemporaryMessage("Сдано: " + activeCargoPieces.Count + " из " + initialPieceCount, 2f);

        if (spawnedCargoRoot != null) Destroy(spawnedCargoRoot);
        activeCargoPieces.Clear();

        ProcessOrderCompletion();
    }

    // --- НОВЫЙ МЕТОД ПОДСЧЕТА ИТОГОВ РЕЙСА ---
    // --- НОВЫЙ МЕТОД ПОДСЧЕТА ИТОГОВ РЕЙСА ---
    private void CalculateAndApplyOrderScore()
    {
        // 1. Даем полную базовую награду, если довезли хотя бы один ящик
        int baseReward = 0;
        if (activeCargoPieces.Count > 0)
        {
            baseReward = deliveryReward; // Берем полную сумму (200)
        }

        // 2. Считаем чистую прибыль: Награда (200) минус Штрафы
        int netScore = baseReward - currentOrderLostPenalty - currentOrderTimePenalty;

        // 3. Добавляем к общему счету
        currentScore += netScore;

        // 4. Защита от ухода общего счета в минус
        if (currentScore < 0) currentScore = 0;

        UpdateScoreUI();
    }

    private void ProcessOrderCompletion()
    {
        if (currentMode == GameMode.Challenge)
        {
            ordersCompleted++;

            if (ordersCompleted >= maxOrders)
            {
                StartCoroutine(EndGameRoutine());
                return;
            }
        }
        ResetZones();
    }

    private IEnumerator EndGameRoutine()
    {
        isTimerRunning = false;
        if (dropoffZoneObject != null) dropoffZoneObject.SetActive(false);
        if (pickupZoneObject != null) pickupZoneObject.SetActive(false);
        if (dropoffVisual != null) dropoffVisual.SetActive(false);

        yield return new WaitForSeconds(2f);

        Time.timeScale = 0f;

        if (timerText != null) timerText.text = "";
        if (gameOverPanel != null) gameOverPanel.SetActive(true);

        string highScoreKey = (currentMode == GameMode.Challenge) ? "HighScore_Challenge" : "HighScore_Endless";

        int previousHighScore = PlayerPrefs.GetInt(highScoreKey, 0);
        if (currentScore > previousHighScore)
        {
            PlayerPrefs.SetInt(highScoreKey, currentScore);
        }
        PlayerPrefs.SetInt("LastSavedScore", currentScore);
        PlayerPrefs.Save();

        // --- ЛОГИКА МЕДАЛЕЙ И ЦВЕТА ФОНА ---
        string medalName = "";
        string customMessage = "";
        bool showMedalImage = true;
        Color selectedColor = failColor; // Цвет по умолчанию (провал)

        if (currentScore >= scoreForGold)
        {
            medalName = "ЗОЛОТО ";
            customMessage = "Идеальная работа!";
            selectedColor = goldColor;
        }
        else if (currentScore >= scoreForSilver)
        {
            medalName = "СЕРЕБРО ";
            customMessage = "Хорошая работа, но можно лучше.";
            selectedColor = silverColor;
        }
        else if (currentScore >= scoreForBronze)
        {
            medalName = "БРОНЗА ";
            customMessage = "Неплохо, ты справился.";
            selectedColor = bronzeColor;
        }
        else if (currentScore >= scoreForWood)
        {
            medalName = "ДЕРЕВО ";
            customMessage = "Хуже некуда, босс в ярости!!!";
            selectedColor = woodColor;
        }
        else
        {
            medalName = "ПОЛНЫЙ ПРОВАЛ ";
            customMessage = "Ты уволен!!!";
            showMedalImage = false;
            selectedColor = failColor;
        }

        // ⚡ Меняем цвет фона панели
        if (gameOverBGImage != null)
        {
            gameOverBGImage.color = selectedColor;
        }
        else if (gameOverPanel != null)
        {
            // Авто-поиск компонента Image, если забыли привязать в инспекторе
            UnityEngine.UI.Image bg = gameOverPanel.GetComponent<UnityEngine.UI.Image>();
            if (bg != null) bg.color = selectedColor;
        }

        // Выводим результат на экран
        if (resultText != null)
        {
            resultText.text = "СМЕНА ОКОНЧЕНА!\n\nТвой результат: " + medalName + "\n" + customMessage + "\n\nИтоговый счет: " + currentScore;
        }

        if (medalImage != null) medalImage.SetActive(showMedalImage);
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

    public bool HasCargo() { return activeCargoPieces.Count > 0; }

    private void ShowTemporaryMessage(string message, float delay = 2f)
    {
        if (timerText != null)
        {
            timerText.text = message;
            if (clearMessageCoroutine != null) StopCoroutine(clearMessageCoroutine);
            clearMessageCoroutine = StartCoroutine(ClearMessageRoutine(delay));
        }
    }

    private IEnumerator ClearMessageRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (timerText != null && !isTimerRunning && !isLoadingCargo)
        {
            timerText.text = "";
        }
    }
}