using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    [Header("Окна UI")]
    public GameObject pauseMenuPanel;
    public GameObject mainMenuWindow;
    public GameObject settingsWindow;
    public GameObject gameplayHUD;
    public GameObject points;

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

    private const string ABS_KEY = "Setting_ABS";
    private const string TRANSMISSION_KEY = "Setting_Transmission";
    private const string HUD_KEY = "Setting_HUD";
    private const string RESTART_FLAG_KEY = "QuickRestartActive"; // Ключ для обхода меню при рестарте

    void Start()
    {
        if (pauseMenuPanel != null)
        {
            bgImage = pauseMenuPanel.GetComponent<Image>();
        }

        // Был ли это быстрый перезапуск через кнопку "Заново"?
        if (PlayerPrefs.GetInt(RESTART_FLAG_KEY, 0) == 1)
        {
            PlayerPrefs.SetInt(RESTART_FLAG_KEY, 0);
            PlayerPrefs.Save();

            isInMainMenu = false;
            isPaused = false;
            Time.timeScale = 1f;

            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            if (mainMenuWindow != null) mainMenuWindow.SetActive(false);
            if (gameplayHUD != null && (hudToggle == null || hudToggle.isOn)) gameplayHUD.SetActive(true);

            // ⚡ ВКЛЮЧАЕМ элемент при быстром перезапуске (минуя меню)
            if (points != null) points.SetActive(true);
        }
        else
        {
            // Стандартный запуск: открываем Главное меню
            Time.timeScale = 0f;
            isInMainMenu = true;
            isPaused = true;

            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
            if (mainMenuWindow != null) mainMenuWindow.SetActive(true);
            if (gameplayHUD != null) gameplayHUD.SetActive(false);

            // ⚡ ГЛУШИМ элемент в самом главном меню при старте
            if (points != null) points.SetActive(false);

            SetBackgroundAlpha(1f);
        }

        if (settingsWindow != null) settingsWindow.SetActive(false);

        UpdateMenuButtons();
        LoadAndApplySettings();

        if (absToggle != null) absToggle.onValueChanged.AddListener(SetABS);
        if (autoTransmissionToggle != null) autoTransmissionToggle.onValueChanged.AddListener(SetTransmission);
        if (hudToggle != null) hudToggle.onValueChanged.AddListener(SetHUDVisibility);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && !isInMainMenu)
        {
            if (isPaused) ResumeGame();
            else PauseGame();
        }
    }

    public void StartGame()
    {
        isInMainMenu = false;
        isPaused = false;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (gameplayHUD != null && (hudToggle == null || hudToggle.isOn)) gameplayHUD.SetActive(true);

        // ⚡ ВКЛЮЧАЕМ ваш постоянный элемент интерфейса, когда игрок нажимает "Начать игру"
        if (points != null) points.SetActive(true);

        Time.timeScale = 1f;
    }

    public void PauseGame()
    {
        isPaused = true;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
        if (mainMenuWindow != null) mainMenuWindow.SetActive(true);
        if (settingsWindow != null) settingsWindow.SetActive(false);

        SetBackgroundAlpha(pauseAlpha);
        UpdateMenuButtons();

        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        isPaused = false;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);

        // ⚡ УБЕЖДАЕМСЯ, что элемент активен, когда мы снимаем игру с паузы по Escape
        if (points != null) points.SetActive(true);

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

    // ⚡ ОБНОВЛЕННЫЙ МЕТОД ПЕРЕЗАПУСКА УРОВНЯ
    public void RestartLevel()
    {
        // Перед перезагрузкой ставим отметку, что игру нужно запустить СРАЗУ
        PlayerPrefs.SetInt(RESTART_FLAG_KEY, 1);
        PlayerPrefs.Save();

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMainMenu()
    {
        // При явном выходе в главное меню через кнопку — убираем флаг быстрого старта на всякий случай
        PlayerPrefs.SetInt(RESTART_FLAG_KEY, 0);
        PlayerPrefs.Save();

        isInMainMenu = true;
        isPaused = true;
        Time.timeScale = 0f;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
        if (mainMenuWindow != null) mainMenuWindow.SetActive(true);
        if (settingsWindow != null) settingsWindow.SetActive(false);
        if (gameplayHUD != null) gameplayHUD.SetActive(false);

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
            // Режим СТАРТА игры (Главное меню):
            if (startGameButton != null) startGameButton.SetActive(true);
            if (settingsButton != null) settingsButton.SetActive(true);
            if (quitButton != null) quitButton.SetActive(true);

            if (resumeButton != null) resumeButton.SetActive(false);
            if (restartButton != null) restartButton.SetActive(false);
            if (backToMenuButton != null) backToMenuButton.SetActive(false);

            // ⚡ ОТКЛЮЧАЕМ ваш постоянный элемент интерфейса, пока игрок в главном меню
            if (points != null) points.SetActive(false);
        }
        else
        {
            // Режим ИГРОВОЙ ПАУЗЫ (После нажатия Escape или во время рейса):
            if (startGameButton != null) startGameButton.SetActive(false);
            if (quitButton != null) quitButton.SetActive(false);

            if (resumeButton != null) resumeButton.SetActive(true);
            if (restartButton != null) restartButton.SetActive(true);
            if (settingsButton != null) settingsButton.SetActive(true);
            if (backToMenuButton != null) backToMenuButton.SetActive(true);

            // ⚡ ВКЛЮЧАЕМ элемент интерфейса обратно во время самой игры
            // Если вы хотите, чтобы он прятался еще и в момент, когда нажата ПАУЗА (Escape),
            // то вместо 'true' напишите '!isPaused' (он будет активен только при движении машины)
            if (points != null) points.SetActive(true);
        }
    }


    private void LoadAndApplySettings()
    {
        bool absValue = PlayerPrefs.GetInt(ABS_KEY, 1) == 1;
        if (carController != null) carController.useABS = absValue;
        if (absToggle != null) absToggle.isOn = absValue;

        bool transValue = PlayerPrefs.GetInt(TRANSMISSION_KEY, 1) == 1;
        if (carController != null) carController.isAutomatic = transValue;
        if (autoTransmissionToggle != null) autoTransmissionToggle.isOn = transValue;

        bool hudValue = PlayerPrefs.GetInt(HUD_KEY, 1) == 1;
        if (gameplayHUD != null) gameplayHUD.SetActive(hudValue);
        if (hudToggle != null) hudToggle.isOn = hudValue;
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
