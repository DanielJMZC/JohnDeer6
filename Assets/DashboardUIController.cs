using System;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(PanelRenderer))]
public class DashboardUIController : MonoBehaviour
{
    private static readonly string[] TabNames =
    {
        "overview", "performance", "pressure", "efficiency", "harvest"
    };

    private readonly Button[] buttons = new Button[TabNames.Length];
    private readonly VisualElement[] pages = new VisualElement[TabNames.Length];
    private readonly Action[] clickHandlers = new Action[TabNames.Length];
    private PanelRenderer panelRenderer;
    private int selectedTab;
    [SerializeField] private WebSocketController webSocketController;
    private Label harvestPercentValue;
    private Label wheatValue;
    private Label fuelValue;
    private Label vehiclesValue;
    private Label timeValue;
    private LineChart harvestChart;
    private LineChart fuelChart;
    private LineChart activeChart;
    private double lastChartTime = -1;

    private void OnEnable()
    {
        if (webSocketController == null)
            webSocketController = GetComponent<WebSocketController>();
        if (webSocketController == null)
        {
            WebSocketController[] connections = FindObjectsByType<WebSocketController>(FindObjectsSortMode.None);
            if (connections.Length == 1)
                webSocketController = connections[0];
        }
        if (webSocketController != null)
        {
            webSocketController.SimulationUpdated += UpdateKpis;
            Debug.Log($"Dashboard linked to WebSocketController on {webSocketController.gameObject.name}.", this);
        }
        else
            Debug.LogWarning("Assign the simulation's WebSocketController in the DashboardUIController Inspector; no unique controller was found.", this);

        panelRenderer = GetComponent<PanelRenderer>();
        panelRenderer.RegisterUIReloadCallback(OnUIReload);
    }

    private void OnDisable()
    {
        if (webSocketController != null)
            webSocketController.SimulationUpdated -= UpdateKpis;
        harvestPercentValue = wheatValue = fuelValue = vehiclesValue = timeValue = null;
        if (panelRenderer != null)
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);

