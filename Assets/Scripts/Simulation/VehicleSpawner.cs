using System.Collections.Generic;
using UnityEngine;

public class VehicleSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject prefabTractor;
    public GameObject prefabCosechadora;

    [Header("Cantidad")]
    [Range(0, 20)] public int cantidadTractores = 2;
    [Range(0, 20)] public int cantidadCosechadoras = 1;

    [Header("Espaciado")]
    public float espacioEntreVehiculos = 4f;
    private float offsetActual = 0f;
    public bool spawnOnStart = true;
    [Tooltip("Vertical offset from the grid cell center, in Unity units.")]
    public float vehicleHeightOffset;
    [Header("Movement smoothing")]
    [SerializeField, Min(0.01f)] private float movementDuration = 0.1f;
    private sealed class Motion
    {
        public Vector3 start;
        public Vector3 target;
        public Quaternion startRotation;
        public Quaternion targetRotation;
        public float elapsed;
        public float duration;
    }
    private readonly Dictionary<int, Motion> motions = new Dictionary<int, Motion>();
    private GameObject generatedRoot;
    private readonly Dictionary<int, GameObject> vehicles = new Dictionary<int, GameObject>();

    private void Start()
    {
        if (!spawnOnStart) return;
        CreateRoot();
        ColocarVehiculos(prefabTractor, cantidadTractores);
        ColocarVehiculos(prefabCosechadora, cantidadCosechadoras);
    }

    private void ColocarVehiculos(GameObject prefab, int cantidad)
    {
        if (prefab == null)
        {
            if (cantidad > 0) Debug.LogError("VehicleSpawner has an unassigned vehicle prefab.", this);
            return;
        }
        for (int i = 0; i < cantidad; i++)
        {
            Vector3 posicion = transform.position + new Vector3(offsetActual, 0f, 0f);
            Instantiate(prefab, posicion, Quaternion.identity, generatedRoot.transform);

            offsetActual += espacioEntreVehiculos;
        }
    }

    public bool SpawnFromSimulation(WebSocketController.AgentData[] agents, GridGenerator grid)
    {
        if (agents == null || grid == null || grid.grid == null) return false;
        var ids = new HashSet<int>();
        foreach (var agent in agents)
        {
            if (agent == null || !ids.Add(agent.id) || PrefabFor(agent.type) == null)
            {
                Debug.LogError("Cannot spawn simulation: duplicate ID, unknown agent type, or missing tractor/harvester prefab.", this);
                return false;
            }
        }

        ClearVehicles();
        CreateRoot();
        generatedRoot.SetActive(false);
        cantidadTractores = 0;
        cantidadCosechadoras = 0;
        foreach (var agent in agents)
        {
            // Keep the parent inactive while preparing instances with unknown positions.
            var instance = Instantiate(PrefabFor(agent.type), generatedRoot.transform);
            instance.name = $"{agent.type}_{agent.id}";
            instance.SetActive(false);
            vehicles.Add(agent.id, instance);
            if (agent.type == "harvester") cantidadCosechadoras++;
            else cantidadTractores++;
        }
        UpdatePositions(agents, grid);
        generatedRoot.SetActive(true);
        Debug.Log($"Prepared {vehicles.Count} Python vehicles. Vehicles appear when positions arrive.", this);
        return true;
    }

    public void UpdatePositions(WebSocketController.AgentData[] agents, GridGenerator grid)
    {
        if (agents == null || grid == null || grid.grid == null) return;
        foreach (var agent in agents)
        {
            if (agent == null || agent.position == null || !vehicles.TryGetValue(agent.id, out var instance)) continue;
            Vector3 target = grid.CellToWorld(agent.position.x, agent.position.y) + Vector3.up * vehicleHeightOffset;
            if (!motions.TryGetValue(agent.id, out Motion motion))
            {
                // Initial placement must not animate from the prefab's origin.
                instance.transform.position = target;
                motions.Add(agent.id, new Motion
                {
                    start = target,
                    target = target,
                    startRotation = instance.transform.rotation,
                    targetRotation = instance.transform.rotation,
                    duration = 0
                });
            }
            else if ((target - motion.target).sqrMagnitude > 0.0001f)
            {
                // Repeated stationary snapshots must not restart an in-flight move.
                motion.start = instance.transform.position;
                motion.target = target;
                motion.startRotation = instance.transform.rotation;
                Vector3 direction = target - motion.start;
                direction.y = 0;
                motion.targetRotation = direction.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(direction, Vector3.up)
                    : motion.startRotation;
                motion.elapsed = 0;
                motion.duration = Mathf.Max(0.01f, movementDuration);
            }
            // Python's active flag means movement, not visibility.
            instance.SetActive(true);
        }
    }

    public void ClearVehicles()
    {
        if (generatedRoot != null)
        {
            generatedRoot.SetActive(false);
            Destroy(generatedRoot);
        }
        generatedRoot = null;
        vehicles.Clear();
        motions.Clear();
        offsetActual = 0;
    }

    private void Update()
    {
        foreach (var entry in motions)
        {
            Motion motion = entry.Value;
            if (motion.duration <= 0 || motion.elapsed >= motion.duration) continue;
            if (!vehicles.TryGetValue(entry.Key, out GameObject instance) || instance == null) continue;
            motion.elapsed = Mathf.Min(motion.duration, motion.elapsed + Time.unscaledDeltaTime);
            float fraction = motion.elapsed / motion.duration;
            instance.transform.SetPositionAndRotation(
                Vector3.Lerp(motion.start, motion.target, fraction),
                Quaternion.Slerp(motion.startRotation, motion.targetRotation, fraction));
        }
    }

    private GameObject PrefabFor(string type)
    {
        if (type == "harvester") return prefabCosechadora;
        if (type == "grain_cart") return prefabTractor;
        return null;
    }

    private void CreateRoot()
    {
        generatedRoot = new GameObject("Generated Vehicles");
        generatedRoot.transform.SetParent(transform, false);
    }
}
