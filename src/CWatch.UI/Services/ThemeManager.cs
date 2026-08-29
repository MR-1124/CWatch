using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace CWatch.UI.Services;

public enum ThemeMode
{
    Dark,
    Light,
    System
}

public sealed class ThemeManager
{
    private static ThemeManager? _instance;
    public static ThemeManager Instance => _instance ??= new ThemeManager();

    private ThemeMode _currentMode = ThemeMode.Dark;
    public ThemeMode CurrentMode => _currentMode;

    public event Action<ThemeMode>? ThemeChanged;

    public void Initialize(ThemeMode initialMode = ThemeMode.Dark)
    {
        _currentMode = initialMode;
        ApplyTheme(_currentMode);

        SystemEvents.UserPreferenceChanged += (s, e) =>
        {
            if (_currentMode == ThemeMode.System)
            {
                Application.Current?.Dispatcher.Invoke(() => ApplyTheme(ThemeMode.System));
            }
        };
    }

    public void SetTheme(ThemeMode mode)
    {
        _currentMode = mode;
        ApplyTheme(mode);
        ThemeChanged?.Invoke(mode);
    }

    private void ApplyTheme(ThemeMode mode)
    {
        bool isDark = mode switch
        {
            ThemeMode.Dark => true,
            ThemeMode.Light => false,
            ThemeMode.System => IsWindowsInDarkMode(),
            _ => true
        };

        var res = Application.Current?.Resources;
        if (res == null) return;

        if (isDark)
        {
            // Nordic Precision Cockpit — Dark Theme
            res["BgCanvas"] = new SolidColorBrush(Color.FromRgb(0x0B, 0x0C, 0x10));         // Deep Obsidian Slate
            res["BgSidebar"] = new SolidColorBrush(Color.FromRgb(0x10, 0x12, 0x18));        // Dark Tactical Sidebar
            res["BgCard"] = new SolidColorBrush(Color.FromRgb(0x15, 0x18, 0x22));           // Panel Base
            res["BgCardSecondary"] = new SolidColorBrush(Color.FromRgb(0x1A, 0x1D, 0x2A));  // Secondary Panel
            res["BgCardNested"] = new SolidColorBrush(Color.FromRgb(0x12, 0x14, 0x1D));     // Nested Container
            res["BgInput"] = new SolidColorBrush(Color.FromRgb(0x0E, 0x10, 0x15));          // Monospace Input Background

            res["BorderSubtle"] = new SolidColorBrush(Color.FromRgb(0x23, 0x28, 0x38));     // Crisp Hairline Divider
            res["BorderLight"] = new SolidColorBrush(Color.FromRgb(0x32, 0x3A, 0x4E));      // Medium Outline
            res["BorderHover"] = new SolidColorBrush(Color.FromRgb(0x45, 0x51, 0x6E));      // Active Highlight

            res["TextPrimary"] = new SolidColorBrush(Color.FromRgb(0xF8, 0xF9, 0xFA));      // Titanium White
            res["TextSecondary"] = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));    // Cool Slate
            res["TextMuted"] = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));        // Muted Gray

            res["AccentOrange"] = new SolidColorBrush(Color.FromRgb(0xFF, 0x57, 0x22));     // Safety Orange
            res["AccentGreen"] = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));      // Calibrated Emerald
            res["AccentCyan"] = new SolidColorBrush(Color.FromRgb(0x06, 0xB6, 0xD4));       // Telemetry Cyan
            res["AccentAmber"] = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));      // Warning Amber
            res["AccentRed"] = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));        // Critical Red
            res["AccentBlue"] = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));       // Indigo Blue

            res["DataGridHeaderBg"] = new SolidColorBrush(Color.FromRgb(0x12, 0x14, 0x1D));
            res["DataGridRowAlt"] = new SolidColorBrush(Color.FromRgb(0x13, 0x16, 0x20));
            res["DataGridRowHover"] = new SolidColorBrush(Color.FromRgb(0x1C, 0x20, 0x2E));
            res["DataGridSelected"] = new SolidColorBrush(Color.FromRgb(0x24, 0x2A, 0x3C));
            res["ScrollbarThumb"] = new SolidColorBrush(Color.FromRgb(0x28, 0x2E, 0x40));
        }
        else
        {
            // Nordic Precision Chalk — Light Theme
            res["BgCanvas"] = new SolidColorBrush(Color.FromRgb(0xF1, 0xF3, 0xF6));         // Crisp Technical Chalk
            res["BgSidebar"] = new SolidColorBrush(Color.FromRgb(0xEA, 0xED, 0xF2));        // Soft Silver Sidebar
            res["BgCard"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));           // Pure White Panel
            res["BgCardSecondary"] = new SolidColorBrush(Color.FromRgb(0xF8, 0xF9, 0xFB));  // Clean Off-White
            res["BgCardNested"] = new SolidColorBrush(Color.FromRgb(0xEE, 0xF1, 0xF6));     // Nested Box
            res["BgInput"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));          // Crisp White Input

            res["BorderSubtle"] = new SolidColorBrush(Color.FromRgb(0xD5, 0xDC, 0xE7));     // Crisp Hairline
            res["BorderLight"] = new SolidColorBrush(Color.FromRgb(0xC4, 0xCE, 0xDC));      // Defined Border
            res["BorderHover"] = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));      // Hover Outline

            res["TextPrimary"] = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));      // Deep Navy Titanium
            res["TextSecondary"] = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));    // Slate Gray
            res["TextMuted"] = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));        // Muted Gray

            res["AccentOrange"] = new SolidColorBrush(Color.FromRgb(0xEA, 0x58, 0x0C));     // Warm Safety Orange
            res["AccentGreen"] = new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69));      // Forest Emerald
            res["AccentCyan"] = new SolidColorBrush(Color.FromRgb(0x08, 0x91, 0xB2));       // Deep Cyan
            res["AccentAmber"] = new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06));      // Amber
            res["AccentRed"] = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));        // Red
            res["AccentBlue"] = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));       // Royal Blue

            res["DataGridHeaderBg"] = new SolidColorBrush(Color.FromRgb(0xEA, 0xED, 0xF2));
            res["DataGridRowAlt"] = new SolidColorBrush(Color.FromRgb(0xF8, 0xF9, 0xFB));
            res["DataGridRowHover"] = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
            res["DataGridSelected"] = new SolidColorBrush(Color.FromRgb(0xDB, 0xE4, 0xF0));
            res["ScrollbarThumb"] = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
        }
    }

    private static bool IsWindowsInDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key != null)
            {
                object? val = key.GetValue("AppsUseLightTheme");
                if (val is int intVal)
                {
                    return intVal == 0; // 0 = Dark Mode, 1 = Light Mode
                }
            }
        }
        catch { }

        return true;
    }
}
