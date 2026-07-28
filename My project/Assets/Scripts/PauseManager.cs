using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
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

    void Start()
    {
        if (pauseMenuPanel != null) bgImage = pauseMenuPanel.GetComponent<Image>();

        // Был ли это быстрый перезапуск через кнопку "Заново"?
        if (PlayerPrefs.GetInt(RESTART_FLAG_KEY, 0) == 1)
        {
            PlayerPrefs.SetInt(RESTART_FLAG_KEY, 0);
            PlayerPrefs.Save();

            isInMainMenu = false;
            isPaused = false;
            isGameOver = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;

            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            if (mainMenuWindow != null) mainMenuWindow.SetActive(false);
            if (gameplayHUD != null && (hudToggle == null || hudToggle.isOn)) gameplayHUD.SetActive(true);

            // Включаем геймплейный UI при быстром рестарте
            if (points != null) points.SetActive(true);
            if (gameTimer != null) gameTimer.SetActive(true);
        }
        else
        {
            // Обычный старт: открываем Главное меню
            Time.timeScale = 0f;
            isInMainMenu = true;
            isPaused = true;
            AudioListener.pause = true;

            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
            if (mainMenuWindow != null) mainMenuWindow.SetActive(true);
            if (gameplayHUD != null) gameplayHUD.SetActive(false);

            // Прячем геймплейный UI в главном меню
            if (points != null) points.SetActive(false);
            if (gameTimer != null) gameTimer.SetActive(false);

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

    // МЕТОД ВЫЗОВА КОНЦА ИГРЫ (Вызывается из скрипта топлива)
    public void TriggerGameOver(int finalScores)
    {
        isGameOver = true;
        isPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
        if (mainMenuWindow != null) mainMenuWindow.SetActive(true);
        if (settingsWindow != null) settingsWindow.SetActive(false);
        if (gameplayHUD != null) gameplayHUD.SetActive(false);

        // ⚡ ИСЧЕЗНОВЕНИЕ ИНТЕРФЕЙСА: Скрываем только очки и время (сообщение об утере не трогаем)
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

    public void StartGame()
    {
        isInMainMenu = false;
        isPaused = false;
        isGameOver = false;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (gameplayHUD != null && (hudToggle == null || hudToggle.isOn)) gameplayHUD.SetActive(true);

        // Включаем геймплейный интерфейс обратно при старте
        if (points != null) points.SetActive(true);
        if (gameTimer != null) gameTimer.SetActive(true);

        if (gameOverText != null) gameOverText.gameObject.SetActive(false);

        AudioListener.pause = false;
        Time.timeScale = 1f;
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

    public void RestartLevel()
    {
        PlayerPrefs.SetInt(RESTART_FLAG_KEY, 1);
        PlayerPrefs.Save();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMainMenu()
    {
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

        SetBackgroundAlpha(1f);
        UpdateMenuButtons();
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
        }
        else
        {
            if (startGameButton != null) startGameButton.SetActive(false);
            if (quitButton != null) quitButton.SetActive(false);

            if (settingsButton != null) settingsButton.SetActive(true);
            if (backToMenuButton != null) backToMenuButton.SetActive(true);

            // ⚡ ИСПРАВЛЕННАЯ ЛОГИКА ДЛЯ ОЧКОВ И ВРЕМЕНИ:
            // Если это конец игры, они должны быть ВЫКЛЮЧЕНЫ. 
            // Если это обычная пауза на Escape — они могут оставаться ВКЛЮЧЕННЫМИ.
            if (isGameOver)
            {
                if (points != null) points.SetActive(false);
                if (gameTimer != null) gameTimer.SetActive(false);

                if (resumeButton != null) resumeButton.SetActive(false);
                if (restartButton != null) restartButton.SetActive(true);
            }
            else
            {
                // Обычная пауза посреди рейса
                if (points != null) points.SetActive(true);
                if (gameTimer != null) gameTimer.SetActive(true);

                if (resumeButton != null) resumeButton.SetActive(true);
                if (restartButton != null) restartButton.SetActive(true);
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