using Avalonia.Controls;
using Avalonia.Input;
using HardwareMonitor.ViewModels;

namespace HardwareMonitor;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        _viewModel.Initialize();
        Closing += (_, _) => _viewModel.Dispose();

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

        if (minBtn != null) minBtn.Click += (_, _) => WindowState = WindowState.Minimized;
        if (maxBtn != null) maxBtn.Click += (_, _) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        if (closeBtn != null) closeBtn.Click += (_, _) => Close();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
}