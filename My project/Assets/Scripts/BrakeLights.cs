using UnityEngine;

public class CarBrakeLights : MonoBehaviour
{
    [Header("Ссылки на источники света")]
    public Light[] brakeLights;

    [Header("Настройки моделей фар")]
    public MeshRenderer[] carMeshRenderers;   // ТЕПЕРЬ ЭТО МАССИВ (СПИСОК) ФАР!
    public int materialIndex = 0;

    [Header("Интенсивность свечения (Emission)")]
    public float idleIntensity = 0.5f;
    public float brakeIntensity = 3.0f;

    private Material[] brakeMaterials;        // Список для хранения копий материалов обеих фар
    private Color baseEmissionColor = Color.red;

    void Start()
    {
        // Создаем массив под материалы в соответствии с количеством перетащенных фар
        if (carMeshRenderers != null && carMeshRenderers.Length > 0)
        {
            brakeMaterials = new Material[carMeshRenderers.Length];

            for (int i = 0; i < carMeshRenderers.Length; i++)
            {
                if (carMeshRenderers[i] != null && carMeshRenderers[i].materials.Length > materialIndex)
                {
                    // Получаем уникальный экземпляр материала для каждой фары
                    brakeMaterials[i] = carMeshRenderers[i].materials[materialIndex];
                    brakeMaterials[i].EnableKeyword("_EMISSION");
                }
            }
        }

        SetBrakeLightsState(false);
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.Space))
        {
            SetBrakeLightsState(true);
        }
        else
        {
            SetBrakeLightsState(false);
        }
    }

    void SetBrakeLightsState(bool isBraking)
    {
        if (brakeLights != null)
        {
            foreach (Light light in brakeLights)
            {
                if (light != null) light.enabled = isBraking;
            }
        }

        // ПЕРЕБИРАЕМ ВСЕ ФАРЫ И МЕНЯЕМ ИМ ЯРКОСТЬ СВЕЧЕНИЯ
        if (brakeMaterials != null)
        {
            float currentIntensity = isBraking ? brakeIntensity : idleIntensity;
            Color finalEmissionColor = baseEmissionColor * currentIntensity;

            foreach (Material mat in brakeMaterials)
            {
                if (mat != null)
                {
                    mat.SetColor("_EmissionColor", finalEmissionColor);
                }
            }
        }
    }
}
