using System.Collections.Generic;
using System.Linq;

namespace HardwareMonitor.Models;

public class HardwareNode
{
    public string Name { get; set; } = "";
    public string HardwareType { get; set; } = "";
    public string Identifier { get; set; } = "";
    public List<SensorReading> Sensors { get; set; } = new();
    public List<HardwareNode> SubHardware { get; set; } = new();

    public int TotalSensorCount => Sensors.Count + SubHardware.Sum(s => s.TotalSensorCount);

    public string Icon => HardwareType switch
    {
        "Cpu" => "🔲",
        "GpuNvidia" or "GpuAmd" or "GpuIntel" => "🎮",
        "Motherboard" => "🖥️",
        "SuperIO" => "⚡",
        "Memory" => "💾",
        "Storage" => "💿",
        "Network" => "🌐",
        "Battery" => "🔋",
        "Cooler" => "❄️",
        "EmbeddedController" => "🔌",
        "Psu" => "⚡",
        "PowerMonitor" => "📊",
        _ => "📦"
    };
}
