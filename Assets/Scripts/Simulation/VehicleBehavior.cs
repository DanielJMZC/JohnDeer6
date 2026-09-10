using UnityEngine;
using UnityEngine.Splines;
using TMPro;

public class VehicleBehavior : MonoBehaviour
{
    public GameObject m_Vehicle;
    public float progress = 0f;
    public float speed = 0.1f;
    public float load;
    public float capacity;
    public TextMeshProUGUI loadText;
    public SplineContainer path;
    public float LoadKg { get; private set; }
    public float CapacityKg { get; private set; }
    public float FuelLiters { get; private set; }
    public float FuelCapacityLiters { get; private set; }
    public float FuelConsumedLiters { get; private set; }
    public string OperatingState { get; private set; }

    public void ApplyTelemetry(WebSocketController.AgentData agent)
    {
        load = agent.load;
        capacity = agent.capacity;
        LoadKg = agent.load;
        CapacityKg = agent.capacity;
        FuelLiters = agent.fuel;
        FuelCapacityLiters = agent.fuel_capacity;
        FuelConsumedLiters = agent.fuel_consumed;
        OperatingState = agent.operating_state;
        if (loadText != null) loadText.text = TelemetryText(agent);
    }

    public static string TelemetryText(WebSocketController.AgentData agent)
    {
        string state;
        switch (agent.operating_state)
        {
            case "unloaded": state = "Descarga realizada"; break;
            case "unloading": state = "Descargando al tractor"; break;
            case "receiving": state = "Recibiendo grano"; break;
            case "going_to_unload": state = "En camino a descargar"; break;
            case "harvesting": state = "Cosechando"; break;
            case "full": state = "Lleno"; break;
            case "moving": state = "En movimiento"; break;
            case "idle": state = "En espera"; break;
            default: state = "Sin estado del backend"; break;
        }
        return $"Carga: {agent.load:F2}/{agent.capacity:F2} kg | Combustible: {agent.fuel:F2}/{agent.fuel_capacity:F2} L | Consumido: {agent.fuel_consumed:F2} L | {state}";
    }
}
