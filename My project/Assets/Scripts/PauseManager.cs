using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

public class PauseManager : MonoBehaviour
{
    [Header("Окна UI")]
    public GameObject pauseMenuPanel;
    public GameObject mainMenuWindow;
    public GameObject settingsWindow;
    public GameObject gameplayHUD;
    public GameObject points;             // Элемент интерфейса очков
    public GameObject gameTimer;          // Элемент интерфейса со временем (часы/таймер)

    [Header("Конец игры (Топливо)")]
    public TMP_Text gameOverText;
    private int currentScores = 0;

    [Header("Настройки сохранения и рекордов")]
    public TMP_Text scorePopupText;       // Плашка рекорда (вспыхивает на 3 секунды при старте)
    public TMP_Text highScoreText;        // Текст "Лучший результат: Х" в Главном Меню
    private const string HIGH_SCORE_KEY = "PlayerHighScore";

    [Header("Настройки прозрачности фона")]
    [Range(0f, 1f)] public float pauseAlpha = 0.5f;
    private Image bgImage;

    [Header("Кнопки Главного Меню / Паузы")]
    public GameObject startGameButton;
    public GameObject settingsButton;
    public GameObject resumeButton;
    public GameObject restartButton;
    public GameObject quitButton;
    public GameObject backToMenuButton;

    [Header("Ссылки на компоненты машины")]
    public PickupController carController;

    [Header("Элементы настроек (Toggles)")]
    public Toggle absToggle;
    public Toggle autoTransmissionToggle;
    public Toggle hudToggle;

    private bool isPaused = false;
    private bool isInMainMenu = true;
    private bool isGameOver = false;

    private const string ABS_KEY = "Setting_ABS";
    private const string TRANSMISSION_KEY = "Setting_Transmission";
    private const string HUD_KEY = "Setting_HUD";
    private const string RESTART_FLAG_KEY = "QuickRestartActive";
    private const string SAVED_SCORE_KEY = "LastSavedScore";
    private const string SHOW_POPUP_KEY = "ShowPopupNextScene";

