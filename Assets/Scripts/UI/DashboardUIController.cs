using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(PanelRenderer))]
public class DashboardUIController : MonoBehaviour
{
    private static readonly string[] TabNames =
    {
        "overview", "production", "fleet", "fuel", "operations"
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
    private Label kpiTitle;
    private DropdownField speedDropdown;
    private static readonly float[] PlaybackSpeeds = { 2.5f, 5f, 10f, 25f, 50f };
    private static readonly string[] PlaybackLabels = { "0.5", "1", "2", "5", "10" };
    private LineChart harvestChart;
    private LineChart fuelChart;
    private LineChart activeChart;
    private LineChart harvestRateChart, averageLoadChart, fuelRateChart, utilizationChart, detailedFuelChart, unloadingChart;
    private MultiLineChart productionChart;
    private MultiLineChart.Series harvestedSeries, remainingSeries;
    private BarChart harvesterLoadBars, fleetLoadBars, fuelRemainingBars;
    private ActivityTimelineChart activityTimeline;
    private readonly List<Vector2> harvestRateHistory = new(), averageLoadHistory = new(), fuelRateHistory = new(), utilizationHistory = new();
    private readonly List<Vector2> harvestedHistory = new(), remainingHistory = new();
    private readonly List<ActivityTimelineChart.Interval> activityIntervals = new();
    private readonly Dictionary<int, string> currentActivities = new();
    private readonly Dictionary<int, float> activityStarted = new();
    private float previousHarvested, previousFuel, previousMetricTime = -1;
    private int initialHarvestable;
    private double lastChartTime = -1;
    private RenderTexture cameraTexture;
    private VisualElement cameraDisplay;
    private Button startControl, pauseControl, resumeControl, restartControl;
    private Label connectionStatus;
    private Button configToggle, configApply;
    private VisualElement configPanel;
    private TextField widthField, heightField, harvesterField, cartField, obstacleField, stepsField;
    private Button overviewModeButton, buildingsModeButton, vehiclesModeButton;
    private VisualElement overviewContent, kpiContent, graphContent, buildingsPage, vehiclesPage, vehicleCards;
    private DropdownField vehicleSelector;
    private readonly Label[] kpiMetricLabels = new Label[5];
    private Label farmDimensions, farmArea, farmHarvestable, farmObstacles, farmUnload, farmRegions;
    private Label farmState, farmUsable, farmFleet, farmRegionSize, farmDensity, farmUnloadDistance;
    private VisualElement vehicleChartHost;
    private LineChart vehicleLoadChart, vehicleFuelChart, vehicleConsumedChart;
    private int selectedVehicleId = int.MinValue;
    private sealed class VehicleHistory
    {
        public string Type;
        public float Capacity;
        public float FuelCapacity;
        public readonly List<Vector2> Load = new List<Vector2>();
        public readonly List<Vector2> Fuel = new List<Vector2>();
        public readonly List<Vector2> Consumed = new List<Vector2>();
        public WebSocketController.AgentData Latest;
        public bool HasPosition;
        public Vector2Int LastPosition;
        public float DistanceMeters;
        public float ActiveSeconds;
        public float ObservedSeconds;
        public float LastSampleTime = -1;
    }
    private readonly Dictionary<int, VehicleHistory> vehicleHistories = new Dictionary<int, VehicleHistory>();
    private float metersPerCell = 5f;

    private void StartClicked() => webSocketController?.StartSimulation();
    private void PauseClicked() => webSocketController?.PauseSimulation();
    private void ResumeClicked() => webSocketController?.ResumeSimulation();
    private void RestartClicked() => webSocketController?.RestartSimulation();
    private void OverviewClicked() => SelectMode(0);
    private void BuildingsClicked() => SelectMode(1);
    private void VehiclesClicked() => SelectMode(2);

    private void RefreshControls()
    {
        string status = webSocketController != null ? webSocketController.Status : "Disconnected";
        bool ready = webSocketController != null && webSocketController.CanSend;
        if (connectionStatus != null) connectionStatus.text = $"Status: {status}";
        if (speedDropdown != null)
        {
            int index = Array.IndexOf(PlaybackSpeeds, webSocketController != null ? webSocketController.PlaybackSpeed : 5f);
            speedDropdown.SetValueWithoutNotify(PlaybackLabels[index >= 0 ? index : 1]);
            speedDropdown.SetEnabled(ready && status != "Waiting for server");
        }
        startControl?.SetEnabled(ready && status == "Ready");
        pauseControl?.SetEnabled(ready && status == "Running");
        resumeControl?.SetEnabled(ready && status == "Paused");
        restartControl?.SetEnabled(ready && (status == "Ready" || status == "Running" || status == "Paused" || status == "Completed" || status == "Command failed"));
    }

    //Se desuscriben los eventos de los botones y se limpian las referencias a los elementos de la UI.
    private void UnbindControls()
    {
        if (speedDropdown != null) speedDropdown.UnregisterValueChangedCallback(OnSpeedChanged);
        speedDropdown = null;
        if (startControl != null) startControl.clicked -= StartClicked;
        if (pauseControl != null) pauseControl.clicked -= PauseClicked;
        if (resumeControl != null) resumeControl.clicked -= ResumeClicked;
        if (restartControl != null) restartControl.clicked -= RestartClicked;
        if (configToggle != null) configToggle.clicked -= ToggleConfig;
        if (configApply != null) configApply.clicked -= ApplyConfig;
        if (overviewModeButton != null) overviewModeButton.clicked -= OverviewClicked;
        if (buildingsModeButton != null) buildingsModeButton.clicked -= BuildingsClicked;
        if (vehiclesModeButton != null) vehiclesModeButton.clicked -= VehiclesClicked;
        startControl = pauseControl = resumeControl = restartControl = null;
        connectionStatus = null;
        configToggle = configApply = null;
        configPanel = null;
        widthField = heightField = harvesterField = cartField = obstacleField = stepsField = null;
        overviewModeButton = buildingsModeButton = vehiclesModeButton = null;
        overviewContent = kpiContent = graphContent = buildingsPage = vehiclesPage = vehicleCards = null;
        farmDimensions = farmArea = farmHarvestable = farmObstacles = farmUnload = farmRegions = null;
        farmState = farmUsable = farmFleet = farmRegionSize = farmDensity = farmUnloadDistance = null;
        UnbindVehiclePanel();
    }
    private DropdownField cameraDropdown;
    private FieldCameraController fieldCameras;
    private VehicleSpawner vehicleSpawner;
    private RenderTexture vehicleCameraTexture;
    private int selectedMode;

    public void BindFieldCameras(FieldCameraController controller)
    {
        fieldCameras = controller;
        RefreshCameraDropdown();
    }

    public void UnbindFieldCameras(FieldCameraController controller)
    {
        if (fieldCameras != controller) return;
        fieldCameras = null;
        RefreshCameraDropdown();
    }

    private void RefreshCameraDropdown()
    {
        if (cameraDropdown == null) return;
        if (selectedMode == 2)
        {
            cameraDropdown.choices = new List<string> { "Vehicle" };
            cameraDropdown.SetValueWithoutNotify("Vehicle");
            cameraDropdown.SetEnabled(false);
            return;
        }
        cameraDropdown.choices = new System.Collections.Generic.List<string>(FieldCameraController.ViewNames);
        cameraDropdown.SetValueWithoutNotify(FieldCameraController.ViewNames[fieldCameras != null ? fieldCameras.SelectedView : 0]);
        cameraDropdown.SetEnabled(fieldCameras != null);
    }

    private void OnCameraChanged(ChangeEvent<string> evt)
    {
        if (fieldCameras != null)
            fieldCameras.SelectView(Array.IndexOf(FieldCameraController.ViewNames, evt.newValue));
    }

    public void SetCameraTexture(RenderTexture texture)
    {
        cameraTexture = texture;
        if (cameraDisplay != null)
            cameraDisplay.style.backgroundImage = texture != null
                ? new StyleBackground(Background.FromRenderTexture(texture))
                : new StyleBackground(StyleKeyword.None);
    }

    public void ClearCameraTexture(RenderTexture texture)
    {
        if (cameraTexture == texture) SetCameraTexture(null);
    }

    private void OnEnable()
    {
        if (webSocketController == null)
            webSocketController = GetComponent<WebSocketController>();
        if (webSocketController == null)
        {
            WebSocketController[] connections = FindObjectsByType<WebSocketController>();
            if (connections.Length == 1)
                webSocketController = connections[0];
        }
        VehicleSpawner[] spawners = FindObjectsByType<VehicleSpawner>();
        if (spawners.Length == 1) vehicleSpawner = spawners[0];
        if (webSocketController != null)
        {
            webSocketController.SimulationUpdated += UpdateKpis;
            webSocketController.StatusChanged += RefreshControls;
            Debug.Log($"Dashboard linked to WebSocketController on {webSocketController.gameObject.name}.", this);
        }
        else
            Debug.LogWarning("Assign the simulation's WebSocketController in the DashboardUIController Inspector; no unique controller was found.", this);

        panelRenderer = GetComponent<PanelRenderer>();
        panelRenderer.RegisterUIReloadCallback(OnUIReload);
    }

    private void OnDisable()
    {
        if (webSocketController != null) webSocketController.StatusChanged -= RefreshControls;
        UnbindControls();
        if (cameraDropdown != null) cameraDropdown.UnregisterValueChangedCallback(OnCameraChanged);
        cameraDropdown = null;
        cameraDisplay = null;
        if (webSocketController != null)
            webSocketController.SimulationUpdated -= UpdateKpis;
        vehicleSpawner?.HideVehicleCameras();
        fieldCameras?.SetRenderingEnabled(true);
        harvestPercentValue = wheatValue = fuelValue = vehiclesValue = timeValue = kpiTitle = null;
        for (int i = 0; i < kpiMetricLabels.Length; i++) kpiMetricLabels[i] = null;
        if (panelRenderer != null)
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);

        UnbindTabs();
    }

