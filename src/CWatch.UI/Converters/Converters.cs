using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using CWatch.Analysis.Classifiers;
using CWatch.Core.Enums;
using CWatch.Core.Models;

namespace CWatch.UI.Converters;

/// <summary>
/// Maps drive capacity pressure to the instrument's state colors:
/// nominal below 80%, caution to the 90% line, critical past it.
/// </summary>
public sealed class CapacityPressureToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double pct = value is double d ? d : 0;
        string hex = pct switch
        {
            >= 90 => "#E0524A",
            >= 80 => "#E3A93C",
            _ => "#F2632B"
        };
        return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class SizeFormatterConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long bytes)
        {
            return ByteSizeFormatter.Format(bytes);
        }
        if (value is int intBytes)
        {
            return ByteSizeFormatter.Format(intBytes);
        }
        if (value is double dBytes)
        {
            return ByteSizeFormatter.Format((long)dBytes);
        }
        return "0 B";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class DeltaFormatterConverter : IValueConverter
{
    // Reads the live theme so delta chips stay legible in dark and light modes.
    private static bool IsDarkTheme()
    {
        if (Application.Current?.TryFindResource("TextPrimary") is SolidColorBrush brush)
        {
            // Light text on a dark canvas means dark theme (luma > mid-gray).
            var c = brush.Color;
            double luma = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
            return luma > 128;
        }
        return true;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long delta)
        {
            bool dark = IsDarkTheme();
            if (parameter is string paramStr)
            {
                if (paramStr == "bg")
                {
                    // Tinted fields, tuned per theme.
                    string hex = dark
                        ? (delta > 0 ? "#2E1614" : delta < 0 ? "#14301F" : "#1C202A")
                        : (delta > 0 ? "#F8E3DE" : delta < 0 ? "#DEF1E5" : "#EAEDF2");
                    return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
                }
                if (paramStr == "fg")
                {
                    string hex = dark
                        ? (delta > 0 ? "#E0524A" : delta < 0 ? "#3DB583" : "#9AA6B5")
                        : (delta > 0 ? "#B3362F" : delta < 0 ? "#1E8A5F" : "#6E7A8C");
                    return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
                }
            }
            return ByteSizeFormatter.FormatDelta(delta);
        }
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class SafetyLevelToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SafetyLevel safety)
        {
            string hex = safety switch
            {
                SafetyLevel.Safe => "#3DB583",       // Nominal
                SafetyLevel.LowRisk => "#4CC3E0",    // Velocity
                SafetyLevel.Review => "#E3A93C",     // Caution
                SafetyLevel.Dangerous => "#E0524A",  // Critical
                _ => "#677182"                       // Shadow mist
            };
            return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class CategoryColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is StorageCategoryType cat)
        {
            string hex = CategoryClassifier.GetCategoryColorHex(cat);
            return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        }
        if (value is string hexStr && !string.IsNullOrEmpty(hexStr))
        {
            try { return (SolidColorBrush)new BrushConverter().ConvertFromString(hexStr)!; } catch { }
        }
        return (SolidColorBrush)new BrushConverter().ConvertFromString("#FF5722")!;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class FileSizeTierColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        long bytes = 0;
        if (value is long l) bytes = l;
        else if (value is int i) bytes = i;

        // The larger the file, the closer to the red line.
        string hex = bytes switch
        {
            >= 10L * 1024 * 1024 * 1024 => "#E0524A",
            >= 2L * 1024 * 1024 * 1024 => "#F2632B",
            >= 500L * 1024 * 1024 => "#E3A93C",
            _ => "#F2F4F7"
        };
        return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class HealthScoreToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int score = 100;
        if (value is int i) score = i;
        else if (value is double d) score = (int)d;

        string hex = score switch
        {
            >= 80 => "#3DB583", // Nominal
            >= 50 => "#E3A93C", // Caution
            _ => "#E0524A"      // Critical
        };
        return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class HealthScoreToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int score = 100;
        if (value is int i) score = i;
        else if (value is double d) score = (int)d;

        return score switch
        {
            >= 85 => "Excellent",
            >= 70 => "Good",
            >= 50 => "Attention needed",
            _ => "Critical"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return false;
    }
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? Visibility.Collapsed : Visibility.Visible;
        }
        if (value is int count)
        {
            return count > 0 ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
