using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace HardwareMonitor.Controls;

/// <summary>
/// A TextBlock that smoothly interpolates its displayed numeric value
/// when the target changes, using quartic ease-out for natural deceleration.
/// </summary>
public class AnimatedValueTextBlock : TextBlock
{
    public static readonly StyledProperty<double> TargetValueProperty =
        AvaloniaProperty.Register<AnimatedValueTextBlock, double>(nameof(TargetValue));

    public static readonly StyledProperty<string> ValueFormatProperty =
        AvaloniaProperty.Register<AnimatedValueTextBlock, string>(nameof(ValueFormat), "F0");

    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<AnimatedValueTextBlock, string>(nameof(Unit), "");

    public double TargetValue
    {
        get => GetValue(TargetValueProperty);
        set => SetValue(TargetValueProperty, value);
    }

    public string ValueFormat
    {
        get => GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    public string Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    private double _displayValue;
    private double _startValue;
    private double _endValue;
    private long _animStartTick;
    private const long AnimDurationTicks = 3_500_000; // 350ms in ticks
    private bool _animating;
    private bool _initialized;
    private DispatcherTimer? _timer;

    static AnimatedValueTextBlock()
    {
        TargetValueProperty.Changed.AddClassHandler<AnimatedValueTextBlock>(
            (ctrl, _) => ctrl.OnTargetValueChanged());
    }

    private void OnTargetValueChanged()
    {
        var target = TargetValue;

        if (!_initialized)
        {
            _initialized = true;
            _displayValue = target;
            _endValue = target;
            UpdateText();
            return;
        }

        // Skip animation for tiny changes (jitter)
        if (Math.Abs(target - _endValue) < 0.05)
            return;

        _startValue = _displayValue;
        _endValue = target;
        _animStartTick = DateTime.UtcNow.Ticks;

        if (!_animating)
        {
            _animating = true;
            _timer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnTick);
            _timer.Start();
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var elapsed = DateTime.UtcNow.Ticks - _animStartTick;
        var t = Math.Min((double)elapsed / AnimDurationTicks, 1.0);

        // Quartic ease-out: 1 - (1-t)^4
        var t1 = 1.0 - t;
        t = 1.0 - (t1 * t1 * t1 * t1);

        _displayValue = _startValue + (_endValue - _startValue) * t;
        UpdateText();

        if (t >= 1.0)
        {
            _animating = false;
            _timer!.Stop();
            _displayValue = _endValue;
            UpdateText();
        }
    }

    private void UpdateText()
    {
        Text = _displayValue.ToString(ValueFormat) + Unit;
    }
}
