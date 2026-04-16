using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HardwareMonitor.Services;

namespace HardwareMonitor;

public partial class App : Application
{
    public static AppSettings Settings { get; private set; } = null!;
    public static TrayIconService TrayService { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Settings = AppSettings.Load();
        TrayService = new TrayIconService();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;

            // Handle --minimized flag (auto-start launches with this)
            bool startMinimized = Settings.StartMinimized ||
                desktop.Args?.Contains("--minimized", StringComparer.OrdinalIgnoreCase) == true;

            // Initialize tray icon
            TrayService.Initialize();
            TrayService.ShowRequested += () =>
            {
                mainWindow.ShowInTaskbar = true;
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
            };
            TrayService.ExitRequested += () =>
            {
                // Force-close: bypass close-to-tray logic
                mainWindow.Tag = "ForceClose";
                desktop.Shutdown();
            };

            if (startMinimized && Settings.MinimizeToTray)
            {
                mainWindow.WindowState = WindowState.Minimized;
                mainWindow.ShowInTaskbar = false;
                mainWindow.Hide();
            }

            // Keep app alive when main window is hidden (tray mode)
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        base.OnFrameworkInitializationCompleted();
    }
}