    private void OnDestroy()
    {
        if (vehicleCameraTexture == null) return;
        vehicleCameraTexture.Release();
        Destroy(vehicleCameraTexture);
        vehicleCameraTexture = null;
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement rootElement)
    {
        UnbindControls();
        startControl = rootElement.Q<Button>("simulation-start");
        pauseControl = rootElement.Q<Button>("simulation-pause");
        resumeControl = rootElement.Q<Button>("simulation-resume");
        restartControl = rootElement.Q<Button>("simulation-restart");
        connectionStatus = rootElement.Q<Label>("simulation-status");
        configToggle = rootElement.Q<Button>("config-toggle");
        configApply = rootElement.Q<Button>("config-apply");
        widthField = rootElement.Q<TextField>("config-width");
        heightField = rootElement.Q<TextField>("config-height");
        harvesterField = rootElement.Q<TextField>("config-harvesters");
        cartField = rootElement.Q<TextField>("config-carts");
        obstacleField = rootElement.Q<TextField>("config-obstacles");
        stepsField = rootElement.Q<TextField>("config-steps");
        overviewModeButton = rootElement.Q<Button>("overview_button");
        buildingsModeButton = rootElement.Q<Button>("field_button");
        vehiclesModeButton = rootElement.Q<Button>("vehicle_button");
        overviewContent = rootElement.Q<VisualElement>("bottom_container");
        kpiContent = rootElement.Q<VisualElement>("kti_container");
        graphContent = rootElement.Q<VisualElement>("graph_container");
        buildingsPage = rootElement.Q<VisualElement>("buildings_mode_page");
        vehiclesPage = rootElement.Q<VisualElement>("vehicles_mode_page");
        vehicleCards = rootElement.Q<VisualElement>("vehicle_cards");
        farmDimensions = rootElement.Q<Label>("farm-dimensions");
        farmArea = rootElement.Q<Label>("farm-area");
        farmHarvestable = rootElement.Q<Label>("farm-harvestable");
        farmObstacles = rootElement.Q<Label>("farm-obstacles");
        farmUnload = rootElement.Q<Label>("farm-unload");
        farmRegions = rootElement.Q<Label>("farm-regions");
        farmState = rootElement.Q<Label>("farm-state");
        farmUsable = rootElement.Q<Label>("farm-usable");
        farmFleet = rootElement.Q<Label>("farm-fleet");
        farmRegionSize = rootElement.Q<Label>("farm-region-size");
        farmDensity = rootElement.Q<Label>("farm-density");
        farmUnloadDistance = rootElement.Q<Label>("farm-unload-distance");
        SetupVehiclePanel();

        
        if (vehiclesPage != null && overviewContent != null && vehiclesPage.parent != overviewContent)
        {
            vehiclesPage.RemoveFromHierarchy();
            overviewContent.Add(vehiclesPage);
            vehiclesPage.style.width = new Length(50, LengthUnit.Percent);
            vehiclesPage.style.flexGrow = 0;
            vehiclesPage.style.flexShrink = 1;
            vehiclesPage.style.minWidth = 0;
            vehiclesPage.style.marginLeft = 10;
            vehiclesPage.style.marginRight = 0;
            vehiclesPage.style.marginTop = 0;
            vehiclesPage.style.marginBottom = 0;
        }
        if (buildingsPage != null && overviewContent != null && buildingsPage.parent != overviewContent)
        {
            buildingsPage.RemoveFromHierarchy();
            overviewContent.Add(buildingsPage);
            buildingsPage.style.width = new Length(50, LengthUnit.Percent);
            buildingsPage.style.flexGrow = 0;
            buildingsPage.style.flexShrink = 1;
            buildingsPage.style.minWidth = 0;
            buildingsPage.style.marginLeft = 10;
            buildingsPage.style.marginRight = buildingsPage.style.marginTop = buildingsPage.style.marginBottom = 0;
        }

        configPanel = rootElement.Q<VisualElement>("simulation-config");
        VisualElement mainContainer = rootElement.Q<VisualElement>("main_container");
        if (configPanel != null && mainContainer != null && configPanel.parent != mainContainer)
        {
            configPanel.RemoveFromHierarchy();
            mainContainer.Add(configPanel);
        }
        if (startControl != null) startControl.clicked += StartClicked;
        if (pauseControl != null) pauseControl.clicked += PauseClicked;
        if (resumeControl != null) resumeControl.clicked += ResumeClicked;
        if (restartControl != null) restartControl.clicked += RestartClicked;
        if (configToggle != null) configToggle.clicked += ToggleConfig;
        if (configApply != null) configApply.clicked += ApplyConfig;
        if (overviewModeButton != null) overviewModeButton.clicked += OverviewClicked;
        if (buildingsModeButton != null) buildingsModeButton.clicked += BuildingsClicked;
        if (vehiclesModeButton != null) vehiclesModeButton.clicked += VehiclesClicked;
        connectionStatus?.BringToFront();
        SetConfigVisible(false);
        SelectMode(0);
        RefreshControls();
        cameraDisplay = rootElement.Q<VisualElement>("camera");
        if (cameraTexture != null) SetCameraTexture(cameraTexture);
        if (cameraDropdown != null) cameraDropdown.UnregisterValueChangedCallback(OnCameraChanged);
        cameraDropdown = rootElement.Q<DropdownField>("camera-dropdown");
        if (cameraDropdown != null)
        {
            StyleDropdown(cameraDropdown);
            RefreshCameraDropdown();
            cameraDropdown.RegisterValueChangedCallback(OnCameraChanged);
        }
        UnbindTabs();
        SetupCharts(rootElement);
        SetupPlaybackControls(rootElement);
        harvestPercentValue = rootElement.Q<Label>("kti_harvest_value");
        wheatValue = rootElement.Q<Label>("kti_wheat_value");
        fuelValue = rootElement.Q<Label>("kti_fuel_value");
        vehiclesValue = rootElement.Q<Label>("kti_vehicle_value");
        timeValue = rootElement.Q<Label>("kti_time_value");
        kpiTitle = rootElement.Q<Label>("kpi-title");
        for (int i = 0; i < kpiMetricLabels.Length; i++)
            kpiMetricLabels[i] = rootElement.Q<Label>($"kpi-label-{i + 1}");
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

    private void OnSpeedChanged(ChangeEvent<string> evt)
    {
        int speedIndex = Array.IndexOf(PlaybackLabels, evt.newValue);
        if (speedIndex >= 0) webSocketController?.SetPlaybackSpeed(PlaybackSpeeds[speedIndex]);
    }

    private void SetupPlaybackControls(VisualElement root)
    {
        root.Q<VisualElement>("playback-controls")?.RemoveFromHierarchy();
        VisualElement toolbar = root.Q<VisualElement>("top_bar");
        if (toolbar == null) return;
        var controls = new VisualElement { name = "playback-controls" };
        controls.style.flexDirection = FlexDirection.Row;
        controls.style.alignItems = Align.Center;
        controls.style.marginRight = 12;
        controls.Add(new Label("Playback speed"));
        speedDropdown = new DropdownField();
        speedDropdown.choices = new List<string>(PlaybackLabels);
        speedDropdown.style.width = 104;
        speedDropdown.style.height = 38;
        speedDropdown.style.marginLeft = 6;
        speedDropdown.tooltip = "1 = 5 segundos simulados por segundo real, igual que la versión anterior.";
        StyleDropdown(speedDropdown);
        StylePlaybackDropdown(speedDropdown);
        speedDropdown.RegisterValueChangedCallback(OnSpeedChanged);
        controls.Add(speedDropdown);
        toolbar.Insert(0, controls);
        RefreshControls();
    }

    private void ToggleConfig()
    {
        if (configPanel == null) return;
        configPanel.style.display = configPanel.resolvedStyle.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
        configPanel.BringToFront();
    }

    private void SetConfigVisible(bool visible)
    {
        if (configPanel != null) configPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void ApplyConfig()
    {
        if (!int.TryParse(widthField?.value, out int width) || !int.TryParse(heightField?.value, out int height) ||
            !int.TryParse(harvesterField?.value, out int harvesters) || !int.TryParse(cartField?.value, out int carts) ||
            !int.TryParse(obstacleField?.value, out int obstacles) || !int.TryParse(stepsField?.value, out int steps))
        {
            Debug.LogWarning("Configuration values must be whole numbers.", this);
            return;
        }
        if (width < 1 || width > 20 || height < 1 || height > 20 || harvesters < 1 ||
            harvesters > width * height || carts < 1 || carts > 20 || obstacles < 0 ||
            obstacles >= width * height || steps < 1 || steps > 100000)
        {
            Debug.LogWarning("Configuration values are outside the allowed ranges.", this);
            return;
        }
        webSocketController?.ConfigureAndRestart(width, height, harvesters, carts, obstacles, steps);
        SetConfigVisible(false);
    }

    private void UpdateKpis(WebSocketController.SimulationMessage message)
    {
        UpdateCharts(message);
        UpdateVehicleAnalytics(message);
        UpdateFarmOverview(message);
        if (selectedMode == 2)
        {
            RefreshSelectedVehicle();
            return;
        }
        if (fuelValue != null) fuelValue.tooltip = "Combustible consumido (L)";
        if (harvestPercentValue != null) harvestPercentValue.tooltip = "Avance de la cosecha";
        bool hasStep = message != null && message.type == "simulation_step" && message.data != null;
        if (harvestPercentValue != null)
            harvestPercentValue.text = hasStep ? $"{message.data.harvest_progress:F1}%" : "--";
        if (wheatValue != null)
            wheatValue.text = hasStep ? $"{message.data.harvested_kg:F2} kg" : "--";
        if (fuelValue != null)
            fuelValue.text = hasStep ? $"{message.data.total_fuel_consumed:F2} L" : "--";
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

    private void SelectMode(int mode)
    {
        selectedMode = mode;
        if (overviewContent != null) overviewContent.style.display = DisplayStyle.Flex;
        if (kpiContent != null) kpiContent.style.display = DisplayStyle.Flex;
        if (graphContent != null) graphContent.style.display = mode == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        if (buildingsPage != null) buildingsPage.style.display = mode == 1 ? DisplayStyle.Flex : DisplayStyle.None;
        if (vehiclesPage != null) vehiclesPage.style.display = mode == 2 ? DisplayStyle.Flex : DisplayStyle.None;
        if (kpiTitle != null) kpiTitle.text = mode == 2 ? "Vehicle Overview" : "KPI Overview";
        SetKpiLabels(mode == 2);
        if (mode == 2) RefreshSelectedVehicle();
        else UpdateKpis(webSocketController != null ? webSocketController.LatestMessage : null);

        if (mode == 2) ShowSelectedVehicleCamera();
        else
        {
            vehicleSpawner?.HideVehicleCameras();
            fieldCameras?.SetRenderingEnabled(true);
            if (fieldCameras != null) fieldCameras.SelectView(fieldCameras.SelectedView);
        }
        RefreshCameraDropdown();

        Button[] modeButtons = { overviewModeButton, buildingsModeButton, vehiclesModeButton };
        for (int i = 0; i < modeButtons.Length; i++)
        {
            if (modeButtons[i] == null) continue;
            bool selected = i == mode;
            modeButtons[i].style.backgroundColor = selected
                ? (Color)new Color32(46, 139, 87, 255)
                : (Color)new Color32(241, 241, 241, 255);
            foreach (Label label in modeButtons[i].Query<Label>().ToList())
                label.style.color = selected ? Color.white : Color.black;
            foreach (Image image in modeButtons[i].Query<Image>().ToList())
                image.style.unityBackgroundImageTintColor = selected ? Color.white : Color.black;
        }
    }

    private void UpdateFarmOverview(WebSocketController.SimulationMessage message)
    {
        var field = message?.world?.field;
        if (field == null) return;
        float cellMeters = message.simulation != null && message.simulation.meters_per_cell > 0
            ? message.simulation.meters_per_cell : metersPerCell;
        int obstacles = message.obstacles?.Length ?? 0;
        if (farmDimensions != null) farmDimensions.text = $"Dimensions: {field.width} × {field.height} cells";
        if (farmArea != null) farmArea.text = $"Area: {field.width * field.height * cellMeters * cellMeters:0} m²";
        if (farmHarvestable != null) farmHarvestable.text = $"Harvestable cells: {Mathf.Max(0, field.width * field.height - obstacles)}";
        if (farmObstacles != null) farmObstacles.text = $"Obstacles: {obstacles}";
        if (farmUnload != null) farmUnload.text = message.world.unload_point != null
            ? $"Unload point: ({message.world.unload_point.x}, {message.world.unload_point.y})" : "Unload point: --";
        int regions = message.regions?.Length ?? 0;
        if (farmRegions != null) farmRegions.text = $"Regions: {regions}";
        int totalCells = field.width * field.height;
        int harvestable = Mathf.Max(0, totalCells - obstacles);
        int harvesters = 0, carts = 0;
        foreach (var agent in message.agents ?? Array.Empty<WebSocketController.AgentData>())
            if (agent != null && agent.type == "harvester") harvesters++; else if (agent != null) carts++;
        string status = webSocketController != null ? webSocketController.Status : message.status ?? "--";
        if (farmState != null) farmState.text = $"Status: {status}";
        if (farmUsable != null) farmUsable.text = $"Harvestable: {(totalCells > 0 ? harvestable * 100f / totalCells : 0):F1}%";
        if (farmFleet != null) farmFleet.text = $"Fleet: {harvesters} harvesters · {carts} carts";
        if (farmRegionSize != null) farmRegionSize.text = $"Average region: {(regions > 0 ? harvestable / (float)regions : 0):F1} cells";
        if (farmDensity != null) farmDensity.text = $"Obstacle density: {(totalCells > 0 ? obstacles * 100f / totalCells : 0):F1}%";
        if (farmUnloadDistance != null)
        {
            float distance = 0;
            if (message.world.unload_point != null)
            {
                float centerX = field.x + (field.width - 1) * .5f;
                float centerY = field.y + (field.height - 1) * .5f;
                distance = Vector2.Distance(new Vector2(centerX, centerY),
                    new Vector2(message.world.unload_point.x, message.world.unload_point.y)) * cellMeters;
            }
            farmUnloadDistance.text = $"Unload distance: {distance:F0} m";
        }
    }

    private void SetKpiLabels(bool vehicleMode)
    {
        string[] overview = { "Harvested %", "Harvested Wheat", "Fuel Consumed", "Total Vehicles", "Simulation Time" };
        string[] vehicle = { "Status", "Position", "Load", "Distance Travelled", "Utilization" };
        for (int i = 0; i < kpiMetricLabels.Length; i++)
            if (kpiMetricLabels[i] != null) kpiMetricLabels[i].text = vehicleMode ? vehicle[i] : overview[i];
        if (!vehicleMode && harvestPercentValue != null)
            harvestPercentValue.style.color = (Color)new Color32(46, 139, 87, 255);
    }

    private void SetupVehiclePanel()
    {
        if (vehicleCards == null) return;
        vehicleCards.Clear();
        vehicleSelector = new DropdownField("Vehicle");
        vehicleSelector.style.height = 42;
        vehicleSelector.style.marginBottom = 10;
        StyleDropdown(vehicleSelector);
        vehicleSelector.RegisterValueChangedCallback(OnVehicleSelected);
        vehicleCards.Add(vehicleSelector);
        vehicleChartHost = new VisualElement();
        vehicleChartHost.style.flexGrow = 1;
        vehicleChartHost.style.minHeight = 420;
        vehicleChartHost.style.flexDirection = FlexDirection.Column;
        vehicleCards.Add(vehicleChartHost);

        vehicleLoadChart = CreateVehicleChart("Vehicle Load", "Load (%)");
        vehicleLoadChart.YAxis.Min = 0;
        vehicleLoadChart.YAxis.Max = 100;
        vehicleLoadChart.LineColor = new Color(0.18f, 0.55f, 0.34f);
        vehicleFuelChart = CreateVehicleChart("Fuel Level", "Fuel remaining (%)");
        vehicleFuelChart.YAxis.Min = 0;
        vehicleFuelChart.YAxis.Max = 100;
        vehicleFuelChart.LineColor = new Color(0.13f, 0.45f, 0.72f);
        vehicleConsumedChart = CreateVehicleChart("Fuel Used", "Cumulative fuel used (L)");
        vehicleConsumedChart.YAxis.Min = 0;
        vehicleConsumedChart.LineColor = new Color(0.88f, 0.52f, 0.13f);
        vehicleChartHost.Add(vehicleLoadChart);
        vehicleChartHost.Add(vehicleFuelChart);
        vehicleChartHost.Add(vehicleConsumedChart);
    }

    private LineChart CreateVehicleChart(string titleText, string yTitle)
    {
        var chart = new LineChart { Title = titleText };
        chart.XAxis.Title = "Simulation time (s)";
        chart.XAxis.Min = 0;
        chart.YAxis.Title = yTitle;
        return chart;
    }

    private void UnbindVehiclePanel()
    {
        if (vehicleSelector != null) vehicleSelector.UnregisterValueChangedCallback(OnVehicleSelected);
        vehicleSelector = null;
        vehicleChartHost = null;
        vehicleLoadChart = vehicleFuelChart = vehicleConsumedChart = null;
    }

    private void OnVehicleSelected(ChangeEvent<string> evt)
    {
        foreach (var pair in vehicleHistories)
            if (VehicleName(pair.Key, pair.Value.Type) == evt.newValue)
            {
                selectedVehicleId = pair.Key;
                RefreshSelectedVehicle();
                ShowSelectedVehicleCamera();
                return;
            }
    }

    private void UpdateVehicleAnalytics(WebSocketController.SimulationMessage message)
    {
        if (message?.agents == null) return;
        if (message.type == "simulation_init")
        {
            vehicleHistories.Clear();
            selectedVehicleId = int.MinValue;
            metersPerCell = message.simulation != null && message.simulation.meters_per_cell > 0
                ? message.simulation.meters_per_cell : 5f;
        }

        foreach (var agent in message.agents)
        {
            if (agent == null) continue;
            if (!vehicleHistories.TryGetValue(agent.id, out VehicleHistory history))
            {
                history = new VehicleHistory { Type = agent.type };
                vehicleHistories.Add(agent.id, history);
            }
            history.Type = agent.type;
            if (agent.capacity > 0) history.Capacity = agent.capacity;
            if (agent.fuel_capacity > 0) history.FuelCapacity = agent.fuel_capacity;
            if (message.type == "simulation_step")
            {
                float time = (float)message.simulation_time;
                if (history.LastSampleTime >= 0 && time > history.LastSampleTime)
                {
                    float elapsed = time - history.LastSampleTime;
                    history.ObservedSeconds += elapsed;
                    if (agent.active || agent.harvesting || agent.going_to_unload) history.ActiveSeconds += elapsed;

                }
                if (agent.position != null)
                {
                    history.LastPosition = new Vector2Int(agent.position.x, agent.position.y);
                    history.HasPosition = true;
                }
                history.DistanceMeters = agent.distance_m;
                history.LastSampleTime = time;
                AddHistoryPoint(history.Load, time, history.Capacity > 0 ? agent.load * 100f / history.Capacity : 0);
                AddHistoryPoint(history.Fuel, time, history.FuelCapacity > 0 ? agent.fuel * 100f / history.FuelCapacity : 0);
                AddHistoryPoint(history.Consumed, time, agent.fuel_consumed);
            }
            history.Latest = agent;
        }

        if (vehicleSelector != null)
        {
            var choices = new List<string>();
            foreach (var pair in vehicleHistories) choices.Add(VehicleName(pair.Key, pair.Value.Type));
            choices.Sort(StringComparer.Ordinal);
            vehicleSelector.choices = choices;
            if (!vehicleHistories.ContainsKey(selectedVehicleId) && vehicleHistories.Count > 0)
                foreach (var pair in vehicleHistories) { selectedVehicleId = pair.Key; break; }
            if (vehicleHistories.TryGetValue(selectedVehicleId, out VehicleHistory selected))
                vehicleSelector.SetValueWithoutNotify(VehicleName(selectedVehicleId, selected.Type));
        }
        RefreshSelectedVehicle();
        if (selectedMode == 2) ShowSelectedVehicleCamera();
    }

    private void ShowSelectedVehicleCamera()
    {
        if (selectedMode != 2) return;
        if (vehicleSpawner == null)
        {
            VehicleSpawner[] spawners = FindObjectsByType<VehicleSpawner>();
            if (spawners.Length == 1) vehicleSpawner = spawners[0];
        }
        if (vehicleCameraTexture == null)
        {
            vehicleCameraTexture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32)
            {
                name = "Selected Vehicle Camera",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            vehicleCameraTexture.Create();
        }
        fieldCameras?.SetRenderingEnabled(false);
        if (vehicleSpawner != null && vehicleSpawner.ShowVehicleCamera(selectedVehicleId, vehicleCameraTexture))
            SetCameraTexture(vehicleCameraTexture);
    }

    private static void StyleDropdown(DropdownField dropdown)
    {
        dropdown.style.color = (Color)new Color32(31, 67, 38, 255);
        dropdown.style.unityFontStyleAndWeight = FontStyle.Bold;
        dropdown.style.backgroundColor = Color.white;
        VisualElement input = dropdown.Q<VisualElement>(className: "unity-base-popup-field__input");
        if (input == null) input = dropdown.Q<VisualElement>(className: "unity-base-field__input");
        if (input == null) return;
        input.style.backgroundColor = Color.white;
        input.style.borderLeftWidth = input.style.borderRightWidth = 1;
        input.style.borderTopWidth = input.style.borderBottomWidth = 1;
        input.style.borderLeftColor = input.style.borderRightColor = (Color)new Color32(177, 196, 174, 255);
        input.style.borderTopColor = input.style.borderBottomColor = (Color)new Color32(177, 196, 174, 255);
        input.style.borderTopLeftRadius = input.style.borderTopRightRadius = 7;
        input.style.borderBottomLeftRadius = input.style.borderBottomRightRadius = 7;
        input.style.flexDirection = FlexDirection.Row;

        Label valueText = dropdown.Q<Label>(className: "unity-base-popup-field__text");
        if (valueText != null)
        {
            valueText.style.flexGrow = 1;
            valueText.style.flexShrink = 1;
            valueText.style.minWidth = 32;
            valueText.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        VisualElement arrow = dropdown.Q<VisualElement>(className: "unity-base-popup-field__arrow");
        if (arrow != null)
        {
            arrow.style.flexGrow = 0;
            arrow.style.flexShrink = 0;
            arrow.style.width = 14;
        }
    }

    private static void StylePlaybackDropdown(DropdownField dropdown)
    {
        Color green = (Color)new Color32(29, 86, 43, 255);
        dropdown.style.backgroundColor = green;
        dropdown.style.color = Color.white;

        VisualElement input = dropdown.Q<VisualElement>(className: "unity-base-popup-field__input");
        if (input == null) input = dropdown.Q<VisualElement>(className: "unity-base-field__input");
        if (input != null)
        {
            input.style.backgroundColor = green;
            input.style.borderLeftColor = input.style.borderRightColor = green;
            input.style.borderTopColor = input.style.borderBottomColor = green;
        }

        Label valueText = dropdown.Q<Label>(className: "unity-base-popup-field__text");
        if (valueText != null) valueText.style.color = Color.white;

        VisualElement arrow = dropdown.Q<VisualElement>(className: "unity-base-popup-field__arrow");
        if (arrow != null) arrow.style.unityBackgroundImageTintColor = Color.white;
    }

    private static void AddHistoryPoint(List<Vector2> points, float x, float y)
    {
        if (points.Count > 0 && points[points.Count - 1].x == x) points[points.Count - 1] = new Vector2(x, y);
        else points.Add(new Vector2(x, y));
        if (points.Count > 2000) points.RemoveAt(0);
    }

    private void RefreshSelectedVehicle()
    {
        if (!vehicleHistories.TryGetValue(selectedVehicleId, out VehicleHistory history)) return;
        var agent = history.Latest;
        if (agent != null)
        {
            string telemetry = VehicleBehavior.TelemetryText(agent);
            if (fuelValue != null) fuelValue.tooltip = telemetry;
            if (harvestPercentValue != null) harvestPercentValue.tooltip = telemetry;
            string position = agent.position != null ? $"({agent.position.x}, {agent.position.y})" : "Waiting";
            string activity = agent.harvesting ? "Harvesting" : agent.going_to_unload ? "Unloading" : agent.active ? "Moving" : "Idle";
            if (harvestPercentValue != null)
            {
                harvestPercentValue.text = activity;
                harvestPercentValue.style.color = activity == "Harvesting" ? (Color)new Color32(34, 139, 73, 255)
                    : activity == "Moving" ? (Color)new Color32(36, 112, 181, 255)
                    : activity == "Unloading" ? (Color)new Color32(214, 126, 27, 255)
                    : (Color)new Color32(105, 109, 105, 255);
            }
            if (wheatValue != null) wheatValue.text = position;
            if (fuelValue != null) fuelValue.text = $"{agent.load:F2} / {history.Capacity:F2} kg";
            if (vehiclesValue != null) vehiclesValue.text = history.DistanceMeters >= 1000
                ? $"{history.DistanceMeters / 1000f:F1} km" : $"{history.DistanceMeters:F0} m";
            if (timeValue != null) timeValue.text = history.ObservedSeconds > 0
                ? $"{history.ActiveSeconds * 100f / history.ObservedSeconds:F0}%" : "0%";
        }
        FillChart(vehicleLoadChart, history.Load);
        FillChart(vehicleFuelChart, history.Fuel);
        FillChart(vehicleConsumedChart, history.Consumed);
        vehicleLoadChart?.Refresh();
        vehicleFuelChart?.Refresh();
        vehicleConsumedChart?.Refresh();
    }

    private static void FillChart(LineChart chart, List<Vector2> points)
    {
        if (chart == null) return;
        chart.SetData(points);
    }

    private static string VehicleName(int id, string type) => $"{(type == "grain_cart" ? "Grain Cart" : "Harvester")} {id}";

    private void SetupCharts(VisualElement root)
    {
        if (harvestChart == null)
        {
            harvestChart = new LineChart { Title = "Harvest Progress" };
            harvestChart.YAxis.Title = "Harvested (%)";
            harvestChart.YAxis.Min = 0;
            harvestChart.YAxis.Max = 100;
            fuelChart = new LineChart { Title = "Fuel Consumption", LineColor = new Color(0.8f, 0.49f, 0.1f) };
            fuelChart.YAxis.Title = "Cumulative fuel consumed (L)";
            fuelChart.YAxis.Min = 0;
            activeChart = new LineChart { Title = "Active Vehicles", LineColor = new Color(0.15f, 0.45f, 0.8f) };
            activeChart.YAxis.Title = "Vehicle count";
            activeChart.YAxis.Min = 0;
            activeChart.YAxis.Format = value => value.ToString("0");
            harvestRateChart = MetricLine("Harvest Rate", "Cells / minute", new Color(.34f, .64f, .2f));
            averageLoadChart = MetricLine("Average Fleet Load", "Load (%)", new Color(.18f, .52f, .72f));
            averageLoadChart.YAxis.Max = 100;
            fuelRateChart = MetricLine("Fuel Consumption Rate", "L / minute", new Color(.9f, .48f, .12f));
            utilizationChart = MetricLine("Fleet Utilization", "Active (%)", new Color(.35f, .61f, .28f));
            utilizationChart.YAxis.Max = 100;
            detailedFuelChart = MetricLine("Total Fuel Consumed", "Cumulative fuel used (L)", new Color(.8f, .49f, .1f));
            unloadingChart = MetricLine("Vehicles Unloading", "Vehicle count", new Color(.65f, .38f, .75f));
            unloadingChart.YAxis.Format = value => value.ToString("0");
            foreach (LineChart chart in new[] { harvestChart, fuelChart, activeChart, harvestRateChart, averageLoadChart, fuelRateChart, utilizationChart, detailedFuelChart, unloadingChart })
            {
                chart.XAxis.Title = "Simulation time (s)";
                chart.XAxis.Min = 0;
            }

            productionChart = new MultiLineChart { Title = "Harvested vs Remaining Wheat" };
            productionChart.XAxis.Title = "Simulation time (s)";
            productionChart.YAxis.Title = "Cells";
            harvestedSeries = productionChart.AddSeries("Harvested", new Color(.2f, .58f, .3f));
            remainingSeries = productionChart.AddSeries("Remaining", new Color(.78f, .65f, .18f));
            harvesterLoadBars = new BarChart { Title = "Harvester Load", Horizontal = true };
            fleetLoadBars = new BarChart { Title = "Current Vehicle Load", Horizontal = true };
            fuelRemainingBars = new BarChart { Title = "Fuel Remaining by Vehicle", Horizontal = true };
            harvesterLoadBars.XAxis.Max = fleetLoadBars.XAxis.Max = fuelRemainingBars.XAxis.Max = 100;
            harvesterLoadBars.XAxis.Title = fleetLoadBars.XAxis.Title = fuelRemainingBars.XAxis.Title = "Percent";
            activityTimeline = new ActivityTimelineChart { Title = "Vehicle Activity Timeline" };
            activityTimeline.XAxis.Title = "Simulation time (s)";
        }
        CreateChartPage(root.Q<VisualElement>("graph_overview_page"), harvestChart, fuelChart, activeChart);
        CreateChartPage(root.Q<VisualElement>("graph_production_page"), productionChart, harvestRateChart, harvesterLoadBars);
        CreateChartPage(root.Q<VisualElement>("graph_fleet_page"), activityTimeline, averageLoadChart, fleetLoadBars);
        CreateChartPage(root.Q<VisualElement>("graph_fuel_page"), detailedFuelChart, fuelRateChart, fuelRemainingBars);
        CreateChartPage(root.Q<VisualElement>("graph_operations_page"), utilizationChart, unloadingChart);
    }

    private static LineChart MetricLine(string title, string yTitle, Color color)
    {
        var chart = new LineChart { Title = title, LineColor = color };
        chart.YAxis.Title = yTitle;
        chart.YAxis.Min = 0;
        return chart;
    }

    private static void CreateChartPage(VisualElement page, params VisualElement[] charts)
    {
        if (page == null) return;
        page.Clear();
        page.style.flexDirection = FlexDirection.Column;
        page.style.minHeight = 0;
        if (charts.Length == 0) return;
        charts[0].RemoveFromHierarchy();
        charts[0].style.flexGrow = 1;
        page.Add(charts[0]);
        if (charts.Length == 1) return;
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexGrow = 1;
        row.style.minHeight = 0;
        row.style.marginTop = 8;
        for (int i = 1; i < charts.Length; i++)
        {
            charts[i].RemoveFromHierarchy();
            charts[i].style.flexBasis = 0;
            charts[i].style.flexGrow = 1;
            charts[i].style.minWidth = 0;
            if (i > 1) charts[i].style.marginLeft = 8;
            row.Add(charts[i]);
        }
        page.Add(row);
    }

    private void UpdateCharts(WebSocketController.SimulationMessage message)
    {
        if (harvestChart == null || message == null) return;
        if (message.type == "simulation_init" || message.simulation_time < lastChartTime)
        {
            ResetAnalytics(message);
        }
        int total = message.agents?.Length ?? 0;
        activeChart.YAxis.Max = Math.Max(1, total);
        activeChart.YAxis.TickCount = Math.Max(2, total + 1);
        unloadingChart.YAxis.Max = Math.Max(1, total);
        unloadingChart.YAxis.TickCount = Math.Max(2, total + 1);
        activeChart.Refresh();
        unloadingChart.Refresh();
        if (message.type != "simulation_step" || message.data == null || message.simulation_time == lastChartTime) return;
        float time = (float)message.simulation_time;
        int[] states = new int[4];
        int active = 0;
        float loadTotal = 0;
        int loadCount = 0;
        var allLoadBars = new List<BarChart.Bar>();
        var harvesterBars = new List<BarChart.Bar>();
        var fuelBars = new List<BarChart.Bar>();
        foreach (var agent in message.agents ?? Array.Empty<WebSocketController.AgentData>())
        {
            if (agent == null) continue;
            int state = agent.harvesting ? 0 : agent.going_to_unload ? 2 : agent.active ? 1 : 3;
            states[state]++;
            if (agent.active || agent.harvesting || agent.going_to_unload) active++;
            float loadPercent = agent.capacity > 0 ? agent.load * 100f / agent.capacity : 0;
            loadTotal += loadPercent; loadCount++;
            Color typeColor = agent.type == "harvester" ? new Color(.2f, .58f, .3f) : new Color(.18f, .48f, .78f);
            var loadBar = new BarChart.Bar { Label = VehicleName(agent.id, agent.type), Value = loadPercent, Color = typeColor };
            allLoadBars.Add(loadBar);
            if (agent.type == "harvester") harvesterBars.Add(loadBar);
            float fuelCapacity = agent.fuel_capacity;
            if (fuelCapacity <= 0 && vehicleHistories.TryGetValue(agent.id, out VehicleHistory knownVehicle))
                fuelCapacity = knownVehicle.FuelCapacity;
            float fuelPercent = fuelCapacity > 0 ? agent.fuel * 100f / fuelCapacity : 0;
            fuelBars.Add(new BarChart.Bar { Label = VehicleName(agent.id, agent.type), Value = fuelPercent, Color = typeColor });
            UpdateActivity(agent, time, state);
        }
        harvestChart.AddPoint(time, message.data.harvest_progress);
        fuelChart.AddPoint(time, message.data.total_fuel_consumed);
        detailedFuelChart.AddPoint(time, message.data.total_fuel_consumed);
        activeChart.AddPoint(time, active);
        unloadingChart.AddPoint(time, states[2]);
        float elapsed = previousMetricTime >= 0 ? time - previousMetricTime : 0;
        float harvestRate = elapsed > 0 ? (message.data.harvested - previousHarvested) * 60f / elapsed : 0;
        float fuelRate = elapsed > 0 ? (message.data.total_fuel_consumed - previousFuel) * 60f / elapsed : 0;
        harvestRateHistory.Add(new(time, Mathf.Max(0, harvestRate)));
        fuelRateHistory.Add(new(time, Mathf.Max(0, fuelRate)));
        averageLoadHistory.Add(new(time, loadCount > 0 ? loadTotal / loadCount : 0));
        utilizationHistory.Add(new(time, total > 0 ? active * 100f / total : 0));
        harvestedHistory.Add(new(time, message.data.harvested));
        remainingHistory.Add(new(time, Mathf.Max(0, initialHarvestable - message.data.harvested)));
        harvestRateChart.SetData(harvestRateHistory);
        fuelRateChart.SetData(fuelRateHistory);
        averageLoadChart.SetData(averageLoadHistory);
        utilizationChart.SetData(utilizationHistory);
        productionChart.SetData(harvestedSeries, harvestedHistory);
        productionChart.SetData(remainingSeries, remainingHistory);
        harvesterLoadBars.SetData(harvesterBars);
        fleetLoadBars.SetData(allLoadBars);
        fuelRemainingBars.SetData(fuelBars);
        RefreshTimeline(message.agents, time);
        previousHarvested = message.data.harvested;
        previousFuel = message.data.total_fuel_consumed;
        previousMetricTime = time;
        lastChartTime = message.simulation_time;
    }

    private void ResetAnalytics(WebSocketController.SimulationMessage message)
    {
        harvestChart.ClearData(); fuelChart.ClearData(); activeChart.ClearData(); detailedFuelChart.ClearData(); unloadingChart.ClearData();
        harvestRateChart.ClearData(); averageLoadChart.ClearData(); fuelRateChart.ClearData(); utilizationChart.ClearData();
        harvestRateHistory.Clear(); averageLoadHistory.Clear(); fuelRateHistory.Clear(); utilizationHistory.Clear();
        harvestedHistory.Clear(); remainingHistory.Clear();
        productionChart.SetData(harvestedSeries, harvestedHistory);
        productionChart.SetData(remainingSeries, remainingHistory);
        harvesterLoadBars.SetData(Array.Empty<BarChart.Bar>());
        fleetLoadBars.SetData(Array.Empty<BarChart.Bar>());
        fuelRemainingBars.SetData(Array.Empty<BarChart.Bar>());
        activityTimeline.SetData(Array.Empty<ActivityTimelineChart.Interval>());
        activityIntervals.Clear(); currentActivities.Clear(); activityStarted.Clear();
        initialHarvestable = message.world?.field != null
            ? Mathf.Max(0, message.world.field.width * message.world.field.height - (message.obstacles?.Length ?? 0))
            : 0;
        previousHarvested = previousFuel = 0;
        previousMetricTime = -1;
        lastChartTime = -1;
    }

    private void UpdateActivity(WebSocketController.AgentData agent, float time, int state)
    {
        string[] names = { "Harvesting", "Moving", "Unloading", "Idle" };
        Color[] colors = { new(.2f, .62f, .3f), new(.18f, .48f, .78f), new(.92f, .58f, .12f), new(.55f, .57f, .55f) };
        string activity = names[state];
        if (!currentActivities.TryGetValue(agent.id, out string previous))
        {
            currentActivities[agent.id] = activity;
            activityStarted[agent.id] = time;
            return;
        }
        if (previous == activity) return;
        int previousIndex = Array.IndexOf(names, previous);
        activityIntervals.Add(new ActivityTimelineChart.Interval
        {
            Row = VehicleName(agent.id, agent.type), Activity = previous,
            Start = activityStarted[agent.id], End = time,
            Color = colors[Mathf.Max(0, previousIndex)]
        });
        currentActivities[agent.id] = activity;
        activityStarted[agent.id] = time;
    }

    private void RefreshTimeline(WebSocketController.AgentData[] agents, float time)
    {
        string[] names = { "Harvesting", "Moving", "Unloading", "Idle" };
        Color[] colors = { new(.2f, .62f, .3f), new(.18f, .48f, .78f), new(.92f, .58f, .12f), new(.55f, .57f, .55f) };
        var visible = new List<ActivityTimelineChart.Interval>(activityIntervals);
        foreach (var agent in agents ?? Array.Empty<WebSocketController.AgentData>())
        {
            if (agent == null || !currentActivities.TryGetValue(agent.id, out string activity)) continue;
            int index = Mathf.Max(0, Array.IndexOf(names, activity));
            visible.Add(new ActivityTimelineChart.Interval
            {
                Row = VehicleName(agent.id, agent.type), Activity = activity,
                Start = activityStarted[agent.id], End = Mathf.Max(time, activityStarted[agent.id] + .001f), Color = colors[index]
            });
        }
        activityTimeline.SetData(visible);
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
