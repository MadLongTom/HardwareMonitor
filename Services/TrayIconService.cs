using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using SkiaSharp;

namespace HardwareMonitor.Services;

/// <summary>
/// Manages the system tray icon with dynamic sensor value rendering.
/// Uses SkiaSharp to render temperature/load/RPM numbers directly as the tray icon.
/// </summary>
public class TrayIconService : IDisposable
{
    private TrayIcon? _trayIcon;
    private NativeMenu? _contextMenu;
    private WindowIcon? _currentIcon;
    private bool _disposed;

    public event Action? ShowRequested;
    public event Action? ExitRequested;

    public TrayIcon? TrayIcon => _trayIcon;

    public void Initialize()
    {
        _contextMenu = new NativeMenu();

        var showItem = new NativeMenuItem("Show Hardware Monitor");
        showItem.Click += (_, _) => ShowRequested?.Invoke();
        _contextMenu.Add(showItem);

        _contextMenu.Add(new NativeMenuItemSeparator());

        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke();
        _contextMenu.Add(exitItem);

        _trayIcon = new TrayIcon
        {
            ToolTipText = "Hardware Monitor",
            Menu = _contextMenu,
            IsVisible = true,
        };

        _trayIcon.Clicked += (_, _) => ShowRequested?.Invoke();

        // Set default icon
        UpdateIcon(TrayIconMode.AppIcon, null);
    }

    /// <summary>
    /// Update the tray icon to display a sensor value or the app icon.
    /// </summary>
    public void UpdateIcon(TrayIconMode mode, float? sensorValue)
    {
        if (_trayIcon == null) return;

        try
        {
            if (mode == TrayIconMode.AppIcon || sensorValue == null)
            {
                _trayIcon.Icon = CreateTextIcon("HM", SKColors.White);
                _trayIcon.ToolTipText = "Hardware Monitor";
                return;
            }

            var value = sensorValue.Value;
            string text;
            string tooltip;
            SKColor color;

            switch (mode)
            {
                case TrayIconMode.CpuTemp:
                    text = $"{value:F0}";
                    tooltip = $"CPU: {value:F0}°C";
                    color = TempToColor(value);
                    break;
                case TrayIconMode.GpuTemp:
                    text = $"{value:F0}";
                    tooltip = $"GPU: {value:F0}°C";
                    color = TempToColor(value);
                    break;
                case TrayIconMode.CpuLoad:
                    text = $"{value:F0}";
                    tooltip = $"CPU: {value:F0}%";
                    color = LoadToColor(value);
                    break;
                case TrayIconMode.GpuLoad:
                    text = $"{value:F0}";
                    tooltip = $"GPU: {value:F0}%";
                    color = LoadToColor(value);
                    break;
                case TrayIconMode.CustomSensor:
                    text = $"{value:F0}";
                    tooltip = $"Sensor: {value:F0}";
                    color = new SKColor(0, 255, 236); // teal
                    break;
                default:
                    text = "HM";
                    tooltip = "Hardware Monitor";
                    color = SKColors.White;
                    break;
            }

            _trayIcon.Icon = CreateTextIcon(text, color);
            _trayIcon.ToolTipText = tooltip;
        }
        catch
        {
            // Silently handle icon rendering failures
        }
    }

    /// <summary>
    /// Render a text string as a 16x16 (or 32x32 for HiDPI) tray icon.
    /// </summary>
    private WindowIcon CreateTextIcon(string text, SKColor color)
    {
        const int size = 32; // Render at 2x for sharpness
        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        using var paint = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Inter", SKFontStyle.Bold),
        };

        // Auto-size: 1-2 chars → larger, 3+ chars → smaller
        paint.TextSize = text.Length <= 2 ? 26 : 20;

        // Center vertically
        var textBounds = new SKRect();
        paint.MeasureText(text, ref textBounds);
        float y = size / 2f - textBounds.MidY;

        canvas.DrawText(text, size / 2f, y, paint);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        var stream = new MemoryStream(data.ToArray());
        _currentIcon = new WindowIcon(stream);
        return _currentIcon;
    }

    private static SKColor TempToColor(float temp)
    {
        if (temp < 50) return new SKColor(0, 255, 236);      // teal — cool
        if (temp < 70) return new SKColor(255, 164, 44);      // orange — warm
        return new SKColor(255, 82, 82);                       // red — hot
    }

    private static SKColor LoadToColor(float load)
    {
        if (load < 50) return new SKColor(0, 255, 236);       // teal — low
        if (load < 80) return new SKColor(255, 164, 44);      // orange — medium
        return new SKColor(255, 82, 82);                       // red — high
    }

    public void SetVisible(bool visible)
    {
        if (_trayIcon != null)
            _trayIcon.IsVisible = visible;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_trayIcon != null)
        {
            _trayIcon.IsVisible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
        _currentIcon = null;
    }
}
