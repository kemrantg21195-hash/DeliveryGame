using UnityEngine;
using UnityEngine.AI;

public class ArrowNavigation : MonoBehaviour
{
    [Header("Ссылки на визуал")]
    public GameObject arrowVisualContainer; // Сама 3D-стрелка
    public GameObject warningTextObject;    // UI Текст "Вы съехали с дороги!"

    [Header("Настройки")]
    [Tooltip("Скорость поворота стрелки")]
    public float rotationSpeed = 8f;
    [Tooltip("На сколько метров вперед смотрит стрелка")]
    public float minLookAheadDistance = 3f;
    [Tooltip("Допустимое расстояние от дороги (в метрах)")]
    public float offRoadTolerance = 5f;

    private CargoManager cargoManager;
    private NavMeshPath path;
    private Transform currentTarget;
    private bool isOffRoad = false;

    private int roadAreaMask;
    private int roadAreaIndex;

    void Start()
    {
        cargoManager = GetComponentInParent<CargoManager>();
        path = new NavMeshPath();

        // Получаем слой "Road", если он создан в Navigation
        roadAreaIndex = NavMesh.GetAreaFromName("Road");
        if (roadAreaIndex != -1)
        {
            roadAreaMask = 1 << roadAreaIndex;
        }
        else
        {
            roadAreaMask = NavMesh.AllAreas; // Если слоя Road нет, используем всю сетку
        }

        if (warningTextObject != null) warningTextObject.SetActive(false);
    }

    void Update()
    {
        if (cargoManager == null) return;

        // 1. Проверяем, на дороге ли машина
        CheckRoadStatus();

        if (isOffRoad) return;

        // 2. Ищем актуальную цель (Pickup или Dropoff)
        UpdateTarget();

        // 3. Строим маршрут и направляем стрелку
        PointArrow();
    }

    private void CheckRoadStatus()
    {
        Vector3 groundPosition = transform.position;
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 10f))
        {
            groundPosition = hit.point;
        }

        if (NavMesh.SamplePosition(groundPosition, out NavMeshHit navHit, offRoadTolerance, NavMesh.AllAreas))
        {
            if (isOffRoad)
            {
                isOffRoad = false;
                if (warningTextObject != null) warningTextObject.SetActive(false);
                if (arrowVisualContainer != null && currentTarget != null) arrowVisualContainer.SetActive(true);
            }
        }
        else
        {
            if (!isOffRoad)
            {
                isOffRoad = true;
                if (warningTextObject != null) warningTextObject.SetActive(true);
                if (arrowVisualContainer != null) arrowVisualContainer.SetActive(false);
            }
        }
    }

    private void UpdateTarget()
    {
        string targetTag = cargoManager.HasCargo() ? "Dropoff" : "Pickup";
        GameObject[] targets = GameObject.FindGameObjectsWithTag(targetTag);

        if (targets.Length == 0)
        {
            currentTarget = null;
            if (arrowVisualContainer != null) arrowVisualContainer.SetActive(false);
            return;
        }

        float closestDistance = Mathf.Infinity;
        Transform bestTarget = null;

        foreach (GameObject target in targets)
        {
            if (target == null || !target.activeInHierarchy) continue;

            float dist = Vector3.Distance(transform.position, target.transform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                bestTarget = target.transform;
            }
        }

        currentTarget = bestTarget;
    }

    private void PointArrow()
    {
        if (currentTarget == null) return;

        if (arrowVisualContainer != null && !isOffRoad) arrowVisualContainer.SetActive(true);

        Vector3 directionToPoint = Vector3.zero;

        // ПРИВЯЗКА К NAVMESH: ищем ближайшие валидные точки на дороге для старта и цели
        bool hasStartPoint = NavMesh.SamplePosition(transform.position, out NavMeshHit startHit, 10f, NavMesh.AllAreas);
        bool hasTargetPoint = NavMesh.SamplePosition(currentTarget.position, out NavMeshHit targetHit, 10f, NavMesh.AllAreas);

        if (hasStartPoint && hasTargetPoint)
        {
            // Строим маршрут строго между найденными точками на дороге
            if (NavMesh.CalculatePath(startHit.position, targetHit.position, roadAreaMask, path))
            {
                if (path.corners.Length > 1)
                {
                    bool foundWaypoint = false;
                    for (int i = 1; i < path.corners.Length; i++)
                    {
                        if (Vector3.Distance(transform.position, path.corners[i]) > minLookAheadDistance)
                        {
                            directionToPoint = path.corners[i] - transform.position;
                            foundWaypoint = true;
                            break;
                        }
                    }

                    if (!foundWaypoint)
                    {
                        directionToPoint = path.corners[path.corners.Length - 1] - transform.position;
                    }
                }
            }
        }

        // Если маршрут построить не удалось вообще, используем прямую линию как запасной вариант
        if (directionToPoint == Vector3.zero)
        {
            directionToPoint = currentTarget.position - transform.position;
        }

        directionToPoint.y = 0; // Игнорируем перепады высоты

        if (directionToPoint != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(directionToPoint);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
        }
    }

    // Голубая линия маршрута в окне Scene для отладки
    private void OnDrawGizmos()
    {
        if (path != null && path.corners != null && path.corners.Length > 1)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < path.corners.Length - 1; i++)
            {
                Gizmos.DrawLine(path.corners[i], path.corners[i + 1]);
            }
        }
    }
}