    void Start()
    {
        if (pauseMenuPanel != null) bgImage = pauseMenuPanel.GetComponent<Image>();

        // Обновляем текст рекорда в Главном Меню сразу при старте сцены
        UpdateHighScoreUI();

        // Проверяем, был ли это быстрый перезапуск через кнопку "Заново"?
        bool isQuickRestart = PlayerPrefs.GetInt(RESTART_FLAG_KEY, 0) == 1;

        // ⚡ ИСПРАВЛЕННАЯ ЛОГИКА ОТОБРАЖЕНИЯ ПРИ СТАРТЕ СЦЕНЫ:
        if (isQuickRestart || PlayerPrefs.GetInt(SHOW_POPUP_KEY, 0) == 1)
        {
            // Сбрасываем флаги
            PlayerPrefs.SetInt(SHOW_POPUP_KEY, 0);
            PlayerPrefs.SetInt(RESTART_FLAG_KEY, 0);
            PlayerPrefs.Save();

            // Запускаем ТОЛЬКО рекорд на 3 секунды посреди экрана, игнорируя старый текст очков
            int recordToShow = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
            StartCoroutine(ShowScorePopupRoutine(recordToShow));

            // Настраиваем состояние геймплея (минуя главное меню)
            isInMainMenu = false;
            isPaused = false;
            isGameOver = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;

            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            if (mainMenuWindow != null) mainMenuWindow.SetActive(false);
            if (gameplayHUD != null && (hudToggle == null || hudToggle.isOn)) gameplayHUD.SetActive(true);

            if (points != null) points.SetActive(true);
            if (gameTimer != null) gameTimer.SetActive(true);
        }
        else
        {
            // Стандартный запуск в Главное меню (Не рестарт)
            Time.timeScale = 0f;
            isInMainMenu = true;
            isPaused = true;
            AudioListener.pause = true;

            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
            if (mainMenuWindow != null) mainMenuWindow.SetActive(true);
            if (gameplayHUD != null) gameplayHUD.SetActive(false);

            if (points != null) points.SetActive(false);
            if (gameTimer != null) gameTimer.SetActive(false);

            // В главном меню плашка очков горит постоянно, показывая прошлый заезд
            if (scorePopupText != null)
            {
                int lastScore = PlayerPrefs.GetInt(SAVED_SCORE_KEY, 0);
                scorePopupText.gameObject.SetActive(true);
                scorePopupText.text = "Набрано очков: " + lastScore;
            }

            SetBackgroundAlpha(1f);
        }

        if (settingsWindow != null) settingsWindow.SetActive(false);
        if (gameOverText != null) gameOverText.gameObject.SetActive(false);

        UpdateMenuButtons();
        LoadAndApplySettings();

        if (absToggle != null) absToggle.onValueChanged.AddListener(SetABS);
        if (autoTransmissionToggle != null) autoTransmissionToggle.onValueChanged.AddListener(SetTransmission);
        if (hudToggle != null) hudToggle.onValueChanged.AddListener(SetHUDVisibility);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && !isInMainMenu && !isGameOver)
        {
            if (isPaused) ResumeGame();
            else PauseGame();
        }
    }

    public void TriggerGameOver(int finalScores)
    {
        isGameOver = true;
        isPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        CheckAndSaveHighScore(finalScores);
        PlayerPrefs.SetInt(SAVED_SCORE_KEY, finalScores);
        PlayerPrefs.Save();

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
        if (mainMenuWindow != null) mainMenuWindow.SetActive(true);
        if (settingsWindow != null) settingsWindow.SetActive(false);
        if (gameplayHUD != null) gameplayHUD.SetActive(false);

        if (points != null) points.SetActive(false);
        if (gameTimer != null) gameTimer.SetActive(false);

        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(true);
            gameOverText.text = "Топливо закончилось!\nНабранные очки: " + finalScores;
        }

        SetBackgroundAlpha(pauseAlpha);
        UpdateMenuButtons();
    }

    public void RestartLevel()
    {
        CheckAndSaveHighScore(currentScores);

        PlayerPrefs.SetInt(SAVED_SCORE_KEY, currentScores);

        // Жестко фиксируем флаги для активации корутины в следующей сцене
        PlayerPrefs.SetInt(SHOW_POPUP_KEY, 1);
        PlayerPrefs.SetInt(RESTART_FLAG_KEY, 1);
        PlayerPrefs.Save();

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMainMenu()
    {
        CheckAndSaveHighScore(currentScores);

        PlayerPrefs.SetInt(SAVED_SCORE_KEY, currentScores);
        PlayerPrefs.SetInt(SHOW_POPUP_KEY, 0);
        PlayerPrefs.SetInt(RESTART_FLAG_KEY, 0);
        PlayerPrefs.Save();

        isInMainMenu = true;
        isPaused = true;
        isGameOver = false;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
        if (mainMenuWindow != null) mainMenuWindow.SetActive(true);
        if (settingsWindow != null) settingsWindow.SetActive(false);
        if (gameplayHUD != null) gameplayHUD.SetActive(false);
        if (gameOverText != null) gameOverText.gameObject.SetActive(false);

        if (points != null) points.SetActive(false);
        if (gameTimer != null) gameTimer.SetActive(false);

        UpdateHighScoreUI();
        if (scorePopupText != null)
        {
            scorePopupText.gameObject.SetActive(true);
            scorePopupText.text = "Набрано очков: " + currentScores;
        }

        SetBackgroundAlpha(1f);
        UpdateMenuButtons();
    }

    // Когда нажимаем "Начать игру" из Главного меню — тоже запускаем рекорд на 3 секунды
    public void StartGame()
    {
        isInMainMenu = false;
        isPaused = false;
        isGameOver = false;

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (gameplayHUD != null && (hudToggle == null || hudToggle.isOn)) gameplayHUD.SetActive(true);

        if (points != null) points.SetActive(true);
        if (gameTimer != null) gameTimer.SetActive(true);
        if (gameOverText != null) gameOverText.gameObject.SetActive(false);

        // Меняем текст плашки на Лучший Результат и запускаем 3 секунды для старта из меню
        int recordToShow = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
        StartCoroutine(ShowScorePopupRoutine(recordToShow));

        AudioListener.pause = false;
        Time.timeScale = 1f;
    }

    private void CheckAndSaveHighScore(int scoreToCheck)
    {
        int previousHighScore = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
        if (scoreToCheck > previousHighScore)
        {
            PlayerPrefs.SetInt(HIGH_SCORE_KEY, scoreToCheck);
            PlayerPrefs.Save();
        }
    }

    private void UpdateHighScoreUI()
    {
        if (highScoreText != null)
        {
            int currentHighScore = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
            highScoreText.text = "Лучший результат: " + currentHighScore;
        }
    }

    // Корутина трехсекундного отображения рекорда на экране
    IEnumerator ShowScorePopupRoutine(int scoreValue)
    {
        if (scorePopupText != null)
        {
            scorePopupText.gameObject.SetActive(true);
            scorePopupText.text = "Лучший результат: " + scoreValue;

            yield return new WaitForSecondsRealtime(3f);

            scorePopupText.gameObject.SetActive(false);
        }
    }
    public void PauseGame()
    {
        isPaused = true;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
        if (mainMenuWindow != null) mainMenuWindow.SetActive(true);
        if (settingsWindow != null) settingsWindow.SetActive(false);
        if (gameOverText != null) gameOverText.gameObject.SetActive(false);

        SetBackgroundAlpha(pauseAlpha);
        UpdateMenuButtons();

        AudioListener.pause = true;
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        isPaused = false;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);

        if (points != null) points.SetActive(true);
        if (gameTimer != null) gameTimer.SetActive(true);

        AudioListener.pause = false;
        Time.timeScale = 1f;
    }

    public void OpenSettings()
    {
        if (mainMenuWindow != null) mainMenuWindow.SetActive(false);
        if (settingsWindow != null) settingsWindow.SetActive(true);
    }

    public void CloseSettings()
    {
        if (mainMenuWindow != null) mainMenuWindow.SetActive(true);
        if (settingsWindow != null) settingsWindow.SetActive(false);
    }

    public void QuitGame()
    {
        Debug.Log("Выход из игры...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetBackgroundAlpha(float alphaValue)
    {
        if (bgImage != null)
        {
            Color currentColor = bgImage.color;
            currentColor.a = alphaValue;
            bgImage.color = currentColor;
        }
    }

    private void UpdateMenuButtons()
    {
        if (isInMainMenu)
        {
            if (startGameButton != null) startGameButton.SetActive(true);
            if (settingsButton != null) settingsButton.SetActive(true);
            if (quitButton != null) quitButton.SetActive(true);

            if (resumeButton != null) resumeButton.SetActive(false);
            if (restartButton != null) restartButton.SetActive(false);
            if (backToMenuButton != null) backToMenuButton.SetActive(false);

            if (points != null) points.SetActive(false);
            if (gameTimer != null) gameTimer.SetActive(false);

            if (highScoreText != null) highScoreText.gameObject.SetActive(true);
            if (scorePopupText != null) scorePopupText.gameObject.SetActive(true);
        }
        else
        {
            if (startGameButton != null) startGameButton.SetActive(false);
            if (quitButton != null) quitButton.SetActive(false);

            if (settingsButton != null) settingsButton.SetActive(true);
            if (backToMenuButton != null) backToMenuButton.SetActive(true);

            if (highScoreText != null) highScoreText.gameObject.SetActive(false);

            if (isGameOver)
            {
                if (points != null) points.SetActive(false);
                if (gameTimer != null) gameTimer.SetActive(false);

                if (resumeButton != null) resumeButton.SetActive(false);
                if (restartButton != null) restartButton.SetActive(true);

                if (scorePopupText != null) scorePopupText.gameObject.SetActive(false);
            }
            else
            {
                // Обычная пауза посреди рейса
                if (points != null) points.SetActive(true);
                if (gameTimer != null) gameTimer.SetActive(true);
                if (resumeButton != null) resumeButton.SetActive(true);
                if (restartButton != null) restartButton.SetActive(true);

                if (scorePopupText != null && PlayerPrefs.GetInt(SHOW_POPUP_KEY, 0) == 0 && !scorePopupText.gameObject.activeSelf)
                    scorePopupText.gameObject.SetActive(false);
            }
        }

    }

    public void AddScores(int amount)
    {
        currentScores += amount;
    }

    public int GetCurrentScores()
    {
        return currentScores;
    }

    private void LoadAndApplySettings()
    {
        bool absValue = PlayerPrefs.GetInt(ABS_KEY, 1) == 1;
        if (carController != null) carController.useABS = absValue;
        if (absToggle != null) absToggle.onValueChanged.Invoke(absValue);

        bool transValue = PlayerPrefs.GetInt(TRANSMISSION_KEY, 1) == 1;
        if (carController != null) carController.isAutomatic = transValue;
        if (autoTransmissionToggle != null) autoTransmissionToggle.onValueChanged.Invoke(transValue);

        bool hudValue = PlayerPrefs.GetInt(HUD_KEY, 1) == 1;
        if (gameplayHUD != null) gameplayHUD.SetActive(hudValue);
        if (hudToggle != null) hudToggle.onValueChanged.Invoke(hudValue);
    }

    private void SetABS(bool value)
    {
        if (carController != null) carController.useABS = value;
        PlayerPrefs.SetInt(ABS_KEY, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void SetTransmission(bool value)
    {
        if (carController != null) carController.isAutomatic = value;
        PlayerPrefs.SetInt(TRANSMISSION_KEY, value ? 1 : 0);
        PlayerPrefs.Save();
    }
    private void SetHUDVisibility(bool value)
    {
        if (gameplayHUD != null) gameplayHUD.SetActive(value);
        PlayerPrefs.SetInt(HUD_KEY, value ? 1 : 0);
        PlayerPrefs.Save();
    }
}