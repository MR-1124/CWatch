using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using CWatch.Analysis.Classifiers;
using CWatch.Core.Enums;
using CWatch.Core.Models;

namespace CWatch.UI.Converters;

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
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long delta)
        {
            if (parameter is string paramStr)
            {
                if (paramStr == "bg")
                {
                    string hex = delta > 0 ? "#3D1416" : delta < 0 ? "#14381E" : "#1C1E26";
                    return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
                }
                if (paramStr == "fg")
                {
                    string hex = delta > 0 ? "#EF4444" : delta < 0 ? "#22C55E" : "#A1A1AA";
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
                SafetyLevel.Safe => "#10B981",       // Emerald Green
                SafetyLevel.LowRisk => "#06B6D4",    // Cyan
                SafetyLevel.Review => "#F59E0B",     // Amber
                SafetyLevel.Dangerous => "#EF4444",  // Red
                _ => "#6B7280"                       // Gray
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

        // > 10 GB: Emergency Red, > 2 GB: Safety Orange, > 500 MB: Amber, Else: White
        string hex = bytes switch
        {
            >= 10L * 1024 * 1024 * 1024 => "#EF4444",
            >= 2L * 1024 * 1024 * 1024 => "#FF5722",
            >= 500L * 1024 * 1024 => "#F59E0B",
            _ => "#F4F4F6"
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
            >= 80 => "#10B981", // Emerald Green
            >= 50 => "#F59E0B", // Amber
            _ => "#EF4444"      // Red
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
            >= 85 => "EXCELLENT",
            >= 70 => "GOOD",
            >= 50 => "ATTENTION REQUIRED",
            _ => "CRITICAL CAPACITY"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? Visibility.Collapsed : Visibility.Visible;
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
