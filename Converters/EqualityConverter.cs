using System;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace HardwareMonitor.Converters;

public class EqualityConverter : IValueConverter
{
    public object? CompareValue { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Equals(value, parameter);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter is string s && int.TryParse(s, out int result))
            return result;
        return Avalonia.Data.BindingOperations.DoNothing;
    }
}

public class ViewEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string v && parameter is string p && v == p;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Avalonia.Data.BindingOperations.DoNothing;
    }
}

/// <summary>Returns true if CurrentView starts with the given parameter string.</summary>
public class ViewStartsWithConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string v && parameter is string p && v.StartsWith(p, StringComparison.Ordinal);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Avalonia.Data.BindingOperations.DoNothing;
    }
}

/// <summary>Converts a percentage (0-100) to a color brush: green → yellow → red.</summary>
public class LoadToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double v = value switch
        {
            float f => f,
            double d => d,
            _ => 0
        };

        // Teal (#00ffec) at low, orange (#ffa42c) at mid, red (#ba3640) at high
        if (v < 50)
        {
            double t = v / 50.0;
            byte r = (byte)(0x00 + t * (0xff - 0x00));
            byte g = (byte)(0xff + t * (0xa4 - 0xff));
            byte b = (byte)(0xec + t * (0x2c - 0xec));
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }
        else
        {
            double t = (v - 50) / 50.0;
            byte r = (byte)(0xff + t * (0xba - 0xff));
            byte g = (byte)(0xa4 + t * (0x36 - 0xa4));
            byte b = (byte)(0x2c + t * (0x40 - 0x2c));
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}

/// <summary>Converts temp °C to a color brush.</summary>
public class TempToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s) return new SolidColorBrush(Color.Parse("#a5a29d"));

        // Extract the numeric part
        var numStr = new string(s.TakeWhile(c => c == '-' || c == '.' || char.IsDigit(c)).ToArray());
        if (!double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double temp))
            return new SolidColorBrush(Color.Parse("#a5a29d"));

        if (temp < 50) return new SolidColorBrush(Color.Parse("#00ffec")); // Teal = cool
        if (temp < 70) return new SolidColorBrush(Color.Parse("#ffa42c")); // Orange = warm
        return new SolidColorBrush(Color.Parse("#ba3640")); // Red = hot
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}

/// <summary>Returns a brush based on whether the current view matches the parameter.
/// Parameter format: "ViewName" for background, "ViewName|fg" for foreground.</summary>
public class NavActiveConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string currentView || parameter is not string param)
            return new SolidColorBrush(Colors.Transparent);
        var parts = param.Split('|');
        bool active = currentView == parts[0];
        if (parts.Length > 1 && parts[1] == "fg")
            return new SolidColorBrush(active ? Color.Parse("#ffffff") : Color.Parse("#a5a29d"));
        return new SolidColorBrush(active ? Color.Parse("#4f00b5") : Colors.Transparent);
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}

/// <summary>Returns a brush for interval button active state.
/// Parameter format: "interval" for background, "interval|fg" for foreground.</summary>
public class IntervalActiveConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int interval || parameter is not string param)
            return new SolidColorBrush(Color.Parse("#160231"));
        var parts = param.Split('|');
        if (!int.TryParse(parts[0], out int target))
            return new SolidColorBrush(Color.Parse("#160231"));
        bool active = interval == target;
        if (parts.Length > 1 && parts[1] == "fg")
            return new SolidColorBrush(active ? Color.Parse("#ffffff") : Color.Parse("#a5a29d"));
        return new SolidColorBrush(active ? Color.Parse("#4f00b5") : Color.Parse("#160231"));
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}
