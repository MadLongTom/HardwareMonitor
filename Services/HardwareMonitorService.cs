using System;
using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;
using HardwareMonitor.Models;

namespace HardwareMonitor.Services;

public class HardwareMonitorService : IDisposable
{
    private readonly Computer _computer;
    private bool _disposed;
    private const int MaxHistoryPoints = 120;

    private readonly Dictionary<string, List<float>> _sensorHistory = new();

    public HardwareMonitorService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsStorageEnabled = true,
            IsNetworkEnabled = true,
            IsBatteryEnabled = true,
            IsControllerEnabled = true,
            IsPsuEnabled = true
        };
    }

    public void Start() => _computer.Open();

    public void SetEnabled(string category, bool enabled)
    {
        switch (category)
        {
            case "Cpu": _computer.IsCpuEnabled = enabled; break;
            case "Gpu": _computer.IsGpuEnabled = enabled; break;
            case "Memory": _computer.IsMemoryEnabled = enabled; break;
            case "Motherboard": _computer.IsMotherboardEnabled = enabled; break;
            case "Storage": _computer.IsStorageEnabled = enabled; break;
            case "Network": _computer.IsNetworkEnabled = enabled; break;
            case "Battery": _computer.IsBatteryEnabled = enabled; break;
            case "Controller": _computer.IsControllerEnabled = enabled; break;
            case "Psu": _computer.IsPsuEnabled = enabled; break;
        }
    }

    public List<HardwareNode> GetHardwareTree()
    {
        var nodes = new List<HardwareNode>();
        foreach (var hw in _computer.Hardware)
        {
            hw.Update();
            nodes.Add(BuildNode(hw));
        }
        return nodes;
    }

    private HardwareNode BuildNode(IHardware hardware)
    {
        var node = new HardwareNode
        {
            Name = SanitizeName(hardware.Name),
            HardwareType = hardware.HardwareType.ToString(),
            Identifier = hardware.Identifier.ToString()
        };

        foreach (var sensor in hardware.Sensors)
        {
            var id = sensor.Identifier.ToString();
            if (!_sensorHistory.TryGetValue(id, out var history))
            {
                history = new List<float>();
                _sensorHistory[id] = history;
            }

            if (sensor.Value.HasValue)
            {
                history.Add(sensor.Value.Value);
                if (history.Count > MaxHistoryPoints)
                    history.RemoveAt(0);
            }

            var reading = new SensorReading
            {
                Name = sensor.Name,
                SensorType = sensor.SensorType.ToString(),
                Value = sensor.Value,
                Min = sensor.Min,
                Max = sensor.Max
            };
            reading.History.Clear();
            reading.History.AddRange(history);
            node.Sensors.Add(reading);
        }

        node.Sensors.Sort((a, b) =>
        {
            int cmp = string.Compare(a.SensorType, b.SensorType, StringComparison.Ordinal);
            return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        });

        foreach (var sub in hardware.SubHardware)
        {
            sub.Update();
            node.SubHardware.Add(BuildNode(sub));
        }

        return node;
    }

    private static string SanitizeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var chars = name.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] < 0x20 || (chars[i] >= 0x7F && chars[i] <= 0x9F))
                chars[i] = '?';
        }
        return new string(chars).Trim();
    }

    public DashboardSummary GetDashboardSummary()
    {
        var summary = new DashboardSummary();

        foreach (var hw in _computer.Hardware)
        {
            hw.Update();
            CollectSummary(hw, summary);
            foreach (var sub in hw.SubHardware)
            {
                sub.Update();
                CollectSummary(sub, summary);
            }
        }

        return summary;
    }

    private static void CollectSummary(IHardware hw, DashboardSummary summary)
    {
        foreach (var sensor in hw.Sensors)
        {
            if (!sensor.Value.HasValue) continue;
            float v = sensor.Value.Value;

            switch (hw.HardwareType)
            {
                case HardwareType.Cpu:
                    if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase))
                        summary.CpuTemp = v;
                    else if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) && !summary.CpuTemp.HasValue)
                        summary.CpuTemp = v;
                    else if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase))
                        summary.CpuLoad = v;
                    else if (sensor.SensorType == SensorType.Power && sensor.Name.Equals("Package", StringComparison.OrdinalIgnoreCase))
                        summary.CpuPower = v;
                    else if (sensor.SensorType == SensorType.Clock && sensor.Name.Contains("Average", StringComparison.OrdinalIgnoreCase) && !sensor.Name.Contains("Effective", StringComparison.OrdinalIgnoreCase))
                        summary.CpuClock = v;
                    else if (sensor.SensorType == SensorType.Clock && !sensor.Name.Contains("Effective", StringComparison.OrdinalIgnoreCase) && !sensor.Name.Contains("Bus", StringComparison.OrdinalIgnoreCase) && !sensor.Name.Contains("Average", StringComparison.OrdinalIgnoreCase))
                        summary.CpuMaxClock = Math.Max(summary.CpuMaxClock ?? 0, v);
                    break;

                case HardwareType.GpuNvidia:
                    if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                        summary.GpuTemp = v;
                    else if (sensor.SensorType == SensorType.Load && sensor.Name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase))
                        summary.GpuLoad = v;
                    else if (sensor.SensorType == SensorType.Power && sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase))
                        summary.GpuPower = v;
                    else if (sensor.SensorType == SensorType.SmallData && sensor.Name.Equals("GPU Memory Used", StringComparison.OrdinalIgnoreCase))
                        summary.GpuMemUsed = v;
                    else if (sensor.SensorType == SensorType.SmallData && sensor.Name.Equals("GPU Memory Total", StringComparison.OrdinalIgnoreCase))
                        summary.GpuMemTotal = v;
                    else if (sensor.SensorType == SensorType.Clock && sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                        summary.GpuClock = v;
                    else if (sensor.SensorType == SensorType.Fan)
                        summary.GpuFan = Math.Max(summary.GpuFan ?? 0, v);
                    break;

                case HardwareType.GpuAmd or HardwareType.GpuIntel:
                    if (!summary.GpuTemp.HasValue && sensor.SensorType == SensorType.Temperature)
                        summary.GpuTemp = v;
                    if (!summary.GpuLoad.HasValue && sensor.SensorType == SensorType.Load && sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                        summary.GpuLoad = v;
                    break;

                case HardwareType.Memory:
                    if (hw.Identifier.ToString() == "/ram")
                    {
                        if (sensor.SensorType == SensorType.Load)
                            summary.MemoryLoad = v;
                        else if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("Used", StringComparison.OrdinalIgnoreCase))
                            summary.MemoryUsed = v;
                        else if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("Available", StringComparison.OrdinalIgnoreCase))
                            summary.MemoryAvailable = v;
                    }
                    break;

                case HardwareType.Battery:
                    if (sensor.SensorType == SensorType.Level)
                        summary.BatteryLevel = v;
                    break;
            }
        }
    }

    public void Stop() => _computer.Close();

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Stop();
        }
    }
}

public class DashboardSummary
{
    public float? CpuTemp { get; set; }
    public float? CpuLoad { get; set; }
    public float? CpuPower { get; set; }
    public float? CpuClock { get; set; }
    public float? CpuMaxClock { get; set; }

    public float? GpuTemp { get; set; }
    public float? GpuLoad { get; set; }
    public float? GpuPower { get; set; }
    public float? GpuMemUsed { get; set; }
    public float? GpuMemTotal { get; set; }
    public float? GpuClock { get; set; }
    public float? GpuFan { get; set; }

    public float? MemoryLoad { get; set; }
    public float? MemoryUsed { get; set; }
    public float? MemoryAvailable { get; set; }

    public float? BatteryLevel { get; set; }
}
