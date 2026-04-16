using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwareMonitor.Models;
using HardwareMonitor.Services;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace HardwareMonitor.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly HardwareMonitorService _service;
    private Timer? _timer;
    private bool _disposed;

    [ObservableProperty] private string _currentView = "Dashboard";
    [ObservableProperty] private ObservableCollection<HardwareNode> _hardwareTree = new();
    [ObservableProperty] private HardwareNode? _selectedHardware;
    [ObservableProperty] private int _updateInterval = 1000;
    [ObservableProperty] private string _statusText = "Initializing...";

    // Dashboard summary
    [ObservableProperty] private string _cpuTempText = "—";
    [ObservableProperty] private string _cpuLoadText = "—";
    [ObservableProperty] private string _cpuPowerText = "—";
    [ObservableProperty] private string _cpuClockText = "—";
    [ObservableProperty] private string _gpuTempText = "—";
    [ObservableProperty] private string _gpuLoadText = "—";
    [ObservableProperty] private string _gpuPowerText = "—";
    [ObservableProperty] private string _gpuMemText = "—";
    [ObservableProperty] private string _gpuClockText = "—";
    [ObservableProperty] private string _gpuFanText = "—";
    [ObservableProperty] private string _memLoadText = "—";
    [ObservableProperty] private string _memUsedText = "—";
    [ObservableProperty] private string _batteryText = "—";

    [ObservableProperty] private double _cpuLoadValue;
    [ObservableProperty] private double _gpuLoadValue;
    [ObservableProperty] private double _memLoadValue;
    [ObservableProperty] private double _gpuMemValue;
    [ObservableProperty] private double _batteryValue;

    // Settings toggles
    [ObservableProperty] private bool _cpuEnabled = true;
    [ObservableProperty] private bool _gpuEnabled = true;
    [ObservableProperty] private bool _memoryEnabled = true;
    [ObservableProperty] private bool _motherboardEnabled = true;
    [ObservableProperty] private bool _storageEnabled = true;
    [ObservableProperty] private bool _networkEnabled = true;
    [ObservableProperty] private bool _batteryEnabled = true;
    [ObservableProperty] private bool _controllerEnabled = true;
    [ObservableProperty] private bool _psuEnabled = true;

    // Detail view - generic sensor table
    [ObservableProperty] private ObservableCollection<SensorReading> _selectedSensors = new();
    [ObservableProperty] private string _selectedHardwareName = "";
    [ObservableProperty] private string _selectedHardwareType = "";

    // CPU-specific detail: per-core load bars
    [ObservableProperty] private ObservableCollection<CoreLoadItem> _cpuCoreLoads = new();
    [ObservableProperty] private string _cpuDetailName = "";
    [ObservableProperty] private string _cpuDetailTemp = "—";
    [ObservableProperty] private string _cpuDetailPower = "—";
    [ObservableProperty] private string _cpuDetailClock = "—";
    [ObservableProperty] private string _cpuDetailLoad = "—";
    // Multi-CPU switching
    private List<HardwareNode> _cpuNodes = new();
    [ObservableProperty] private int _cpuSelectedIndex;
    [ObservableProperty] private bool _cpuHasMultiple;
    [ObservableProperty] private string _cpuSwitchLabel = "";

    // GPU-specific detail
    [ObservableProperty] private string _gpuDetailName = "";
    [ObservableProperty] private string _gpuDetailTemp = "—";
    [ObservableProperty] private string _gpuDetailMemJunction = "—";
    [ObservableProperty] private string _gpuDetailPower = "—";
    [ObservableProperty] private string _gpuDetailClock = "—";
    [ObservableProperty] private string _gpuDetailMemClock = "—";
    [ObservableProperty] private string _gpuDetailVram = "—";
    [ObservableProperty] private string _gpuDetailFan = "—";
    [ObservableProperty] private string _gpuDetailVoltage = "—";
    [ObservableProperty] private double _gpuDetailLoadValue;
    [ObservableProperty] private double _gpuDetailMemValue;
    [ObservableProperty] private double _gpuDetailPowerPercent;
    // Multi-GPU switching
    private List<HardwareNode> _gpuNodes = new();
    [ObservableProperty] private int _gpuSelectedIndex;
    [ObservableProperty] private bool _gpuHasMultiple;
    [ObservableProperty] private string _gpuSwitchLabel = "";

    // Storage items
    [ObservableProperty] private ObservableCollection<StorageItem> _storageItems = new();

    // Network items
    [ObservableProperty] private ObservableCollection<NetworkItem> _networkItems = new();

    // Memory DIMM items
    [ObservableProperty] private ObservableCollection<DimmItem> _dimmItems = new();
    [ObservableProperty] private string _memDetailUsed = "—";
    [ObservableProperty] private string _memDetailAvailable = "—";
    [ObservableProperty] private double _memDetailLoad;

    // Motherboard detail items
    [ObservableProperty] private ObservableCollection<MbFanItem> _mbFanItems = new();
    [ObservableProperty] private ObservableCollection<MbTempItem> _mbTempItems = new();
    [ObservableProperty] private ObservableCollection<MbVoltageItem> _mbVoltageItems = new();
    [ObservableProperty] private bool _mbShowEmpty = true;
    [ObservableProperty] private bool _mbHasData;

    // Generic detail empty state
    [ObservableProperty] private bool _detailShowEmpty = true;
    [ObservableProperty] private bool _detailHasData;

    // Sidebar collapse
    [ObservableProperty] private bool _isSidebarCollapsed;
    [ObservableProperty] private double _sidebarWidth = 240;

    // ═══ System Tray & Auto-Start ═══
    [ObservableProperty] private bool _autoStartEnabled;
    [ObservableProperty] private bool _minimizeToTray = true;
    [ObservableProperty] private bool _closeToTray = true;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private string _trayIconMode = "CpuTemp";

    public static string[] TrayIconModeOptions { get; } = ["AppIcon", "CpuTemp", "GpuTemp", "CpuLoad", "GpuLoad"];

    // Monitor page: time-series chart data
    private const int MaxHistory = 120; // 2 minutes at 1s interval
    private const double EmaAlpha = 0.3; // EMA smoothing factor (lower = smoother)
    private readonly ObservableCollection<ObservablePoint> _cpuLoadHistory = new();
    private readonly ObservableCollection<ObservablePoint> _gpuLoadHistory = new();
    private readonly ObservableCollection<ObservablePoint> _cpuPowerHistory = new();
    private readonly ObservableCollection<ObservablePoint> _gpuPowerHistory = new();
    private int _historyTick;
    // EMA state for fixed series
    private double _emaCpuLoad, _emaGpuLoad, _emaCpuPower, _emaGpuPower;
    private bool _emaInitialized;

    // Dynamic per-device temperature histories
    private readonly Dictionary<string, ObservableCollection<ObservablePoint>> _tempHistories = new();
    private readonly Dictionary<string, double> _tempEma = new();
    // Dynamic per-fan speed histories
    private readonly Dictionary<string, ObservableCollection<ObservablePoint>> _fanHistories = new();
    private readonly Dictionary<string, double> _fanEma = new();

    // Legend items for custom legends
    [ObservableProperty] private ObservableCollection<LegendItem> _tempLegendItems = new();
    [ObservableProperty] private ObservableCollection<LegendItem> _fanLegendItems = new();

    private static readonly SKColor[] SeriesColors =
    {
        new(0, 255, 236),    // teal
        new(141, 89, 207),   // purple
        new(255, 164, 44),   // orange
        new(76, 175, 80),    // green
        new(255, 82, 82),    // red
        new(33, 150, 243),   // blue
        new(255, 235, 59),   // yellow
        new(233, 30, 99),    // pink
        new(0, 188, 212),    // cyan
        new(156, 39, 176),   // deep purple
    };

    public ISeries[] LoadSeries { get; private set; } = null!;
    [ObservableProperty] private ObservableCollection<ISeries> _tempSeries = new();
    public ISeries[] PowerSeries { get; private set; } = null!;
    [ObservableProperty] private ObservableCollection<ISeries> _fanSeries = new();

    public LiveChartsCore.Measure.Margin ChartMargin { get; } = new(50, 8, 16, 30);

    public Axis[] LoadYAxes { get; } = { new Axis { Name = "%", MinLimit = 0, MaxLimit = 100, NamePaint = new SolidColorPaint(SKColors.Gray), LabelsPaint = new SolidColorPaint(SKColors.Gray), SeparatorsPaint = new SolidColorPaint(new SKColor(47, 0, 107)) } };
    public Axis[] TempYAxes { get; } = { new Axis { Name = "°C", MinLimit = 0, NamePaint = new SolidColorPaint(SKColors.Gray), LabelsPaint = new SolidColorPaint(SKColors.Gray), SeparatorsPaint = new SolidColorPaint(new SKColor(47, 0, 107)) } };
    public Axis[] PowerYAxes { get; } = { new Axis { Name = "W", MinLimit = 0, NamePaint = new SolidColorPaint(SKColors.Gray), LabelsPaint = new SolidColorPaint(SKColors.Gray), SeparatorsPaint = new SolidColorPaint(new SKColor(47, 0, 107)) } };
    public Axis[] FanYAxes { get; } = { new Axis { Name = "RPM", MinLimit = 0, NamePaint = new SolidColorPaint(SKColors.Gray), LabelsPaint = new SolidColorPaint(SKColors.Gray), SeparatorsPaint = new SolidColorPaint(new SKColor(47, 0, 107)) } };
    public Axis[] ChartXAxes { get; } = { new Axis { IsVisible = false, MinLimit = 0, MaxLimit = 120 } };

    public MainViewModel()
    {
        _service = new HardwareMonitorService();
        InitChartSeries();
    }

    private void InitChartSeries()
    {
        var cpuColor = new SKColor(0, 255, 236);   // #00ffec teal
        var gpuColor = new SKColor(141, 89, 207);   // #8d59cf purple

        LoadSeries = new ISeries[]
        {
            new LineSeries<ObservablePoint> { Values = _cpuLoadHistory, Name = "CPU", Stroke = new SolidColorPaint(cpuColor, 2), GeometrySize = 0, Fill = null, LineSmoothness = 0.7 },
            new LineSeries<ObservablePoint> { Values = _gpuLoadHistory, Name = "GPU", Stroke = new SolidColorPaint(gpuColor, 2), GeometrySize = 0, Fill = null, LineSmoothness = 0.7 }
        };
        PowerSeries = new ISeries[]
        {
            new LineSeries<ObservablePoint> { Values = _cpuPowerHistory, Name = "CPU", Stroke = new SolidColorPaint(cpuColor, 2), GeometrySize = 0, Fill = null, LineSmoothness = 0.7 },
            new LineSeries<ObservablePoint> { Values = _gpuPowerHistory, Name = "GPU", Stroke = new SolidColorPaint(gpuColor, 2), GeometrySize = 0, Fill = null, LineSmoothness = 0.7 }
        };
        // TempSeries and FanSeries are dynamically populated in UpdateMonitorHistory
    }

    public void Initialize()
    {
        try
        {
            // Load persisted settings
            LoadFromSettings();

            _service.Start();
            StatusText = "Monitoring active";

            _timer = new Timer(OnTimerTick, null, 0, UpdateInterval);
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    private void LoadFromSettings()
    {
        var s = App.Settings;
        AutoStartEnabled = s.AutoStart;
        MinimizeToTray = s.MinimizeToTray;
        CloseToTray = s.CloseToTray;
        StartMinimized = s.StartMinimized;
        TrayIconMode = s.TrayIconDisplay.ToString();
        UpdateInterval = s.UpdateInterval;
        CpuEnabled = s.CpuEnabled;
        GpuEnabled = s.GpuEnabled;
        MemoryEnabled = s.MemoryEnabled;
        MotherboardEnabled = s.MotherboardEnabled;
        StorageEnabled = s.StorageEnabled;
        NetworkEnabled = s.NetworkEnabled;
        BatteryEnabled = s.BatteryEnabled;
        ControllerEnabled = s.ControllerEnabled;
        PsuEnabled = s.PsuEnabled;
    }

    public void SaveToSettings()
    {
        var s = App.Settings;
        s.AutoStart = AutoStartEnabled;
        s.MinimizeToTray = MinimizeToTray;
        s.CloseToTray = CloseToTray;
        s.StartMinimized = StartMinimized;
        s.TrayIconDisplay = Enum.TryParse<Services.TrayIconMode>(TrayIconMode, out var mode) ? mode : Services.TrayIconMode.CpuTemp;
        s.UpdateInterval = UpdateInterval;
        s.CpuEnabled = CpuEnabled;
        s.GpuEnabled = GpuEnabled;
        s.MemoryEnabled = MemoryEnabled;
        s.MotherboardEnabled = MotherboardEnabled;
        s.StorageEnabled = StorageEnabled;
        s.NetworkEnabled = NetworkEnabled;
        s.BatteryEnabled = BatteryEnabled;
        s.ControllerEnabled = ControllerEnabled;
        s.PsuEnabled = PsuEnabled;
        s.Save();
    }

    private void OnTimerTick(object? state)
    {
        try
        {
            var tree = _service.GetHardwareTree();
            var summary = _service.GetDashboardSummary();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
              try
              {
                // Dashboard
                CpuTempText = summary.CpuTemp.HasValue ? $"{summary.CpuTemp:F0}°" : "—";
                CpuLoadText = summary.CpuLoad.HasValue ? $"{summary.CpuLoad:F1}%" : "—";
                CpuPowerText = summary.CpuPower.HasValue ? $"{summary.CpuPower:F0}W" : "—";
                CpuClockText = summary.CpuClock.HasValue ? $"{summary.CpuClock:F0} MHz" : (summary.CpuMaxClock.HasValue ? $"{summary.CpuMaxClock:F0} MHz" : "—");
                CpuLoadValue = summary.CpuLoad ?? 0;

                GpuTempText = summary.GpuTemp.HasValue ? $"{summary.GpuTemp:F0}°" : "—";
                GpuLoadText = summary.GpuLoad.HasValue ? $"{summary.GpuLoad:F0}%" : "—";
                GpuPowerText = summary.GpuPower.HasValue ? $"{summary.GpuPower:F0}W" : "—";
                GpuClockText = summary.GpuClock.HasValue ? $"{summary.GpuClock:F0} MHz" : "—";
                GpuFanText = summary.GpuFan.HasValue ? $"{summary.GpuFan:F0} RPM" : "0 RPM";
                GpuLoadValue = summary.GpuLoad ?? 0;

                if (summary.GpuMemUsed.HasValue && summary.GpuMemTotal.HasValue && summary.GpuMemTotal > 0)
                {
                    GpuMemText = $"{summary.GpuMemUsed:F0} / {summary.GpuMemTotal:F0} MB";
                    GpuMemValue = summary.GpuMemUsed.Value / summary.GpuMemTotal.Value * 100;
                }
                else if (summary.GpuMemUsed.HasValue)
                {
                    GpuMemText = $"{summary.GpuMemUsed:F0} MB";
                    GpuMemValue = 0;
                }

                MemLoadText = summary.MemoryLoad.HasValue ? $"{summary.MemoryLoad:F1}%" : "—";
                MemUsedText = summary.MemoryUsed.HasValue && summary.MemoryAvailable.HasValue
                    ? $"{summary.MemoryUsed:F1} / {summary.MemoryUsed + summary.MemoryAvailable:F1} GB"
                    : summary.MemoryUsed.HasValue ? $"{summary.MemoryUsed:F1} GB" : "—";
                MemLoadValue = summary.MemoryLoad ?? 0;

                BatteryText = summary.BatteryLevel.HasValue ? $"{summary.BatteryLevel:F0}%" : "N/A";
                BatteryValue = summary.BatteryLevel ?? 0;

                // Hardware tree
                HardwareTree.Clear();
                foreach (var node in tree)
                    HardwareTree.Add(node);

                // Update type-specific detail if on a detail view
                if (CurrentView.StartsWith("Detail") && SelectedHardware != null)
                    UpdateDetailView(tree);

                // Update monitor chart history
                UpdateMonitorHistory(summary, tree);

                // Update tray icon with selected sensor value
                UpdateTrayIcon(summary);

                int totalSensors = tree.Sum(CountSensors);
                StatusText = $"{DateTime.Now:HH:mm:ss}  ·  {HardwareTree.Count} devices  ·  {totalSensors} sensors";
              }
              catch (Exception ex)
              {
                StatusText = $"UI Error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"UI Error: {ex}");
              }
            });
        }
        catch { }
    }

    private static int CountSensors(HardwareNode node)
    {
        int c = node.Sensors.Count;
        foreach (var sub in node.SubHardware) c += CountSensors(sub);
        return c;
    }

    private void UpdateDetailView(List<HardwareNode> tree)
    {
        var match = FindNodeById(tree, SelectedHardware!.Identifier);
        if (match == null) return;

        SelectedSensors.Clear();
        CollectAllSensors(match, SelectedSensors);
        DetailShowEmpty = SelectedSensors.Count == 0;
        DetailHasData = !DetailShowEmpty;

        switch (CurrentView)
        {
            case "DetailCpu":
                // Refresh _cpuNodes list and update from correct index
                _cpuNodes = tree.Where(n => n.HardwareType == "Cpu").ToList();
                if (CpuSelectedIndex < _cpuNodes.Count)
                {
                    var cpuNode = _cpuNodes[CpuSelectedIndex];
                    SelectedHardware = cpuNode;
                    SelectedSensors.Clear();
                    CollectAllSensors(cpuNode, SelectedSensors);
                    UpdateCpuDetail(cpuNode);
                }
                break;
            case "DetailGpu":
                _gpuNodes = tree.Where(n => n.HardwareType is "GpuNvidia" or "GpuAmd" or "GpuIntel").ToList();
                if (GpuSelectedIndex < _gpuNodes.Count)
                {
                    var gpuNode = _gpuNodes[GpuSelectedIndex];
                    SelectedHardware = gpuNode;
                    SelectedSensors.Clear();
                    CollectAllSensors(gpuNode, SelectedSensors);
                    UpdateGpuDetail(gpuNode);
                }
                break;
            case "DetailStorage":
                UpdateStorageDetail(tree);
                break;
            case "DetailMemory":
                UpdateMemoryDetail(tree);
                break;
            case "DetailNetwork":
                UpdateNetworkDetail(tree);
                break;
            case "DetailMotherboard":
                UpdateMotherboardDetail(match);
                break;
        }
    }

    private void UpdateCpuDetail(HardwareNode node)
    {
        CpuDetailName = node.Name;
        var allSensors = node.Sensors.Concat(node.SubHardware.SelectMany(s => s.Sensors)).ToList();

        var temp = allSensors.FirstOrDefault(s => s.SensorType == "Temperature" && s.Name.Contains("Tctl"));
        CpuDetailTemp = temp != null ? $"{temp.Value:F1}°C" : "—";

        var power = allSensors.FirstOrDefault(s => s.SensorType == "Power" && s.Name == "Package");
        CpuDetailPower = power != null ? $"{power.Value:F1} W" : "—";

        var load = allSensors.FirstOrDefault(s => s.SensorType == "Load" && s.Name.Contains("Total"));
        CpuDetailLoad = load != null ? $"{load.Value:F1}%" : "—";

        var avgClock = allSensors.FirstOrDefault(s => s.SensorType == "Clock" && s.Name.Contains("Average") && !s.Name.Contains("Effective"));
        CpuDetailClock = avgClock != null ? $"{avgClock.Value:F0} MHz" : "—";

        // Per-core loads (CPU Core #N, not "CPU Core Max")
        var coreLoads = allSensors
            .Where(s => s.SensorType == "Load" && s.Name.StartsWith("CPU Core #"))
            .OrderBy(s =>
            {
                var numStr = s.Name.Replace("CPU Core #", "");
                return int.TryParse(numStr, out int n) ? n : 999;
            })
            .ToList();

        CpuCoreLoads.Clear();
        foreach (var c in coreLoads)
        {
            CpuCoreLoads.Add(new CoreLoadItem
            {
                Name = c.Name.Replace("CPU Core ", ""),
                Load = c.Value ?? 0,
                LoadText = $"{c.Value:F0}%"
            });
        }
    }

    private void UpdateGpuDetail(HardwareNode node)
    {
        GpuDetailName = node.Name;
        var sensors = node.Sensors;

        var temp = sensors.FirstOrDefault(s => s.SensorType == "Temperature" && s.Name.Contains("Core"));
        GpuDetailTemp = temp != null ? $"{temp.Value:F1}°C" : "—";

        var memJ = sensors.FirstOrDefault(s => s.SensorType == "Temperature" && s.Name.Contains("Junction"));
        GpuDetailMemJunction = memJ != null ? $"{memJ.Value:F0}°C" : "—";

        var power = sensors.FirstOrDefault(s => s.SensorType == "Power");
        GpuDetailPower = power != null ? $"{power.Value:F1} W" : "—";

        var clock = sensors.FirstOrDefault(s => s.SensorType == "Clock" && s.Name.Contains("Core"));
        GpuDetailClock = clock != null ? $"{clock.Value:F0} MHz" : "—";

        var memClock = sensors.FirstOrDefault(s => s.SensorType == "Clock" && s.Name.Contains("Memory"));
        GpuDetailMemClock = memClock != null ? $"{memClock.Value:F0} MHz" : "—";

        var memUsed = sensors.FirstOrDefault(s => s.SensorType == "SmallData" && s.Name == "GPU Memory Used");
        var memTotal = sensors.FirstOrDefault(s => s.SensorType == "SmallData" && s.Name == "GPU Memory Total");
        if (memUsed != null && memTotal != null)
        {
            GpuDetailVram = $"{memUsed.Value:F0} / {memTotal.Value:F0} MB";
            GpuDetailMemValue = memTotal.Value > 0 ? memUsed.Value!.Value / memTotal.Value!.Value * 100 : 0;
        }

        var fan = sensors.Where(s => s.SensorType == "Fan").MaxBy(s => s.Value ?? 0);
        GpuDetailFan = fan != null ? $"{fan.Value:F0} RPM" : "0 RPM";

        var voltage = sensors.FirstOrDefault(s => s.SensorType == "Voltage");
        GpuDetailVoltage = voltage != null ? $"{voltage.Value:F3} V" : "—";

        var coreLoad = sensors.FirstOrDefault(s => s.SensorType == "Load" && s.Name == "GPU Core");
        GpuDetailLoadValue = coreLoad?.Value ?? 0;

        var powerLoad = sensors.FirstOrDefault(s => s.SensorType == "Load" && s.Name.Contains("Board Power"));
        GpuDetailPowerPercent = powerLoad?.Value ?? (sensors.FirstOrDefault(s => s.SensorType == "Load" && s.Name == "GPU Power")?.Value ?? 0);
    }

    private void UpdateStorageDetail(List<HardwareNode> tree)
    {
        var drives = tree.Where(n => n.HardwareType == "Storage").ToList();
        StorageItems.Clear();
        foreach (var d in drives)
        {
            var sensors = d.Sensors;
            var temp = sensors.FirstOrDefault(s => s.SensorType == "Temperature" && s.Name.Contains("Composite"));
            var temp2 = sensors.FirstOrDefault(s => s.SensorType == "Temperature" && !s.Name.Contains("Composite"));
            var life = sensors.FirstOrDefault(s => s.SensorType == "Level" && s.Name == "Life");
            var usedSpace = sensors.FirstOrDefault(s => s.SensorType == "Load" && s.Name.Contains("Used Space"));
            var freeSpace = sensors.FirstOrDefault(s => s.SensorType == "Data" && s.Name.Contains("Free"));
            var totalSpace = sensors.FirstOrDefault(s => s.SensorType == "Data" && s.Name.Contains("Total Space"));
            var written = sensors.FirstOrDefault(s => s.SensorType == "Data" && s.Name.Contains("Written"));
            var read = sensors.FirstOrDefault(s => s.SensorType == "Data" && s.Name.Contains("Data Read"));
            var hours = sensors.FirstOrDefault(s => s.SensorType == "Factor" && s.Name.Contains("Hours"));
            var readSpeed = sensors.FirstOrDefault(s => s.SensorType == "Throughput" && s.Name.Contains("Read"));
            var writeSpeed = sensors.FirstOrDefault(s => s.SensorType == "Throughput" && s.Name.Contains("Write"));
            var spare = sensors.FirstOrDefault(s => s.SensorType == "Level" && s.Name.Contains("Spare"));
            var wear = sensors.FirstOrDefault(s => s.SensorType == "Level" && s.Name.Contains("Wear"));

            StorageItems.Add(new StorageItem
            {
                Name = d.Name,
                Temp = temp?.Value.HasValue == true ? $"{temp.Value:F0}°C" : "—",
                Temp2 = temp2 != null && temp2 != temp && temp2.Value.HasValue ? $"{temp2.Name}: {temp2.Value:F0}°C" : "",
                Health = life?.Value.HasValue == true ? $"{life.Value:F0}%" : "—",
                HealthValue = life?.Value ?? 100,
                UsedPercent = usedSpace?.Value ?? 0,
                FreeSpace = freeSpace?.Value.HasValue == true ? $"{freeSpace.Value:F1} GB" : "—",
                TotalSpace = totalSpace?.Value.HasValue == true ? $"{totalSpace.Value:F1} GB" : "—",
                DataWritten = written?.Value.HasValue == true ? FormatLargeData(written.Value.Value) : "—",
                DataRead = read?.Value.HasValue == true ? FormatLargeData(read.Value.Value) : "—",
                PowerOnHours = hours?.Value.HasValue == true ? $"{hours.Value:F0}h" : "—",
                ReadSpeed = FormatSpeed(readSpeed?.Value),
                WriteSpeed = FormatSpeed(writeSpeed?.Value),
                AvailableSpare = spare?.Value.HasValue == true ? $"{spare.Value:F0}%" : "—",
                WearLevel = wear?.Value.HasValue == true ? $"{wear.Value:F0}%" : "—"
            });
        }
    }

    private static string FormatLargeData(float gb)
    {
        if (gb >= 1024) return $"{gb / 1024:F1} TB";
        return $"{gb:F0} GB";
    }

    private SmbiosMemoryInfo[]? _smbiosMemInfo;

    private void UpdateMemoryDetail(List<HardwareNode> tree)
    {
        var memNodes = tree.Where(n => n.HardwareType == "Memory").ToList();
        var ram = memNodes.FirstOrDefault(n => n.Identifier == "/ram");
        if (ram != null)
        {
            var used = ram.Sensors.FirstOrDefault(s => s.Name.Contains("Used"));
            var avail = ram.Sensors.FirstOrDefault(s => s.Name.Contains("Available"));
            var load = ram.Sensors.FirstOrDefault(s => s.SensorType == "Load");
            MemDetailUsed = used != null ? $"{used.Value:F1} GB" : "—";
            MemDetailAvailable = avail != null ? $"{avail.Value:F1} GB" : "—";
            MemDetailLoad = load?.Value ?? 0;
        }

        // Get SMBIOS info once (cached)
        _smbiosMemInfo ??= _service.GetSmbiosMemoryInfo();

        DimmItems.Clear();
        var dimms = memNodes.Where(n => n.Identifier.Contains("/dimm/")).ToList();
        for (int i = 0; i < dimms.Count; i++)
        {
            var d = dimms[i];

            // SPD sensors
            var temp = d.Sensors.FirstOrDefault(s => s.SensorType == "Temperature" && s.Name.Contains("DIMM"));
            var cap = d.Sensors.FirstOrDefault(s => s.SensorType == "Data" && s.Name == "Capacity");

            // SPD timing sensors - prefer XMP/EXPO profile over JEDEC base
            var allTimings = d.Sensors.Where(s => s.SensorType == "Timing").ToList();
            SensorReading? Pref(string key) =>
                allTimings.FirstOrDefault(s => s.Name.Contains(key) && (s.Name.Contains("XMP") || s.Name.Contains("EXPO")))
                ?? allTimings.FirstOrDefault(s => s.Name.Contains(key));

            var tCKmin = Pref("tCKAVGmin");
            var tAA = Pref("tAA");
            var tRCD = Pref("tRCD");
            var tRP = Pref("tRP");
            var tRAS = Pref("tRAS");
            var tRC = Pref("tRC");

            // Convert ns timings to clock cycles using tCKmin
            float tCK = tCKmin?.Value ?? 0;
            int clCycles = NsToCycles(tAA?.Value, tCK);
            int rcdCycles = NsToCycles(tRCD?.Value, tCK);
            int rpCycles = NsToCycles(tRP?.Value, tCK);
            int rasCycles = NsToCycles(tRAS?.Value, tCK);
            int rcCycles = NsToCycles(tRC?.Value, tCK);

            // Derive speed from tCKmin: clock = 1000/tCK MHz, DDR rate = 2x
            int clockMHz = tCK > 0 ? (int)Math.Round(1000.0 / tCK) : 0;
            int ddrRate = clockMHz * 2;

            // Match SMBIOS data by index
            var smbios = i < _smbiosMemInfo.Length ? _smbiosMemInfo[i] : null;
            string memType = smbios?.MemoryType ?? "";
            // Normalize memory type display
            string memTypeShort = memType switch
            {
                "DDR5" or "DDR5_SDRAM" => "DDR5",
                "DDR4" or "DDR4_SDRAM" => "DDR4",
                "DDR3" or "DDR3_SDRAM" => "DDR3",
                "LPDDR5" or "LPDDR5_SDRAM" => "LPDDR5",
                "LPDDR4" or "LPDDR4X_SDRAM" or "LPDDR4_SDRAM" => "LPDDR4",
                _ when memType.Contains("DDR5") => "DDR5",
                _ when memType.Contains("DDR4") => "DDR4",
                _ when memType.Contains("DDR3") => "DDR3",
                _ => ddrRate > 0 ? "DDR" : ""
            };

            // Use SMBIOS ConfiguredSpeed as primary DDR rate (actual running speed)
            int actualMTs = smbios?.ConfiguredSpeedMTs > 0 ? smbios.ConfiguredSpeedMTs : ddrRate;
            int displayRate = actualMTs > 0 ? actualMTs : ddrRate;
            int displayClockMHz = displayRate / 2;

            // Speed text - SMBIOS speed as primary
            string speedText = displayRate > 0
                ? $"{memTypeShort}-{displayRate} ({displayClockMHz} MHz)"
                : "";

            // Timings text (clock cycles)
            string timingsText = clCycles > 0
                ? $"CL{clCycles}-{rcdCycles}-{rpCycles}-{rasCycles}"
                : "—";

            // CAS latency shorthand
            string clText = clCycles > 0 ? $"CL{clCycles}" : "—";

            // Voltage - prefer XMP/EXPO profile voltage if available
            var profileVoltage = d.Sensors.FirstOrDefault(s => s.SensorType == "Voltage" &&
                (s.Name.Contains("XMP") || s.Name.Contains("EXPO")));
            string voltageText = profileVoltage?.Value.HasValue == true
                ? $"{profileVoltage.Value:F2} V"
                : smbios?.ConfiguredVoltageMV > 0
                    ? $"{smbios.ConfiguredVoltageMV / 1000.0:F2} V"
                    : "—";

            DimmItems.Add(new DimmItem
            {
                Name = d.Name,
                Temp = temp?.Value.HasValue == true ? $"{temp.Value:F1}°C" : "—",
                Capacity = cap?.Value.HasValue == true ? $"{cap.Value:F0} GB" : "—",
                CasLatency = clText,
                MemoryType = memTypeShort,
                SpeedText = speedText,
                TimingsText = timingsText,
                Voltage = voltageText,
                Manufacturer = smbios?.Manufacturer?.Trim() ?? "",
                PartNumber = smbios?.PartNumber?.Trim() ?? ""
            });
        }
    }

    private static int NsToCycles(float? timingNs, float tCKNs)
    {
        if (timingNs is null || timingNs <= 0 || tCKNs <= 0) return 0;
        double ratio = (double)timingNs.Value / (double)tCKNs;
        int rounded = (int)Math.Round(ratio);
        // SPD stores timings as integer picoseconds, causing rounding error up to ~0.08
        // (e.g. DDR5-6000: tCK=333ps, tAA=9333ps → 9333/333=28.018, not 28.0)
        // Snap to nearest integer within tolerance; otherwise ceiling per JEDEC spec
        return Math.Abs(ratio - rounded) < 0.1 ? rounded : (int)Math.Ceiling(ratio);
    }

    private void UpdateNetworkDetail(List<HardwareNode> tree)
    {
        var nics = tree.Where(n => n.HardwareType == "Network").ToList();
        NetworkItems.Clear();
        
        // Separate active and inactive adapters
        var active = new List<NetworkItem>();
        var inactive = new List<NetworkItem>();
        
        foreach (var nic in nics)
        {
            // Filter out sub-adapters (QoS, WFP, etc.) for cleaner display
            if (nic.Name.Contains("QoS") || nic.Name.Contains("WFP") || nic.Name.Contains("Native WiFi"))
                continue;

            var dl = nic.Sensors.FirstOrDefault(s => s.SensorType == "Throughput" && s.Name.Contains("Download"));
            var ul = nic.Sensors.FirstOrDefault(s => s.SensorType == "Throughput" && s.Name.Contains("Upload"));
            var dataDown = nic.Sensors.FirstOrDefault(s => s.SensorType == "Data" && s.Name.Contains("Download"));
            var dataUp = nic.Sensors.FirstOrDefault(s => s.SensorType == "Data" && s.Name.Contains("Upload"));

            var item = new NetworkItem
            {
                Name = nic.Name,
                DownSpeed = FormatSpeed(dl?.Value),
                UpSpeed = FormatSpeed(ul?.Value),
                TotalDown = dataDown?.Value.HasValue == true ? $"{dataDown.Value:F2} GB" : "0 GB",
                TotalUp = dataUp?.Value.HasValue == true ? $"{dataUp.Value:F2} GB" : "0 GB"
            };
            
            // Prioritize adapters with active traffic (speed > 0)
            double dlSpeed = dl?.Value ?? 0;
            double ulSpeed = ul?.Value ?? 0;
            if (dlSpeed > 0 || ulSpeed > 0)
                active.Add(item);
            else
                inactive.Add(item);
        }
        
        // Add active adapters first, then inactive
        foreach (var item in active)
            NetworkItems.Add(item);
        foreach (var item in inactive)
            NetworkItems.Add(item);
    }

    private void UpdateMotherboardDetail(HardwareNode node)
    {
        var allSensors = new List<SensorReading>();
        CollectAllSensorsToList(node, allSensors);

        // Fan items: pair Control (duty %) with Fan (RPM) sensors by name
        var controls = allSensors.Where(s => s.SensorType == "Control").ToList();
        var fans = allSensors.Where(s => s.SensorType == "Fan").ToList();

        var fanItems = new List<MbFanItem>();
        foreach (var ctrl in controls)
        {
            var matching = fans.FirstOrDefault(f => f.Name == ctrl.Name);
            fanItems.Add(new MbFanItem
            {
                Name = ctrl.Name,
                DutyValue = ctrl.Value ?? 0,
                DutyText = $"{ctrl.Value:F0}%",
                RpmText = matching != null ? $"{matching.Value:F0}" : "—",
                MinRpm = matching != null ? $"min {matching.Min:F0}" : "",
                MaxRpm = matching != null ? $"max {matching.Max:F0}" : ""
            });
        }
        // Add fan sensors without a matching control
        foreach (var fan in fans)
        {
            if (!controls.Any(c => c.Name == fan.Name))
            {
                fanItems.Add(new MbFanItem
                {
                    Name = fan.Name,
                    DutyValue = 0,
                    DutyText = "—",
                    RpmText = $"{fan.Value:F0}",
                    MinRpm = $"min {fan.Min:F0}",
                    MaxRpm = $"max {fan.Max:F0}"
                });
            }
        }

        MbFanItems.Clear();
        foreach (var f in fanItems) MbFanItems.Add(f);

        // Temperature items
        var temps = allSensors.Where(s => s.SensorType == "Temperature").ToList();
        MbTempItems.Clear();
        foreach (var t in temps)
        {
            MbTempItems.Add(new MbTempItem
            {
                Name = t.Name,
                ValueText = $"{t.Value:F1}°C",
                MinText = $"min {t.Min:F1}°",
                MaxText = $"max {t.Max:F1}°"
            });
        }

        // Voltage items
        var voltages = allSensors.Where(s => s.SensorType == "Voltage").ToList();
        MbVoltageItems.Clear();
        foreach (var v in voltages)
        {
            MbVoltageItems.Add(new MbVoltageItem
            {
                Name = v.Name,
                ValueText = $"{v.Value:F3} V",
                MinText = $"min {v.Min:F3}",
                MaxText = $"max {v.Max:F3}"
            });
        }

        MbShowEmpty = MbFanItems.Count == 0 && MbTempItems.Count == 0 && MbVoltageItems.Count == 0;
        MbHasData = !MbShowEmpty;
    }

    private static void CollectAllSensorsToList(HardwareNode node, List<SensorReading> target)
    {
        foreach (var s in node.Sensors) target.Add(s);
        foreach (var sub in node.SubHardware)
            CollectAllSensorsToList(sub, target);
    }

    private static string FormatSpeed(float? bytesPerSec)
    {
        if (bytesPerSec is null or 0) return "0 B/s";
        float v = bytesPerSec.Value;
        if (v >= 1_073_741_824) return $"{v / 1_073_741_824:F1} GB/s";
        if (v >= 1_048_576) return $"{v / 1_048_576:F1} MB/s";
        if (v >= 1024) return $"{v / 1024:F1} KB/s";
        return $"{v:F0} B/s";
    }

    private static void CollectAllSensors(HardwareNode node, ObservableCollection<SensorReading> target)
    {
        foreach (var s in node.Sensors) target.Add(s);
        foreach (var sub in node.SubHardware)
            CollectAllSensors(sub, target);
    }

    private static HardwareNode? FindNodeById(List<HardwareNode> nodes, string id)
    {
        foreach (var node in nodes)
        {
            if (node.Identifier == id) return node;
            var sub = FindNodeById(node.SubHardware, id);
            if (sub != null) return sub;
        }
        return null;
    }

    [RelayCommand]
    private void NavigateTo(string view)
    {
        CurrentView = view;
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;
        SidebarWidth = IsSidebarCollapsed ? 52 : 240;
    }

    [RelayCommand]
    private void SetInterval(string ms)
    {
        if (int.TryParse(ms, out int val))
            UpdateInterval = val;
    }

    [RelayCommand]
    private void SelectHardware(HardwareNode? node)
    {
        if (node == null) return;
        SelectedHardware = node;
        SelectedHardwareName = node.Name;
        SelectedHardwareType = $"{node.Icon} {node.HardwareType}";

        // Dashboard clicks always show generic sensor list
        SelectedSensors.Clear();
        CollectAllSensors(node, SelectedSensors);
        CurrentView = "Detail";
    }

    [RelayCommand]
    private void NavigateToType(string type)
    {
        HardwareNode? node;
        switch (type)
        {
            case "Cpu":
                _cpuNodes = HardwareTree.Where(n => n.HardwareType == "Cpu").ToList();
                CpuHasMultiple = _cpuNodes.Count > 1;
                CpuSelectedIndex = 0;
                node = _cpuNodes.FirstOrDefault();
                if (node == null) return;
                CpuSwitchLabel = $"1 / {_cpuNodes.Count}";
                break;
            case "Gpu":
                _gpuNodes = HardwareTree.Where(n => n.HardwareType is "GpuNvidia" or "GpuAmd" or "GpuIntel").ToList();
                GpuHasMultiple = _gpuNodes.Count > 1;
                GpuSelectedIndex = 0;
                node = _gpuNodes.FirstOrDefault();
                if (node == null) return;
                GpuSwitchLabel = $"1 / {_gpuNodes.Count}";
                break;
            default:
                node = type switch
                {
                    "Storage" => HardwareTree.FirstOrDefault(n => n.HardwareType == "Storage"),
                    "Memory" => HardwareTree.FirstOrDefault(n => n.HardwareType == "Memory"),
                    "Network" => HardwareTree.FirstOrDefault(n => n.HardwareType == "Network"),
                    "Motherboard" => HardwareTree.FirstOrDefault(n => n.HardwareType == "Motherboard"),
                    _ => null
                };
                if (node == null) return;
                break;
        }

        SelectedHardware = node;
        SelectedHardwareName = node.Name;
        SelectedHardwareType = $"{node.Icon} {node.HardwareType}";

        CurrentView = type switch
        {
            "Cpu" => "DetailCpu",
            "Gpu" => "DetailGpu",
            "Storage" => "DetailStorage",
            "Memory" => "DetailMemory",
            "Network" => "DetailNetwork",
            "Motherboard" => "DetailMotherboard",
            _ => "Detail"
        };
    }

    [RelayCommand]
    private void SwitchCpu(string direction)
    {
        if (_cpuNodes.Count <= 1) return;
        CpuSelectedIndex = direction == "next"
            ? (CpuSelectedIndex + 1) % _cpuNodes.Count
            : (CpuSelectedIndex - 1 + _cpuNodes.Count) % _cpuNodes.Count;
        var node = _cpuNodes[CpuSelectedIndex];
        SelectedHardware = node;
        SelectedHardwareName = node.Name;
        CpuSwitchLabel = $"{CpuSelectedIndex + 1} / {_cpuNodes.Count}";
    }

    [RelayCommand]
    private void SwitchGpu(string direction)
    {
        if (_gpuNodes.Count <= 1) return;
        GpuSelectedIndex = direction == "next"
            ? (GpuSelectedIndex + 1) % _gpuNodes.Count
            : (GpuSelectedIndex - 1 + _gpuNodes.Count) % _gpuNodes.Count;
        var node = _gpuNodes[GpuSelectedIndex];
        SelectedHardware = node;
        SelectedHardwareName = node.Name;
        GpuSwitchLabel = $"{GpuSelectedIndex + 1} / {_gpuNodes.Count}";
    }

    partial void OnUpdateIntervalChanged(int value) => _timer?.Change(0, value);

    partial void OnAutoStartEnabledChanged(bool value) => AutoStartService.SetEnabled(value);
    partial void OnMinimizeToTrayChanged(bool value) => App.Settings.MinimizeToTray = value;
    partial void OnCloseToTrayChanged(bool value) => App.Settings.CloseToTray = value;
    partial void OnStartMinimizedChanged(bool value) => App.Settings.StartMinimized = value;

    [RelayCommand]
    private void SetTrayIconMode(string mode)
    {
        TrayIconMode = mode;
    }

    partial void OnCpuEnabledChanged(bool value) => _service.SetEnabled("Cpu", value);
    partial void OnGpuEnabledChanged(bool value) => _service.SetEnabled("Gpu", value);
    partial void OnMemoryEnabledChanged(bool value) => _service.SetEnabled("Memory", value);
    partial void OnMotherboardEnabledChanged(bool value) => _service.SetEnabled("Motherboard", value);
    partial void OnStorageEnabledChanged(bool value) => _service.SetEnabled("Storage", value);
    partial void OnNetworkEnabledChanged(bool value) => _service.SetEnabled("Network", value);
    partial void OnBatteryEnabledChanged(bool value) => _service.SetEnabled("Battery", value);
    partial void OnControllerEnabledChanged(bool value) => _service.SetEnabled("Controller", value);
    partial void OnPsuEnabledChanged(bool value) => _service.SetEnabled("Psu", value);

    private void UpdateMonitorHistory(DashboardSummary summary, List<HardwareNode> tree)
    {
        _historyTick++;

        // Initialize EMA on first tick
        if (!_emaInitialized)
        {
            _emaCpuLoad = summary.CpuLoad ?? 0;
            _emaGpuLoad = summary.GpuLoad ?? 0;
            _emaCpuPower = summary.CpuPower ?? 0;
            _emaGpuPower = summary.GpuPower ?? 0;
            _emaInitialized = true;
        }

        // Load & Power (fixed CPU/GPU) with EMA smoothing
        _emaCpuLoad = EmaAlpha * (summary.CpuLoad ?? 0) + (1 - EmaAlpha) * _emaCpuLoad;
        _emaGpuLoad = EmaAlpha * (summary.GpuLoad ?? 0) + (1 - EmaAlpha) * _emaGpuLoad;
        _emaCpuPower = EmaAlpha * (summary.CpuPower ?? 0) + (1 - EmaAlpha) * _emaCpuPower;
        _emaGpuPower = EmaAlpha * (summary.GpuPower ?? 0) + (1 - EmaAlpha) * _emaGpuPower;

        AddPoint(_cpuLoadHistory, _historyTick, _emaCpuLoad);
        AddPoint(_gpuLoadHistory, _historyTick, _emaGpuLoad);
        AddPoint(_cpuPowerHistory, _historyTick, _emaCpuPower);
        AddPoint(_gpuPowerHistory, _historyTick, _emaGpuPower);

        // Temperature: one representative temp per hardware device
        var tempReadings = new Dictionary<string, double>();
        foreach (var node in tree)
            CollectDeviceTemps(node, tempReadings);

        foreach (var (label, value) in tempReadings)
        {
            if (!_tempHistories.TryGetValue(label, out var col))
            {
                col = new ObservableCollection<ObservablePoint>();
                _tempHistories[label] = col;
                _tempEma[label] = value;
                var color = SeriesColors[_tempHistories.Count % SeriesColors.Length];
                TempSeries.Add(new LineSeries<ObservablePoint>
                {
                    Values = col, Name = label,
                    Stroke = new SolidColorPaint(color, 2),
                    GeometrySize = 0, Fill = null, LineSmoothness = 0.7
                });
                TempLegendItems.Add(new LegendItem { Label = label, Color = $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}" });
            }
            var ema = EmaAlpha * value + (1 - EmaAlpha) * _tempEma[label];
            _tempEma[label] = ema;
            AddPoint(col, _historyTick, ema);
        }

        // Fan speed: every fan RPM sensor from all devices
        var fanReadings = new Dictionary<string, double>();
        foreach (var node in tree)
            CollectFanSpeeds(node, fanReadings);

        foreach (var (label, rpm) in fanReadings)
        {
            if (!_fanHistories.TryGetValue(label, out var col))
            {
                col = new ObservableCollection<ObservablePoint>();
                _fanHistories[label] = col;
                _fanEma[label] = rpm;
                var color = SeriesColors[_fanHistories.Count % SeriesColors.Length];
                FanSeries.Add(new LineSeries<ObservablePoint>
                {
                    Values = col, Name = label,
                    Stroke = new SolidColorPaint(color, 2),
                    GeometrySize = 0, Fill = null, LineSmoothness = 0.7
                });
                FanLegendItems.Add(new LegendItem { Label = label, Color = $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}" });
            }
            var ema = EmaAlpha * rpm + (1 - EmaAlpha) * _fanEma[label];
            _fanEma[label] = ema;
            AddPoint(col, _historyTick, ema);
        }

        // Slide X axis window
        if (_historyTick > MaxHistory)
        {
            foreach (var ax in ChartXAxes)
            {
                ax.MinLimit = _historyTick - MaxHistory;
                ax.MaxLimit = _historyTick;
            }
        }
    }

    private static void CollectDeviceTemps(HardwareNode node, Dictionary<string, double> result)
    {
        var allSensors = new List<SensorReading>();
        CollectAllSensorsToList(node, allSensors);
        var temp = allSensors.FirstOrDefault(s => s.SensorType == "Temperature");
        if (temp?.Value != null)
        {
            var label = node.HardwareType switch
            {
                "Cpu" => "CPU",
                "GpuNvidia" or "GpuAmd" or "GpuIntel" => "GPU",
                "Storage" => node.Name.Length > 16 ? node.Name[..16] : node.Name,
                "Motherboard" => "MB",
                _ => node.HardwareType.Length > 10 ? node.HardwareType[..10] : node.HardwareType
            };
            if (!result.ContainsKey(label))
                result[label] = temp.Value.Value;
        }
    }

    private static void CollectFanSpeeds(HardwareNode node, Dictionary<string, double> result)
    {
        var allSensors = new List<SensorReading>();
        CollectAllSensorsToList(node, allSensors);
        var fans = allSensors.Where(s => s.SensorType == "Fan" && s.Value is > 0);
        foreach (var fan in fans)
        {
            var prefix = node.HardwareType switch
            {
                "GpuNvidia" or "GpuAmd" or "GpuIntel" => "GPU",
                "Motherboard" => "MB",
                _ => node.HardwareType.Length > 6 ? node.HardwareType[..6] : node.HardwareType
            };
            // Short label: prefix + fan number
            var fanName = fan.Name.Replace("Fan Control ", "F").Replace("Fan #", "F");
            var label = $"{prefix} {fanName}";
            if (!result.ContainsKey(label))
                result[label] = fan.Value!.Value;
        }
    }

    private static void AddPoint(ObservableCollection<ObservablePoint> col, int x, double y)
    {
        col.Add(new ObservablePoint(x, y));
        while (col.Count > MaxHistory)
            col.RemoveAt(0);
    }

    private void UpdateTrayIcon(DashboardSummary summary)
    {
        if (!Enum.TryParse<Services.TrayIconMode>(TrayIconMode, out var mode))
            mode = Services.TrayIconMode.CpuTemp;

        float? value = mode switch
        {
            Services.TrayIconMode.CpuTemp => summary.CpuTemp,
            Services.TrayIconMode.GpuTemp => summary.GpuTemp,
            Services.TrayIconMode.CpuLoad => summary.CpuLoad,
            Services.TrayIconMode.GpuLoad => summary.GpuLoad,
            _ => null,
        };

        App.TrayService.UpdateIcon(mode, value);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            SaveToSettings();
            _timer?.Dispose();
            _service.Dispose();
        }
    }

}

