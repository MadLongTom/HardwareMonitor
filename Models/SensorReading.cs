using System.Collections.Generic;

namespace HardwareMonitor.Models;

public class SensorReading
{
    public string Name { get; set; } = "";
    public string SensorType { get; set; } = "";
    public float? Value { get; set; }
    public float? Min { get; set; }
    public float? Max { get; set; }
    public string FormattedValue => FormatValue(Value, SensorType);
    public string FormattedMin => FormatValue(Min, SensorType);
    public string FormattedMax => FormatValue(Max, SensorType);
    public string Unit => GetUnit(SensorType);
    public List<float> History { get; } = new();

    private static string FormatValue(float? value, string sensorType)
    {
        if (value is null) return "—";
        return sensorType switch
        {
            "Temperature" => $"{value:F1} °C",
            "Voltage" => $"{value:F3} V",
            "Clock" or "Frequency" => $"{value:F0} MHz",
            "Load" or "Level" or "Control" => $"{value:F1} %",
            "Fan" => $"{value:F0} RPM",
            "Flow" => $"{value:F1} L/h",
            "Power" => $"{value:F1} W",
            "Current" => $"{value:F3} A",
            "Data" => $"{value:F2} GB",
            "SmallData" => $"{value:F2} MB",
            "Factor" => $"{value:F3}",
            "Throughput" => FormatThroughput(value.Value),
            "TimeSpan" => $"{value:F1} s",
            "Energy" => $"{value:F1} Wh",
            "Noise" => $"{value:F1} dBA",
            "Humidity" => $"{value:F1} %",
            "Conductivity" => $"{value:F1} µS/cm",
            "Timing" => $"{value:F1} ns",
            _ => $"{value:F2}"
        };
    }

    private static string FormatThroughput(float bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824:F1} GB/s";
        if (bytes >= 1_048_576) return $"{bytes / 1_048_576:F1} MB/s";
        if (bytes >= 1024) return $"{bytes / 1024:F1} KB/s";
        return $"{bytes:F0} B/s";
    }

    private static string GetUnit(string sensorType)
    {
        return sensorType switch
        {
            "Temperature" => "°C",
            "Voltage" => "V",
            "Clock" or "Frequency" => "MHz",
            "Load" or "Level" or "Control" or "Humidity" => "%",
            "Fan" => "RPM",
            "Flow" => "L/h",
            "Power" => "W",
            "Current" => "A",
            "Data" => "GB",
            "SmallData" => "MB",
            "Throughput" => "B/s",
            "TimeSpan" => "s",
            "Energy" => "Wh",
            "Noise" => "dBA",
            "Conductivity" => "µS/cm",
            "Timing" => "ns",
            _ => ""
        };
    }
}
