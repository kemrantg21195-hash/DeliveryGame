using System.Collections;
using UnityEngine;

public class DynamicWeatherSystem : MonoBehaviour
{
    [Header("Ссылки на компоненты")]
    public ParticleSystem rainParticleSystem;
    public Light directionalLight;
    public AudioSource rainAudioSource;

    [Header("Настройки неба (Материал Day)")]
    public Material daySkyboxMaterial;
    private string skyColorPropertyName = "_SkyTint";
    private string groundColorPropertyName = "_GroundColor";

    [Header("Ссылки на колеса машины")]
    public WheelCollider[] carWheels;         // Перетащите сюда все 4 колеса вашей машины

    [Header("Настройки времени")]
    public float minTimeBetweenWeatherChange = 30f;
    public float maxTimeBetweenWeatherChange = 90f;
    public float weatherTransitionSpeed = 5f;

    [Header("Настройки Ясной Погоды")]
    public float clearLightIntensity = 1f;
    public float clearFogDensity = 0.005f;
    public Color clearSkyColor = new Color(0.3f, 0.5f, 0.7f);
    public Color clearGroundColor = new Color(0.6f, 0.6f, 0.6f);
    [Range(0.1f, 1f)] public float clearFrictionMultiplier = 1f; // 100% сцепления в сухую погоду

    [Header("Настройки Дождливой Погоды")]
    public float rainLightIntensity = 0.2f;
    public float rainFogDensity = 0.03f;
    [Range(0f, 1f)] public float maxRainVolume = 0.5f;
    public Color rainSkyColor = new Color(0.15f, 0.15f, 0.18f);
    public Color rainGroundColor = new Color(0.25f, 0.25f, 0.25f);
    [Range(0.1f, 1f)] public float rainFrictionMultiplier = 0.5f; // 50% сцепления (скользкая дорога)

    private bool isRainy = false;
    private float targetLightIntensity;
    private float targetFogDensity;
    private float targetVolume;
    private float targetFrictionMultiplier; // Целевой множитель трения
    private float currentFrictionMultiplier = 1f; // Текущий множитель трения

    private Color currentSkyColor;
    private Color currentGroundColor;
    private Color targetSkyColor;
    private Color targetGroundColor;

    // Структуры для хранения исходных настроек трения каждого колеса
    private WheelFrictionCurve[] initialForwardFriction;
    private WheelFrictionCurve[] initialSidewaysFriction;

    void Start()
    {
        RenderSettings.fog = true;

        if (daySkyboxMaterial == null) daySkyboxMaterial = RenderSettings.skybox;

        if (daySkyboxMaterial != null)
        {
            currentSkyColor = clearSkyColor;
            currentGroundColor = clearGroundColor;
            daySkyboxMaterial.SetColor(skyColorPropertyName, currentSkyColor);
            daySkyboxMaterial.SetColor(groundColorPropertyName, currentGroundColor);
        }

        // Сохраняем исходные настройки физики колес, чтобы не сломать их
        SaveInitialWheelFriction();

        RenderSettings.fogDensity = clearFogDensity;
        if (directionalLight != null) directionalLight.intensity = clearLightIntensity;

        SetClearWeather();

        if (rainAudioSource != null)
        {
            rainAudioSource.loop = true;
            if (!rainAudioSource.isPlaying) rainAudioSource.Play();
            rainAudioSource.volume = 0f;
        }

        StartCoroutine(WeatherStateRoutine());
    }

    void Update()
    {
        float delta = Time.deltaTime * weatherTransitionSpeed;

        // Лерпим интенсивность света, туман и звук
        if (directionalLight != null) directionalLight.intensity = Mathf.Lerp(directionalLight.intensity, targetLightIntensity, delta);
        RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, targetFogDensity, delta);
        if (rainAudioSource != null) rainAudioSource.volume = Mathf.Lerp(rainAudioSource.volume, targetVolume, delta);

