using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace HardwareMonitor.Services;

/// <summary>
/// Cross-platform auto-start management.
/// Windows: HKCU\Software\Microsoft\Windows\CurrentVersion\Run
/// Linux:   ~/.config/autostart/HardwareMonitor.desktop
/// macOS:   ~/Library/LaunchAgents/com.hardwaremonitor.plist
/// </summary>
public static class AutoStartService
{
    private const string AppName = "HardwareMonitor";

    public static bool IsEnabled
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return IsEnabledWindows();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return File.Exists(LinuxDesktopPath());
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return File.Exists(MacPlistPath());
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            SetEnabledWindows(enabled);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            SetEnabledLinux(enabled);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            SetEnabledMac(enabled);
    }

    private static string GetExePath()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
            exe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
        return exe;
    }

    // ═══ Windows: Registry ═══
    [SupportedOSPlatform("windows")]
    private static bool IsEnabledWindows()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", false);
            return key?.GetValue(AppName) is string val && val.Length > 0;
        }
        catch { return false; }
    }

    [SupportedOSPlatform("windows")]
    private static void SetEnabledWindows(bool enabled)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;

            if (enabled)
            {
                var exe = GetExePath();
                if (!string.IsNullOrEmpty(exe))
                    key.SetValue(AppName, $"\"{exe}\" --minimized");
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch
        {
            // May fail without appropriate permissions
        }
    }

    // ═══ Linux: XDG Autostart ═══
    private static string LinuxDesktopPath()
    {
        var configDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(configDir))
            configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(configDir, "autostart", $"{AppName}.desktop");
    }

    private static void SetEnabledLinux(bool enabled)
    {
        var path = LinuxDesktopPath();
        if (enabled)
        {
            var exe = GetExePath();
            if (string.IsNullOrEmpty(exe)) return;

            var dir = Path.GetDirectoryName(path);
            if (dir != null) Directory.CreateDirectory(dir);

            var content = $"""
                [Desktop Entry]
                Type=Application
                Name={AppName}
                Exec="{exe}" --minimized
                Terminal=false
                X-GNOME-Autostart-enabled=true
                Comment=Hardware monitoring and fan control
                """;
            File.WriteAllText(path, content);
        }
        else
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ═══ macOS: LaunchAgent ═══
    private static string MacPlistPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, "Library", "LaunchAgents", "com.hardwaremonitor.plist");
    }

    private static void SetEnabledMac(bool enabled)
    {
        var path = MacPlistPath();
        if (enabled)
        {
            var exe = GetExePath();
            if (string.IsNullOrEmpty(exe)) return;

            var dir = Path.GetDirectoryName(path);
            if (dir != null) Directory.CreateDirectory(dir);

            var plist = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0">
                <dict>
                    <key>Label</key>
                    <string>com.hardwaremonitor</string>
                    <key>ProgramArguments</key>
                    <array>
                        <string>{exe}</string>
                        <string>--minimized</string>
                    </array>
                    <key>RunAtLoad</key>
                    <true/>
                    <key>KeepAlive</key>
                    <false/>
                </dict>
                </plist>
                """;
            File.WriteAllText(path, plist);
        }
        else
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
