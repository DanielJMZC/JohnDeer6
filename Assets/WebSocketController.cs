using System;
using System.Text;
using NativeWebSocket;
using UnityEngine;

public class WebSocketController : MonoBehaviour
{
    [SerializeField] private string serverUrl = "ws://localhost:8765";

    private WebSocket socket;

    public SimulationMessage LatestMessage { get; private set; }
    public event Action<SimulationMessage> SimulationUpdated;

    [Serializable]
    public class SimulationMessage
    {
        public string type;
        public double simulation_time;
        public AgentData[] agents;
        public KpiData data;
    }

    [Serializable]
    public class AgentData
    {
        public int id;
        public string type;
        public bool active;
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
            Debug.LogError($"Python connection failed: {exception.Message}");
        }
    }

    private void OnConnected()
    {
        Debug.Log("Connected to Python. Waiting for simulation data.", this);
    }

    [ContextMenu("Start Simulation")]
    public async void StartSimulation()
    {
        WebSocket connection = socket;
        if (!Application.isPlaying || connection == null || connection.State != WebSocketState.Open)
        {
            Debug.LogWarning("Enter Play Mode and wait for the Python connection before starting the simulation.", this);
            return;
        }

        try
        {
            await connection.SendText("{\"command\":\"start\"}");
            Debug.Log("Start command sent to Python. Waiting for simulation updates.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not send start command: {exception.Message}");
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

        if (message == null || (message.type != "simulation_init" && message.type != "simulation_step"))
            return;

        if (message.agents == null || (message.type == "simulation_step" && message.data == null))
        {
            Debug.LogWarning("Python message is missing agents or KPI data.", this);
            return;
        }

        if (message.type == "simulation_init")
            Debug.Log($"Python message: {json}", this);
        else if (LatestMessage == null || LatestMessage.type != "simulation_step")
            Debug.Log("Receiving live simulation updates from Python.", this);

        LatestMessage = message;
        SimulationUpdated?.Invoke(message);
    }

    private void OnConnectionError(string error)
    {
        Debug.LogError($"Python WebSocket error: {error}", this);
    }

    private void OnDisconnected(WebSocketCloseCode code)
    {
        Debug.Log($"Python disconnected: {code}", this);
    }

    private async void OnDisable()
    {
        WebSocket connection = socket;
        socket = null;
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