// Per-core load item for CPU detail
public class CoreLoadItem
{
    public string Name { get; set; } = "";
    public float Load { get; set; }
    public string LoadText { get; set; } = "";
}

// Drive item for storage detail
public class StorageItem
{
    public string Name { get; set; } = "";
    public string Temp { get; set; } = "—";
    public string Temp2 { get; set; } = "";
    public string Health { get; set; } = "—";
    public float HealthValue { get; set; }
    public float UsedPercent { get; set; }
    public string FreeSpace { get; set; } = "—";
    public string TotalSpace { get; set; } = "—";
    public string DataWritten { get; set; } = "—";
    public string DataRead { get; set; } = "—";
    public string PowerOnHours { get; set; } = "—";
    public string ReadSpeed { get; set; } = "—";
    public string WriteSpeed { get; set; } = "—";
    public string AvailableSpare { get; set; } = "—";
    public string WearLevel { get; set; } = "—";
}

// Network adapter item
public class NetworkItem
{
    public string Name { get; set; } = "";
    public string DownSpeed { get; set; } = "0 B/s";
    public string UpSpeed { get; set; } = "0 B/s";
    public string TotalDown { get; set; } = "0 GB";
    public string TotalUp { get; set; } = "0 GB";
}

// DIMM item for memory detail
public class DimmItem
{
    public string Name { get; set; } = "";
    public string Temp { get; set; } = "—";
    public string Capacity { get; set; } = "—";
    public string CasLatency { get; set; } = "—";
    public string MemoryType { get; set; } = "";     // "DDR5"
    public string SpeedText { get; set; } = "";       // "DDR5-6000 @ 3000 MHz"
    public string TimingsText { get; set; } = "";     // "CL28-35-35-76"
    public string Voltage { get; set; } = "—";        // "1.35 V"
    public string Manufacturer { get; set; } = "";    // "Asgard"
    public string PartNumber { get; set; } = "";      // "VAM5UH60C28BG-CBRBWA"
}

// Motherboard fan item (paired control + speed)
public class MbFanItem
{
    public string Name { get; set; } = "";
    public float DutyValue { get; set; }
    public string DutyText { get; set; } = "—";
    public string RpmText { get; set; } = "—";
    public string MinRpm { get; set; } = "";
    public string MaxRpm { get; set; } = "";
}

// Motherboard temperature item
public class MbTempItem
{
    public string Name { get; set; } = "";
    public string ValueText { get; set; } = "—";
    public string MinText { get; set; } = "";
    public string MaxText { get; set; } = "";
}

// Motherboard voltage item
public class MbVoltageItem
{
    public string Name { get; set; } = "";
    public string ValueText { get; set; } = "—";
    public string MinText { get; set; } = "";
    public string MaxText { get; set; } = "";
}

// Legend item for custom inline chart legends
public class LegendItem
{
    public string Label { get; set; } = "";
    public string Color { get; set; } = "#ffffff";
}
