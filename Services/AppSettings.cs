using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HardwareMonitor.Services;

/// <summary>
/// Persistent application settings with JSON file storage.
/// </summary>
public class AppSettings
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HardwareMonitor");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    // ═══ General ═══
    public bool AutoStart { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool StartMinimized { get; set; }

    // ═══ Tray Icon ═══
    public TrayIconMode TrayIconDisplay { get; set; } = TrayIconMode.CpuTemp;
    public string? TrayCustomSensorId { get; set; }

    // ═══ Monitoring ═══
    public int UpdateInterval { get; set; } = 1000;
    public bool CpuEnabled { get; set; } = true;
    public bool GpuEnabled { get; set; } = true;
    public bool MemoryEnabled { get; set; } = true;
    public bool MotherboardEnabled { get; set; } = true;
    public bool StorageEnabled { get; set; } = true;
    public bool NetworkEnabled { get; set; } = true;
    public bool BatteryEnabled { get; set; } = true;
    public bool ControllerEnabled { get; set; } = true;
    public bool PsuEnabled { get; set; } = true;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize(json, AppSettingsContext.Default.AppSettings) ?? new AppSettings();
            }
        }
        catch
        {
            // Corrupted file — return defaults
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(this, AppSettingsContext.Default.AppSettings);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Best-effort persistence
        }
    }
}

public enum TrayIconMode
{
    AppIcon,
    CpuTemp,
    GpuTemp,
    CpuLoad,
    GpuLoad,
    CustomSensor,
}

// AOT-compatible JSON source generation
[JsonSerializable(typeof(AppSettings))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class AppSettingsContext : JsonSerializerContext;