        // Лерпим цвета неба
        if (daySkyboxMaterial != null)
        {
            currentSkyColor = Color.Lerp(currentSkyColor, targetSkyColor, delta);
            currentGroundColor = Color.Lerp(currentGroundColor, targetGroundColor, delta);
            daySkyboxMaterial.SetColor(skyColorPropertyName, currentSkyColor);
            daySkyboxMaterial.SetColor(groundColorPropertyName, currentGroundColor);
        }

        // ПЛАВНО ИЗМЕНЯЕМ СЦЕПЛЕНИЕ КОЛЕС С ДОРОГОЙ
        currentFrictionMultiplier = Mathf.Lerp(currentFrictionMultiplier, targetFrictionMultiplier, delta);
        ApplyFrictionToWheels(currentFrictionMultiplier);
    }

    void SaveInitialWheelFriction()
    {
        if (carWheels == null || carWheels.Length == 0) return;

        initialForwardFriction = new WheelFrictionCurve[carWheels.Length];
        initialSidewaysFriction = new WheelFrictionCurve[carWheels.Length];

        for (int i = 0; i < carWheels.Length; i++)
        {
            if (carWheels[i] != null)
            {
                initialForwardFriction[i] = carWheels[i].forwardFriction;
                initialSidewaysFriction[i] = carWheels[i].sidewaysFriction;
            }
        }
    }

    void ApplyFrictionToWheels(float multiplier)
    {
        if (carWheels == null || carWheels.Length == 0) return;

        for (int i = 0; i < carWheels.Length; i++)
        {
            if (carWheels[i] == null) continue;

            // Изменяем продольное трение (ускорение / торможение)
            WheelFrictionCurve fFriction = initialForwardFriction[i];
            fFriction.extremumValue *= multiplier;
            fFriction.asymptoteValue *= multiplier;
            carWheels[i].forwardFriction = fFriction;

            // Изменяем боковое трение (занос / дрифт)
            WheelFrictionCurve sFriction = initialSidewaysFriction[i];
            sFriction.extremumValue *= multiplier;
            sFriction.asymptoteValue *= multiplier;
            carWheels[i].sidewaysFriction = sFriction;
        }
    }

    IEnumerator WeatherStateRoutine()
    {
        while (true)
        {
            float waitTime = Random.Range(minTimeBetweenWeatherChange, maxTimeBetweenWeatherChange);
            yield return new WaitForSeconds(waitTime);

            isRainy = !isRainy;

            if (isRainy) SetRainyWeather();
            else SetClearWeather();
        }
    }

    void SetClearWeather()
    {
        targetLightIntensity = clearLightIntensity;
        targetFogDensity = clearFogDensity;
        targetVolume = 0f;
        targetSkyColor = clearSkyColor;
        targetGroundColor = clearGroundColor;

        targetFrictionMultiplier = clearFrictionMultiplier; // Возвращаем 100% зацепа

        if (rainParticleSystem != null) { var emission = rainParticleSystem.emission; emission.enabled = false; }
        Debug.Log("Погода меняется на: ЯСНО. Сцепление восстановлено.");
    }

    void SetRainyWeather()
    {
        targetLightIntensity = rainLightIntensity;
        targetFogDensity = rainFogDensity;
        targetVolume = maxRainVolume;
        targetSkyColor = rainSkyColor;
        targetGroundColor = rainGroundColor;

        targetFrictionMultiplier = rainFrictionMultiplier; // Снижаем зацеп до скользкого

        if (rainParticleSystem != null) { var emission = rainParticleSystem.emission; emission.enabled = true; if (!rainParticleSystem.isPlaying) rainParticleSystem.Play(); }
        Debug.Log("Погода меняется на: ДОЖДЬ. Дорога становится скользкой!");
    }

    void OnApplicationQuit()
    {
        if (daySkyboxMaterial != null)
        {
            daySkyboxMaterial.SetColor(skyColorPropertyName, clearSkyColor);
            daySkyboxMaterial.SetColor(groundColorPropertyName, clearGroundColor);
        }
        // Возвращаем колесам исходную физику при выходе
        ApplyFrictionToWheels(1f);
    }
}

