using System;
using System.Text;
using NativeWebSocket;
using UnityEngine;

public class WebSocketController : MonoBehaviour
{
    //URL de Servidor Python
    [SerializeField] private string serverUrl = "ws://localhost:8765";

    //Websocket + Mensajes Ultimos
    private WebSocket socket;

    public SimulationMessage LatestMessage { get; private set; }
    public SimulationMessage LatestInitialization { get; private set; }
    public float PlaybackSpeed { get; private set; } = 5f;
    public float SimulationStepDuration { get; private set; } = 0.5f;

    //Una accion que invoca WebSocketController y otros controladores se subscriben para poder recibir los mensajes de simulacion
    public event Action<SimulationMessage> SimulationUpdated;

    //Las clases de datos que se reciben desde el servidor Python.
    [Serializable]
    public class SimulationMessage
    {
        public string type;
        public double simulation_time;
        public float playback_speed;
        public string status;
        public SimulationData simulation;
        public AgentData[] agents;
        public KpiData data;
        public WorldData world;
        public CellPosition[] obstacles;
        public RegionData[] regions;
    }

    [Serializable]
    public class RegionData
    {
        public int id;
        public int x_min;
        public int x_max;
        public int y_min;
        public int y_max;
    }

    [Serializable]
    public class SimulationData
    {
        public int width;
        public int height;
        public float meters_per_cell = 5f;
        public float delta_time;
    }

    [Serializable]
    public class CellPosition
    {
        public int x;
        public int y;
    }

    [Serializable]
    public class FieldData
    {
        public int x;
        public int y;
        public int width;
        public int height;
    }

    [Serializable]
    public class WorldData
    {
        public int width;
        public int height;
        public FieldData field;
        public CellPosition unload_point;
    }

    [Serializable]
    public class AgentData
    {
        public int id;
        public string type;
        public bool active;
        public bool harvesting;
        public bool full;
        public bool going_to_unload;
        public float load;
        public float capacity;
        public float fuel;
        public float fuel_capacity;
        public float fuel_consumed;
        public string operating_state;
        public float transferred_kg;
        public float delivered_kg;
        public float distance_m;
        public CellPosition position;
    }

    [Serializable]
    public class KpiData
    {
        public int harvested;
        public float harvested_kg;
        public float harvest_progress;
        public float total_fuel_consumed;
    }


    //Crea el cliente de Websocket y se conecta al servidor Python. 
    // Se subscribe a los eventos de conexion, mensaje, error y desconexion.
    private async void OnEnable()
    {
        try
        {
            //Actualiza el status en el Dashboard.
            SetStatus("Connecting");

            socket = new WebSocket(serverUrl);
            socket.OnOpen += OnConnected;
            socket.OnMessage += OnMessageReceived;
            socket.OnError += OnConnectionError;
            socket.OnClose += OnDisconnected;

            Debug.Log($"Connecting to Python at {serverUrl}...", this);
            await socket.Connect();
        }
        catch (Exception exception)
        {
            SetStatus("Connection failed");
            Debug.LogError($"Python connection failed: {exception.Message}");
        }
    }

    //Actualiza el status en el Dashboard cuando te conectes al servidor Python.
    private void OnConnected()
    {
        SetStatus("Connected");
        Debug.Log("Connected to Python. Waiting for simulation data.", this);
    }

    //Status
    public string Status { get; private set; } = "Disconnected";

    //Invoca una accion que otros controladores se subscriben para poder recibir los mensajes de status. 
    // Principalmente para actualizar el Dashboard.
    public event Action StatusChanged;

    //Para los IF statements si existe un socket y si el estado del socket es abierto, entonces se puede enviar un mensaje al servidor Python.
    public bool CanSend => socket != null && socket.State == WebSocketState.Open;

