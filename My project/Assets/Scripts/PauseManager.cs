using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro; // ОБЯЗАТЕЛЬНО для работы с TextMeshPro. Если у вас обычный Text, замените TMP_Text на Text.

public class PauseManager : MonoBehaviour
{
    [Header("Окна UI")]
    public GameObject pauseMenuPanel;
    public GameObject mainMenuWindow;
    public GameObject settingsWindow;
    public GameObject gameplayHUD;
    public GameObject points;             // Элемент интерфейса очков
    public GameObject gameTimer;          // Элемент интерфейса со временем (часы/таймер)
    public GameObject warningText;        // ⚡ НОВАЯ ССЫЛКА: Перетащите сюда ваш Warning Text из Canvas

    [Header("Настройки фоновых изображений")]
    public Sprite[] mainBackgroundSprites;

    [Header("Музыкальная система")]
    public AudioSource musicAudioSource;
    public AudioClip menuMusicClip;
    public AudioClip[] gameplayMusicClips;
    public Slider musicVolumeSlider;
    private int currentGameplayTrackIndex = 0;
    private float currentVolumeValue = 1f;

    [Header("Конец игры (Топливо)")]
    public TMP_Text gameOverText;
    private int currentScores = 0;

    [Header("Настройки сохранения и рекордов")]
    public TMP_Text scorePopupText;
    public TMP_Text highScoreText;
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

    // Технический флаг, чтобы скрипт помнил: горело ли предупреждение до нажатия Esc
    private bool wasWarningActiveBeforePause = false;

    private const string ABS_KEY = "Setting_ABS";
    private const string TRANSMISSION_KEY = "Setting_Transmission";
    private const string HUD_KEY = "Setting_HUD";
    private const string MUSIC_VOLUME_KEY = "Setting_MusicVolume";
    private const string RESTART_FLAG_KEY = "QuickRestartActive";
    private const string SAVED_SCORE_KEY = "LastSavedScore";
    private const string SHOW_POPUP_KEY = "ShowPopupNextScene";

    void Start()
    {
        if (pauseMenuPanel != null) bgImage = pauseMenuPanel.GetComponent<Image>();

        // Логика случайного фона
        if (bgImage != null && mainBackgroundSprites != null && mainBackgroundSprites.Length > 0)
        {
            int randomIndex = Random.Range(0, mainBackgroundSprites.Length);
            bgImage.sprite = mainBackgroundSprites[randomIndex];
        }

        UpdateHighScoreUI();

        // Был ли это быстрый перезапуск через кнопку "Заново"?
        bool isQuickRestart = PlayerPrefs.GetInt(RESTART_FLAG_KEY, 0) == 1;

        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);

        LoadAndApplySettings();

        if (isQuickRestart || PlayerPrefs.GetInt(SHOW_POPUP_KEY, 0) == 1)
        {
            PlayerPrefs.SetInt(SHOW_POPUP_KEY, 0);
            PlayerPrefs.SetInt(RESTART_FLAG_KEY, 0);
            PlayerPrefs.Save();

            int recordToShow = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
            StartCoroutine(ShowScorePopupRoutine(recordToShow));

            isInMainMenu = false;
            isPaused = false;
            isGameOver = false;
            Time.timeScale = 1f;

            AudioListener.pause = false;
            PlayNextGameplayTrack();

            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            if (mainMenuWindow != null) mainMenuWindow.SetActive(false);
            if (gameplayHUD != null && (hudToggle == null || hudToggle.isOn)) gameplayHUD.SetActive(true);

            if (points != null) points.SetActive(true);
            if (gameTimer != null) gameTimer.SetActive(true);

            // На чистом быстром рестарте предупреждений изначально быть не должно
            if (warningText != null) warningText.SetActive(false);
            wasWarningActiveBeforePause = false;
        }
        else
        {
            // Стандартный запуск в Главное меню
            Time.timeScale = 0f;
            isInMainMenu = true;
            isPaused = true;

            AudioListener.pause = true;
            PlayMenuMusic();

            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
            if (mainMenuWindow != null) mainMenuWindow.SetActive(true);
            if (gameplayHUD != null) gameplayHUD.SetActive(false);

            if (points != null) points.SetActive(false);
            if (gameTimer != null) gameTimer.SetActive(false);

            // В самом главном меню при старте полностью отключаем текст варнинга
            if (warningText != null) warningText.SetActive(false);
            wasWarningActiveBeforePause = false;

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

        if (!isInMainMenu && musicAudioSource != null && !musicAudioSource.isPlaying)
        {
            PlayNextGameplayTrack();
        }
    }

