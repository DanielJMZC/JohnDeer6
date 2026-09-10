using System.Collections.Generic;
using UnityEngine;

public class VehicleSpawner : MonoBehaviour
{
    //Prefabs y cantidad de vehiculos.
    [Header("Prefabs")]
    public GameObject prefabTractor;
    public GameObject prefabCosechadora;

    [Header("Cantidad")]
    [Range(0, 20)] public int cantidadTractores = 2;
    [Range(0, 20)] public int cantidadCosechadoras = 1;

    //Para cuando hagan spawn tengan espacio entre ellos y esten posicionados correctamente.

    [Header("Espaciado")]
    public float espacioEntreVehiculos = 4f;
    private float offsetActual = 0f;
    public float PlaybackSpeed { get; set; } = 5f;
    public float SimulationStepDuration { get; set; } = 0.5f;
    public bool PlaybackPaused { get; set; }
    public bool spawnOnStart = true;
    [Tooltip("Vertical offset from the grid cell center, in Unity units.")]
    public float vehicleHeightOffset;

    //Para animar el vehiculo.
    private sealed class Motion
    {
        public Vector3 start;
        public Vector3 target;
        public Quaternion startRotation;
        public Quaternion targetRotation;
        public float elapsed;
        public float duration;
    }

    //ID de agente con su respectivo movimiento
    private readonly Dictionary<int, Motion> motions = new Dictionary<int, Motion>();

    //Donde se guardan los vehiculos que se instancian. Objeto vacio.
    private GameObject generatedRoot;

    //ID de agente con su vehiculo.
    private readonly Dictionary<int, GameObject> vehicles = new Dictionary<int, GameObject>();

    //Crea la ruta y coloca los vehiculos.
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

