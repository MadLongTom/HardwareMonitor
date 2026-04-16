using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using HardwareMonitor.ViewModels;

namespace HardwareMonitor;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    // DWM constants
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWCP_DONOTROUND = 1;

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        _viewModel.Initialize();

        Closing += OnWindowClosing;
        PropertyChanged += OnWindowPropertyChanged;

        // Disable Windows 11 rounded corners & set themed border
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            ApplyWindowsChrome();

        // Sidebar title bar drag area
        var titleBar = this.FindControl<Border>("TitleBar");
        if (titleBar != null)
        {
            titleBar.PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                    BeginMoveDrag(e);
            };
            titleBar.DoubleTapped += (s, e) => ToggleMaximize();
        }

        // Window control buttons
        var minBtn = this.FindControl<Button>("MinBtn");
        var maxBtn = this.FindControl<Button>("MaxBtn");
        var closeBtn = this.FindControl<Button>("CloseBtn");

        if (minBtn != null) minBtn.Click += (_, _) =>
        {
            if (App.Settings.MinimizeToTray)
                HideToTray();
            else
                WindowState = WindowState.Minimized;
        };
        if (maxBtn != null) maxBtn.Click += (_, _) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        if (closeBtn != null) closeBtn.Click += (_, _) => Close();
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (App.Settings.CloseToTray && !Equals(Tag, "ForceClose"))
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        _viewModel.Dispose();
        App.TrayService.Dispose();
        App.Settings.Save();
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty && WindowState == WindowState.Minimized && App.Settings.MinimizeToTray)
        {
            HideToTray();
        }
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    [SupportedOSPlatform("windows")]
    private void ApplyWindowsChrome()
    {
        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero) return;

        // Square corners (disable Win11 rounding)
        int cornerPref = DWMWCP_DONOTROUND;
        DwmSetWindowAttribute(handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

        // Border color: #2f006b → COLORREF 0x006B002F (0x00BBGGRR)
        int borderColor = 0x006B002F;
        DwmSetWindowAttribute(handle, DWMWA_BORDER_COLOR, ref borderColor, sizeof(int));
    }
}