    private void PlayMenuMusic()
    {
        if (musicAudioSource == null || menuMusicClip == null) return;
        musicAudioSource.ignoreListenerPause = true;
        musicAudioSource.clip = menuMusicClip;
        musicAudioSource.volume = currentVolumeValue;
        musicAudioSource.loop = true;
        musicAudioSource.Play();
    }

    private void PlayNextGameplayTrack()
    {
        if (musicAudioSource == null || gameplayMusicClips == null || gameplayMusicClips.Length == 0) return;
        musicAudioSource.ignoreListenerPause = true;
        musicAudioSource.loop = false;
        musicAudioSource.clip = gameplayMusicClips[currentGameplayTrackIndex];
        musicAudioSource.volume = currentVolumeValue;
        musicAudioSource.Play();

        currentGameplayTrackIndex++;
        if (currentGameplayTrackIndex >= gameplayMusicClips.Length) currentGameplayTrackIndex = 0;
    }

    private void SetMusicVolume(float volume)
    {
        currentVolumeValue = volume;
        if (musicAudioSource != null) musicAudioSource.volume = currentVolumeValue;
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, currentVolumeValue);
        PlayerPrefs.Save();
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

        // ⚡ КОНЕЦ ИГРЫ: принудительно гасим варнинг-текст, так как игра завершена
        if (warningText != null) warningText.SetActive(false);

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
        CargoManager cargoManager = Object.FindFirstObjectByType<CargoManager>();
        if (cargoManager != null) currentScores = cargoManager.currentScore;

        CheckAndSaveHighScore(currentScores);
        PlayerPrefs.SetInt(SAVED_SCORE_KEY, currentScores);

        PlayerPrefs.SetInt(SHOW_POPUP_KEY, 1);
        PlayerPrefs.SetInt(RESTART_FLAG_KEY, 1);
        PlayerPrefs.Save();

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ⚡ ГАРАНТИРОВАННЫЙ ВЫХОД В МЕНЮ С ИМПОРТОМ ОЧКОВ ИЗ CARGO_MANAGER
    public void GoToMainMenu()
    {
        // Пытаемся найти ваш менеджер грузов на сцене, где считаются реальные очки
        CargoManager cargoManager = Object.FindFirstObjectByType<CargoManager>();
        if (cargoManager != null)
        {
            // Перезаписываем приватные очки паузы реальными очками из вашей игры перед сохранением
            currentScores = cargoManager.currentScore;
        }
        PlayerPrefs.SetInt(SAVED_SCORE_KEY, currentScores);

        // 1. Сохраняем и проверяем рекорды (теперь тут точно будут реальные очки!)
        CheckAndSaveHighScore(currentScores);
        PlayerPrefs.SetInt(SAVED_SCORE_KEY, currentScores);

        // 2. Сбрасываем флаги
        PlayerPrefs.SetInt(SHOW_POPUP_KEY, 0);
        PlayerPrefs.SetInt(RESTART_FLAG_KEY, 0);
        PlayerPrefs.Save();

        // 3. ПЕРЕЗАГРУЖАЕМ СЦЕНУ
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

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

        if (musicAudioSource != null) musicAudioSource.Stop();
        PlayNextGameplayTrack();

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

        if (warningText != null)
        {
            wasWarningActiveBeforePause = warningText.activeSelf;
            if (wasWarningActiveBeforePause) warningText.SetActive(false);
        }

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

        if (warningText != null && wasWarningActiveBeforePause) warningText.SetActive(true);

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

        currentVolumeValue = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, 1f);
        if (musicAudioSource != null) musicAudioSource.volume = currentVolumeValue;
        if (musicVolumeSlider != null) musicVolumeSlider.value = currentVolumeValue;
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