    //Comandos publicos para mandar un comando al servidor Python.
    public void StartSimulation() => SendCommand("start");
    public void PauseSimulation() => SendCommand("pause");
    public void ResumeSimulation() => SendCommand("resume");
    public void RestartSimulation() => SendCommand("restart");
    public void SetPlaybackSpeed(float speed)
    {
        if (speed != 2.5f && speed != 5f && speed != 10f && speed != 25f && speed != 50f) return;
        SendCommand("set_speed:" + speed.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    //Igual que los otros comandos pero envia mas informacion de Python para cambiar los parametros.

    public void ConfigureAndRestart(int width, int height, int harvesters, int carts, int obstacles, int steps)
    {
        SendCommand($"configure_restart:{width}:{height}:{harvesters}:{carts}:{obstacles}:{steps}");
    }

    //Cambia el status y invoca la accion StatusChanged para que otros controladores puedan recibir el mensaje de status.
    private void SetStatus(string value)
    {
        Status = value;
        StatusChanged?.Invoke();
    }

    private async void SendCommand(string command)
    {
        //Solo manda comandos si el socket esta abierto y no se ha enviado un comando mientras se espera respuesta.
        if (!CanSend || Status == "Waiting for server") return;

        SetStatus("Waiting for server");
        try
        {
            //Manda comando al servidor Python. 
            // Si el comando es configure_restart, entonces envia un JSON con los parametros de configuracion.
            if (command.StartsWith("configure_restart:"))
            {
                string[] values = command.Split(':');
                await socket.SendText($"{{\"command\":\"configure_restart\",\"width\":{values[1]},\"height\":{values[2]},\"num_harvesters\":{values[3]},\"num_grain_carts\":{values[4]},\"num_obstacles\":{values[5]},\"steps\":{values[6]}}}");
            }
            else if (command.StartsWith("set_speed:"))
                await socket.SendText("{\"command\":\"set_speed\",\"speed\":" + command.Split(':')[1] + "}");
            else
                await socket.SendText("{\"command\":\"" + command + "\"}");
        }

        //Por si no se pueden enviar los comandos.
        catch (Exception exception)
        {
            SetStatus("Command failed");
            Debug.LogError($"Command failed: {exception.Message}", this);
        }
    }
    private void OnMessageReceived(byte[] bytes)
    {
        //Convierte el mensaje de bytes a string y luego a un objeto SimulationMessage.
        string json = Encoding.UTF8.GetString(bytes);
        SimulationMessage message;
        try
        {
            message = JsonUtility.FromJson<SimulationMessage>(json);
        }
        catch (ArgumentException exception)
        {
            //Por si es invalido. No deberia pasar esto... pero por si acaso.
            Debug.LogWarning($"Invalid Python JSON: {exception.Message}", this);
            return;
        }

        if (message != null && message.playback_speed > 0 && !float.IsInfinity(message.playback_speed))
            PlaybackSpeed = message.playback_speed;
        if (message?.simulation != null && message.simulation.delta_time > 0)
            SimulationStepDuration = message.simulation.delta_time;

        //Si el mensaje es de tipo estatus, actualiza el estatus.

        if (message != null && message.type == "simulation_status")
        {
            if (!string.IsNullOrEmpty(message.status)) SetStatus(message.status);
            return;
        }

        //Si el mensaje es null o invalido, ignorarlo.
        if (message == null || (message.type != "simulation_init" && message.type != "simulation_step"))
            return;

        //No hay agentes o datos KPI, no se puede procesar el mensaje.
        if (message.agents == null || (message.type == "simulation_step" && message.data == null))
        {
            Debug.LogWarning("Python message is missing agents or KPI data.", this);
            return;
        }

        //Si es un mensaje de inicializacion, guarda el mensaje y actualiza el estatus a "Ready".
        if (message.type == "simulation_init")
        {
            SetStatus("Ready");
            LatestInitialization = message;
            Debug.Log($"Python message: {json}", this);
        }

        //Recibio el primer mensaje de simulacion en vivo. 
        else if (LatestMessage == null || LatestMessage.type != "simulation_step")
            Debug.Log("Receiving live simulation updates from Python.", this);

        //Si es un mensaje de paso de simulacion, guarda el mensaje y invoca el comando para que otros controladores se enteren.
        if (message.type == "simulation_step" && Status != "Waiting for server") SetStatus("Running");
        LatestMessage = message;
        SimulationUpdated?.Invoke(message);
    }

    //Error de coneccion. Revisa URL.
    private void OnConnectionError(string error)
    {
        SetStatus("Connection error");
        Debug.LogError($"Python WebSocket error: {error}", this);
    }

    //Desconectado. Revisa si el servidor Python esta corriendo. 
    private void OnDisconnected(WebSocketCloseCode code)
    {
        SetStatus("Disconnected");
        Debug.Log($"Python disconnected: {code}", this);
    }

    //Cuando ya termines la simulacion, desconecta el servidor Python y limpia los eventos. Cierra la conneccion. 
    private async void OnDisable()
    {
        WebSocket connection = socket;
        socket = null;
        SetStatus("Disconnected");
        if (connection == null)
            return;

        connection.OnOpen -= OnConnected;
        connection.OnMessage -= OnMessageReceived;
        connection.OnError -= OnConnectionError;
        connection.OnClose -= OnDisconnected;

        try
        {
            if (connection.State == WebSocketState.Open)
                await connection.Close();
            else if (connection.State == WebSocketState.Connecting)
                connection.CancelConnection();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not close Python connection: {exception.Message}");
        }
    }
}
