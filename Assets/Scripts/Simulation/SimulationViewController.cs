using UnityEngine;

//Para que este controlador se cree antes que otros controladores que dependan. 
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(GridGenerator))]
public class SimulationViewController : MonoBehaviour
{
    //Guarda los componentes que se necesitan para mostrar la simulacion.
    [SerializeField] private WebSocketController webSocketController;
    private GridGenerator generator;
    [SerializeField] private VehicleSpawner vehicleSpawner;
    private bool vehiclesReady;
    private FieldCameraController fieldCamera;
    private FarmLayoutController farmLayout;
    private WebSocketController.SimulationMessage renderedInitialization;

    //Guarda y "Inicializa" los componentes.
    private void Awake()
    {
        generator = GetComponent<GridGenerator>();
        generator.generateOnStart = false;
        fieldCamera = GetComponent<FieldCameraController>();
        if (fieldCamera == null) fieldCamera = gameObject.AddComponent<FieldCameraController>();
        farmLayout = GetComponent<FarmLayoutController>();
        if (farmLayout == null) farmLayout = gameObject.AddComponent<FarmLayoutController>();
        if (vehicleSpawner == null) vehicleSpawner = GetComponent<VehicleSpawner>();
        if (vehicleSpawner != null) vehicleSpawner.spawnOnStart = false;
        else Debug.LogWarning("Assign a VehicleSpawner to display Python vehicles.", this);
    }

    //Encuentra WebSocketController y se subscribe a los eventos de simulacion cuando recibe mensaje.
    private void OnEnable()
    {
        if (webSocketController == null)
        {
            var connections = FindObjectsByType<WebSocketController>();
            if (connections.Length == 1) webSocketController = connections[0];
        }
        if (webSocketController == null)
        {
            Debug.LogError("Assign the simulation WebSocketController in SimulationViewController.", this);
            return;
        }
        webSocketController.SimulationUpdated += OnSimulationMessage;
        OnSimulationMessage(webSocketController.LatestInitialization);
        if (webSocketController.LatestMessage?.type == "simulation_step")
            OnSimulationMessage(webSocketController.LatestMessage);
    }

    //Se desuscribe de los eventos. 
    private void OnDisable()
    {
        if (webSocketController != null)
            webSocketController.SimulationUpdated -= OnSimulationMessage;
    }

    //Cuando recibe un mensaje de simulacion, procesa el mensaje y actualiza la vista.

    private void OnSimulationMessage(WebSocketController.SimulationMessage message)
    {
        if (message == null) return;
        if (message.type == "simulation_init")
        {
            //Si el mensaje es el mismo, no hace nada. Ya se inicializo.
            if (ReferenceEquals(message, renderedInitialization)) return;
            vehiclesReady = false;

            //Construye el campo.
            if (!generator.GenerateFromSimulation(message)) return;
            renderedInitialization = message;

            //Calcula las dimensiones del campo en el mundo. Se usan para posicionar camaras y edificios de la granja.
            Bounds fieldBounds = generator.GetFieldBounds(message.world.field);
            farmLayout.Layout(fieldBounds);
            fieldCamera.FrameField(fieldBounds);
            if (vehicleSpawner != null)
                vehiclesReady = vehicleSpawner.SpawnFromSimulation(message.agents, generator);
        }

        //Si es un step actualiza los vehiculos y quita los cultivos que fueron cosechados.
        else if (message.type == "simulation_step" && renderedInitialization != null)
        {
            if (vehiclesReady) vehicleSpawner.UpdatePositions(message.agents, generator);
            if (message.agents == null) return;
            foreach (var agent in message.agents)
            {
                if (agent != null && agent.type == "harvester" && agent.harvesting && agent.position != null)
                    generator.MarkHarvested(agent.position.x, agent.position.y);
            }
        }
    }
}
