using UnityEngine;
using UnityEngine.AI;

public class ArrowNavigation : MonoBehaviour
{
    [Header("Ссылки")]
    public GameObject warningTextObject;
    public GameObject arrowVisualContainer;

    [Header("Настройки навигатора")]
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float sampleRadius = 5.0f;

    [Header("Умный маршрут")]
    [SerializeField] private float deviationThreshold = 15f; // Насколько можно отъехать от маршрута (в метрах)
    [SerializeField] private float waypointReachRadius = 10f; // За сколько метров засчитывать прохождение поворота

    private NavMeshPath activePath;
    private Transform currentTarget;
    private int currentCornerIndex = 0;

    private int roadAreaMask;
    private int roadAreaIndex;
    private bool isCurrentlyOnRoad = true;

    private CargoManager cargoManager;
    private Vector3 currentTargetDirection;

    void Start()
    {
        activePath = new NavMeshPath();
        cargoManager = GetComponentInParent<CargoManager>();

        if (warningTextObject != null) warningTextObject.SetActive(false);

        roadAreaIndex = NavMesh.GetAreaFromName("Road");
        roadAreaMask = (roadAreaIndex != -1) ? (1 << roadAreaIndex) : NavMesh.AllAreas;

        ToggleRoadState(CheckIfPlayerIsOnRoad());
    }

    void Update()
    {
        bool isOnRoad = CheckIfPlayerIsOnRoad();
        if (isOnRoad != isCurrentlyOnRoad) ToggleRoadState(isOnRoad);

        if (!isOnRoad) return;

        CheckAndTargetUpdate();
        FollowActivePath();

        // Плавный поворот стрелки
        if (currentTargetDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(currentTargetDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
    }

    void CheckAndTargetUpdate()
    {
        if (cargoManager == null) return;

        string targetTag = cargoManager.HasCargo() ? "Dropoff" : "Pickup";
        GameObject[] targets = GameObject.FindGameObjectsWithTag(targetTag);

        if (targets.Length == 0)
        {
            currentTarget = null;
            return;
        }

        // Ищем ближайшую цель
        float closestDistance = Mathf.Infinity;
        Transform bestTarget = null;
        foreach (GameObject target in targets)
        {
            if (target == null || !target.activeInHierarchy) continue;
            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                bestTarget = target.transform;
            }
        }

        // Если цель сменилась (взяли или отдали груз) или маршрута вообще нет - строим новый
        if (currentTarget != bestTarget || activePath.corners.Length == 0)
        {
            currentTarget = bestTarget;
            CalculateNewPath();
        }
        else
        {
            // Проверяем, не сильно ли мы отклонились от текущего маршрута
            float distanceToRoute = GetDistanceToPath(transform.position, activePath);
            if (distanceToRoute > deviationThreshold)
            {
                Debug.Log("Перестроение маршрута! Отклонение: " + distanceToRoute + "м.");
                CalculateNewPath();
            }
        }
    }

    void CalculateNewPath()
    {
        if (currentTarget == null) return;

        Vector3 startPos = transform.position;
        Vector3 targetPos = currentTarget.position;

        bool startSnapped = NavMesh.SamplePosition(startPos, out NavMeshHit startHit, sampleRadius, NavMesh.AllAreas);
        bool targetSnapped = NavMesh.SamplePosition(targetPos, out NavMeshHit targetHit, sampleRadius, NavMesh.AllAreas);

        if (startSnapped && targetSnapped)
        {
            NavMesh.CalculatePath(startHit.position, targetHit.position, roadAreaMask, activePath);

            // Если путь успешно построен, начинаем ехать к первой точке поворота (индекс 1)
            if (activePath.status == NavMeshPathStatus.PathComplete && activePath.corners.Length > 1)
            {
                currentCornerIndex = 1;
            }
        }
    }

    void FollowActivePath()
    {
        if (activePath == null || activePath.corners.Length == 0 || currentTarget == null)
        {
            currentTargetDirection = Vector3.zero;
            return;
        }

        // Проверяем, достигли ли мы текущего поворота
        if (currentCornerIndex < activePath.corners.Length)
        {
            // Игнорируем перепады высот при расчете дистанции
            Vector2 playerPos2D = new Vector2(transform.position.x, transform.position.z);
            Vector2 cornerPos2D = new Vector2(activePath.corners[currentCornerIndex].x, activePath.corners[currentCornerIndex].z);

            // Если подъехали достаточно близко к повороту — переключаемся на следующий
            if (Vector2.Distance(playerPos2D, cornerPos2D) < waypointReachRadius)
            {
                currentCornerIndex++;
            }
        }

        // Направляем стрелку на актуальный поворот
        if (currentCornerIndex < activePath.corners.Length)
        {
            Vector3 dir = activePath.corners[currentCornerIndex] - transform.position;
            dir.y = 0;
            if (dir != Vector3.zero) currentTargetDirection = dir;
        }
        else
        {
            // Если кончились точки маршрута (мы на финишной прямой) - смотрим прямо на базу
            Vector3 dir = currentTarget.position - transform.position;
            dir.y = 0;
            if (dir != Vector3.zero) currentTargetDirection = dir;
        }
    }

    // Математика: вычисляет дистанцию от машины до ближайшей ЛИНИИ маршрута
    float GetDistanceToPath(Vector3 position, NavMeshPath path)
    {
        if (path == null || path.corners.Length < 2) return 0f;

        float minDistance = float.MaxValue;
        for (int i = 0; i < path.corners.Length - 1; i++)
        {
            float dist = DistancePointToLineSegment(position, path.corners[i], path.corners[i + 1]);
            if (dist < minDistance) minDistance = dist;
        }
        return minDistance;
    }

    float DistancePointToLineSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        Vector3 lineDir = lineEnd - lineStart;
        float lineLength = lineDir.magnitude;
        if (lineLength == 0) return Vector3.Distance(point, lineStart);

        lineDir.Normalize();
        float projectLength = Mathf.Clamp(Vector3.Dot(point - lineStart, lineDir), 0f, lineLength);
        Vector3 closestPoint = lineStart + lineDir * projectLength;

        return Vector3.Distance(point, closestPoint);
    }

    void ToggleRoadState(bool isOnRoad)
    {
        isCurrentlyOnRoad = isOnRoad;
        if (arrowVisualContainer != null) arrowVisualContainer.SetActive(isOnRoad);
        if (warningTextObject != null) warningTextObject.SetActive(!isOnRoad);
    }

    bool CheckIfPlayerIsOnRoad()
    {
        Vector3 groundPosition = transform.position;
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit rayHit, 10f)) groundPosition = rayHit.point;
        if (NavMesh.SamplePosition(groundPosition, out NavMeshHit navHit, 3.0f, NavMesh.AllAreas))
        {
            if (roadAreaIndex != -1) return (navHit.mask & (1 << roadAreaIndex)) != 0;
            return true;
        }
        return false;
    }

    // Отрисовка отладочной графики в редакторе (зеленая линия - маршрут, красный шар - текущая цель стрелки)
    private void OnDrawGizmosSelected()
    {
        if (activePath != null && activePath.corners != null && activePath.corners.Length > 1)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < activePath.corners.Length - 1; i++) Gizmos.DrawLine(activePath.corners[i], activePath.corners[i + 1]);

            if (currentCornerIndex < activePath.corners.Length)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(activePath.corners[currentCornerIndex], 1.5f);
            }
        }
    }
}