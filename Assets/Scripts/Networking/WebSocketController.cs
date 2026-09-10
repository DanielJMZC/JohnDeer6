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

    //Una accion que invoca WebSocketController y otros controladores se subscriben para poder recibir los mensajes de simulacion
    public event Action<SimulationMessage> SimulationUpdated;

    //Las clases de datos que se reciben desde el servidor Python.
    [Serializable]
    public class SimulationMessage
    {
        public string type;
        public double simulation_time;
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
        public int load;
        public int capacity;
        public float fuel;
        public float fuel_capacity;
        public float fuel_consumed;
        public CellPosition position;
    }

    [Serializable]
    public class KpiData
    {
        public int harvested;
        public float harvest_progress;
        public float total_fuel_consumed;
    }


    
    private async void OnEnable()
    {
        try
        {
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

    private void OnConnected()
    {
        SetStatus("Connected");
        Debug.Log("Connected to Python. Waiting for simulation data.", this);
    }

    public string Status { get; private set; } = "Disconnected";
    public event Action StatusChanged;
    public bool CanSend => socket != null && socket.State == WebSocketState.Open;
    public void StartSimulation() => SendCommand("start");
    public void PauseSimulation() => SendCommand("pause");
    public void ResumeSimulation() => SendCommand("resume");
    public void RestartSimulation() => SendCommand("restart");

    public void ConfigureAndRestart(int width, int height, int harvesters, int carts, int obstacles, int steps)
    {
        SendCommand($"configure_restart:{width}:{height}:{harvesters}:{carts}:{obstacles}:{steps}");
    }

    private void SetStatus(string value)
    {
        Status = value;
        StatusChanged?.Invoke();
    }

    private async void SendCommand(string command)
    {
        if (!CanSend || Status == "Waiting for server") return;
        SetStatus("Waiting for server");
        try
        {
            if (command.StartsWith("configure_restart:"))
            {
                string[] values = command.Split(':');
                await socket.SendText($"{{\"command\":\"configure_restart\",\"width\":{values[1]},\"height\":{values[2]},\"num_harvesters\":{values[3]},\"num_grain_carts\":{values[4]},\"num_obstacles\":{values[5]},\"steps\":{values[6]}}}");
            }
            else
                await socket.SendText("{\"command\":\"" + command + "\"}");
        }
        catch (Exception exception)
        {
            SetStatus("Command failed");
            Debug.LogError($"Command failed: {exception.Message}", this);
        }
    }
    private void OnMessageReceived(byte[] bytes)
    {
        string json = Encoding.UTF8.GetString(bytes);
        SimulationMessage message;
        try
        {
            message = JsonUtility.FromJson<SimulationMessage>(json);
        }
        catch (ArgumentException exception)
        {
            Debug.LogWarning($"Invalid Python JSON: {exception.Message}", this);
            return;
        }

        if (message != null && message.type == "simulation_status")
        {
            if (!string.IsNullOrEmpty(message.status)) SetStatus(message.status);
            return;
        }
        if (message == null || (message.type != "simulation_init" && message.type != "simulation_step"))
            return;

        if (message.agents == null || (message.type == "simulation_step" && message.data == null))
        {
            Debug.LogWarning("Python message is missing agents or KPI data.", this);
            return;
        }

        if (message.type == "simulation_init")
        {
            SetStatus("Ready");
            LatestInitialization = message;
            Debug.Log($"Python message: {json}", this);
        }
        else if (LatestMessage == null || LatestMessage.type != "simulation_step")
            Debug.Log("Receiving live simulation updates from Python.", this);

        if (message.type == "simulation_step" && Status != "Waiting for server") SetStatus("Running");
        LatestMessage = message;
        SimulationUpdated?.Invoke(message);
    }

    private void OnConnectionError(string error)
    {
        SetStatus("Connection error");
        Debug.LogError($"Python WebSocket error: {error}", this);
    }

    private void OnDisconnected(WebSocketCloseCode code)
    {
        SetStatus("Disconnected");
        Debug.Log($"Python disconnected: {code}", this);
    }

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