        UnbindTabs();
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement rootElement)
    {
        UnbindTabs();
        SetupCharts(rootElement);
        harvestPercentValue = rootElement.Q<Label>("kti_harvest_value");
        wheatValue = rootElement.Q<Label>("kti_wheat_value");
        fuelValue = rootElement.Q<Label>("kti_fuel_value");
        vehiclesValue = rootElement.Q<Label>("kti_vehicle_value");
        timeValue = rootElement.Q<Label>("kti_time_value");
        if (harvestPercentValue == null || wheatValue == null || fuelValue == null || vehiclesValue == null || timeValue == null)
            Debug.LogError("Dashboard KPI labels are missing. Check the assigned WebUI UXML.", this);
        UpdateKpis(webSocketController != null ? webSocketController.LatestMessage : null);

        for (int i = 0; i < TabNames.Length; i++)
        {
            string buttonName = $"graph_{TabNames[i]}_button";
            string pageName = $"graph_{TabNames[i]}_page";
            buttons[i] = rootElement.Q<Button>(buttonName);
            pages[i] = rootElement.Q<VisualElement>(pageName);

            if (buttons[i] == null || pages[i] == null)
            {
                Debug.LogError($"Dashboard UI is missing {buttonName} or {pageName}. Check the assigned UXML.", this);
                UnbindTabs();
                return;
            }
        }

        for (int i = 0; i < TabNames.Length; i++)
        {
            int tabIndex = i;
            clickHandlers[i] = () => SelectTab(tabIndex);
            buttons[i].clicked += clickHandlers[i];
        }

        SelectTab(selectedTab);
    }

    private void UpdateKpis(WebSocketController.SimulationMessage message)
    {
        UpdateCharts(message);
        bool hasStep = message != null && message.type == "simulation_step" && message.data != null;
        if (harvestPercentValue != null)
            harvestPercentValue.text = hasStep ? $"{message.data.harvest_progress:F1}%" : "--";
        if (wheatValue != null)
            wheatValue.text = hasStep ? $"{message.data.harvested} cells" : "--";
        if (fuelValue != null)
            fuelValue.text = hasStep ? $"{message.data.total_fuel_consumed:F2}" : "--";
        if (vehiclesValue != null)
            vehiclesValue.text = message?.agents != null ? message.agents.Length.ToString() : "--";
        if (timeValue != null)
        {
            if (hasStep)
            {
                double seconds = Math.Max(0, message.simulation_time);
                timeValue.text = $"{Math.Floor(seconds / 60):0}m {Math.Floor(seconds % 60):00}s";
            }
            else
                timeValue.text = "--";
        }
    }

    private void SetupCharts(VisualElement root)
    {
        if (harvestChart == null)
        {
            harvestChart = new LineChart { Title = "Harvest Progress" };
            harvestChart.YAxis.Title = "Harvested (%)";
            harvestChart.YAxis.Min = 0;
            harvestChart.YAxis.Max = 100;
            fuelChart = new LineChart { Title = "Fuel Consumption", LineColor = new Color(0.8f, 0.49f, 0.1f) };
            fuelChart.YAxis.Title = "Cumulative fuel consumed";
            fuelChart.YAxis.Min = 0;
            activeChart = new LineChart { Title = "Active Vehicles", LineColor = new Color(0.15f, 0.45f, 0.8f) };
            activeChart.YAxis.Title = "Vehicle count";
            activeChart.YAxis.Min = 0;
            activeChart.YAxis.Format = value => value.ToString("0");
            foreach (LineChart chart in new[] { harvestChart, fuelChart, activeChart })
            {
                chart.XAxis.Title = "Simulation time (s)";
                chart.XAxis.Min = 0;
            }
        }
        MountChart(root, "overview_harvest_graph", harvestChart);
        MountChart(root, "overview_fuel_graph", fuelChart);
        MountChart(root, "overview_active_graph", activeChart);
    }

    private void MountChart(VisualElement root, string containerName, LineChart chart)
    {
        VisualElement container = root.Q<VisualElement>(containerName);
        if (container == null)
        {
            Debug.LogError($"Missing chart container: {containerName}", this);
            return;
        }
        chart.RemoveFromHierarchy();
        container.Clear();
        container.Add(chart);
        chart.Refresh();
    }

    private void UpdateCharts(WebSocketController.SimulationMessage message)
    {
        if (harvestChart == null || message == null) return;
        if (message.type == "simulation_init" || message.simulation_time < lastChartTime)
        {
            harvestChart.ClearData();
            fuelChart.ClearData();
            activeChart.ClearData();
            lastChartTime = -1;
        }
        int total = message.agents?.Length ?? 0;
        activeChart.YAxis.Max = Math.Max(1, total);
        activeChart.YAxis.TickCount = Math.Max(2, total + 1);
        activeChart.Refresh();
        if (message.type != "simulation_step" || message.data == null || message.simulation_time == lastChartTime) return;
        int active = 0;
        foreach (var agent in message.agents)
            if (agent.active) active++;
        float time = (float)message.simulation_time;
        harvestChart.AddPoint(time, message.data.harvest_progress);
        fuelChart.AddPoint(time, message.data.total_fuel_consumed);
        activeChart.AddPoint(time, active);
        lastChartTime = message.simulation_time;
    }

    private void SelectTab(int tabIndex)
    {
        selectedTab = tabIndex;

        for (int i = 0; i < TabNames.Length; i++)
        {
            bool selected = i == tabIndex;
            pages[i].style.display = selected ? DisplayStyle.Flex : DisplayStyle.None;
            buttons[i].style.backgroundColor = selected
                ? (Color)new Color32(46, 139, 87, 255)
                : (Color)new Color32(241, 241, 241, 255);
            buttons[i].style.color = selected ? Color.white : Color.black;
            buttons[i].style.unityFontStyleAndWeight = selected ? FontStyle.Bold : FontStyle.Normal;
        }
    }

    private void UnbindTabs()
    {
        for (int i = 0; i < TabNames.Length; i++)
        {
            if (buttons[i] != null && clickHandlers[i] != null)
                buttons[i].clicked -= clickHandlers[i];

            buttons[i] = null;
            pages[i] = null;
            clickHandlers[i] = null;
        }
    }
}