        //Posiciona los vehiculos y los instancea. 
        for (int i = 0; i < cantidad; i++)
        {
            Vector3 posicion = transform.position + new Vector3(offsetActual, 0f, 0f);
            Instantiate(prefab, posicion, Quaternion.identity, generatedRoot.transform);

            offsetActual += espacioEntreVehiculos;
        }
    }

    //Los crea en base a los datos de simulacion

    public bool SpawnFromSimulation(WebSocketController.AgentData[] agents, GridGenerator grid)
    {
        //Valida los vehiculos. Si no es valido no lo crea.
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

        //Crea los vehiculos y los posiciona en base a la simulacion.

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
            foreach (Camera vehicleCamera in instance.GetComponentsInChildren<Camera>(true))
                vehicleCamera.enabled = false;
            vehicles.Add(agent.id, instance);
            if (instance.GetComponentsInChildren<VehicleBehavior>(true).Length == 0)
                instance.AddComponent<VehicleBehavior>();
            foreach (var behavior in instance.GetComponentsInChildren<VehicleBehavior>(true))
                behavior.ApplyTelemetry(agent);
            if (agent.type == "harvester") cantidadCosechadoras++;
            else cantidadTractores++;
        }

        //Actualiza posiciones en base a la simulacion. 
        UpdatePositions(agents, grid);
        generatedRoot.SetActive(true);
        Debug.Log($"Prepared {vehicles.Count} Python vehicles. Vehicles appear when positions arrive.", this);
        return true;
    }

    public void UpdatePositions(WebSocketController.AgentData[] agents, GridGenerator grid)
    {
        if (agents == null || grid == null || grid.grid == null) return;

        //Si un vehiculo esta en la misma celula que otro, los separa en una formacion de cuadrado.
        var occupantCounts = new Dictionary<Vector2Int, int>();
        var occupantIndexes = new Dictionary<Vector2Int, int>();

        //Cuenta cuantos vehiculos hay en cada celula.
        foreach (var agent in agents)
        {
            if (agent?.position == null) continue;
            var cell = new Vector2Int(agent.position.x, agent.position.y);
            occupantCounts.TryGetValue(cell, out int count);
            occupantCounts[cell] = count + 1;
        }
        foreach (var agent in agents)
        {
            if (agent == null || !vehicles.TryGetValue(agent.id, out var instance)) continue;
            foreach (var behavior in instance.GetComponentsInChildren<VehicleBehavior>(true))
                behavior.ApplyTelemetry(agent);
            if (agent.position == null) continue;
            var cell = new Vector2Int(agent.position.x, agent.position.y);

            //Revisa cuantos vehiculos hay en la celula y los separa en una formacion de cuadrado. Es mas para el inciio donde hacen spawn en el mismo lugar.
            occupantIndexes.TryGetValue(cell, out int occupantIndex);
            occupantIndexes[cell] = occupantIndex + 1;
            int occupantCount = occupantCounts[cell];
            int columns = Mathf.CeilToInt(Mathf.Sqrt(occupantCount));
            int rows = Mathf.CeilToInt(occupantCount / (float)columns);
            int column = occupantIndex % columns;
            int row = occupantIndex / columns;
            Bounds vehicleBounds = CombinedRendererBounds(instance);
            float modelFootprint = Mathf.Max(vehicleBounds.size.x, vehicleBounds.size.z);
            float spacing = Mathf.Max(espacioEntreVehiculos, modelFootprint * .85f);
            Vector3 formationOffset = new Vector3(
                (column - (columns - 1) * .5f) * spacing,
                0,
                (row - (rows - 1) * .5f) * spacing);
            Vector3 target = grid.CellToWorld(cell.x, cell.y) + formationOffset + Vector3.up * vehicleHeightOffset;

            //Cuando recibe su primera posicion, hace el movimiento para llegar a su primera posicion.
            if (!motions.TryGetValue(agent.id, out Motion motion))
            {
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
            
            //Cuando recibe la primera posicion, posiciona el vehiculo inmediatamente.
            else if (motion.duration <= 0 || motion.elapsed >= motion.duration)
            {
                motion.start = instance.transform.position;
                motion.target = target;
                motion.startRotation = instance.transform.rotation;
                Vector3 direction = target - motion.start;
                direction.y = 0;
                motion.targetRotation = direction.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(direction, Vector3.up)
                    : motion.startRotation;
                motion.elapsed = 0;
                motion.duration = Mathf.Max(0.01f, SimulationStepDuration / Mathf.Max(0.01f, PlaybackSpeed));
            }
            //Si el movimiento anterior termino, prepara el movimiento hacia la posicion recibida.
            else if ((target - motion.target).sqrMagnitude > 0.0001f)
            {
                motion.start = instance.transform.position;
                motion.target = target;
                motion.startRotation = instance.transform.rotation;
                Vector3 direction = target - motion.start;
                direction.y = 0;
                motion.targetRotation = direction.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(direction, Vector3.up)
                    : motion.startRotation;
                motion.elapsed = 0;
                motion.duration = Mathf.Max(0.01f, SimulationStepDuration / Mathf.Max(0.01f, PlaybackSpeed));
            }

            //Muestra vehiculo cuando tenga posicion valida.
            instance.SetActive(true);
        }
    }

    //Elimina todos los vehiculos
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

    //Activa la camara del vehiculo seleccionado.
    //Las otras camaras son desactivadas. 
    public bool ShowVehicleCamera(int vehicleId, RenderTexture target)
    {
        bool found = false;
        foreach (var pair in vehicles)
        {
            if (pair.Value == null) continue;
            foreach (Camera vehicleCamera in pair.Value.GetComponentsInChildren<Camera>(true))
            {
                bool selected = pair.Key == vehicleId && !found;
                vehicleCamera.targetTexture = selected ? target : null;
                vehicleCamera.enabled = selected;
                if (selected) found = true;
            }
        }
        return found;
    }

    //Desatciva todas las camaras. Se utiliza cuando se cambia de la interfaz de vehiculo a la de Overview o Farm.
    public void HideVehicleCameras()
    {
        foreach (var pair in vehicles)
        {
            if (pair.Value == null) continue;
            foreach (Camera vehicleCamera in pair.Value.GetComponentsInChildren<Camera>(true))
            {
                vehicleCamera.enabled = false;
                vehicleCamera.targetTexture = null;
            }
        }
    }
    
    //Para cada frame, si el vehiculo tiene un movimiento pendiente, lo mueve hacia su destino.
    private void Update()
    {
        if (PlaybackPaused) return;
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

    //Regresa prefab correspondiente.

    private GameObject PrefabFor(string type)
    {
        if (type == "harvester") return prefabCosechadora;
        if (type == "grain_cart") return prefabTractor;
        return null;
    }

    //Calcula size del objeto.

    private static Bounds CombinedRendererBounds(GameObject instance)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(instance.transform.position, Vector3.one * 4);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private void CreateRoot()
    {
        generatedRoot = new GameObject("Generated Vehicles");
        generatedRoot.transform.SetParent(transform, false);
    }
}
