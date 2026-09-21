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
            // "Pressure Gauge" — Dark: an instrument panel in low light.
            // Surfaces are blue-slate ink; the signal orange is reserved for pressure,
            // never decoration. Numbers stay white; color carries state, not emphasis.
            res["BgCanvas"] = new SolidColorBrush(Color.FromRgb(0x0C, 0x0E, 0x12));         // Ink
            res["BgSidebar"] = new SolidColorBrush(Color.FromRgb(0x0A, 0x0C, 0x10));        // Ink, recessed
            res["BgCard"] = new SolidColorBrush(Color.FromRgb(0x15, 0x19, 0x22));           // Panel
            res["BgCardSecondary"] = new SolidColorBrush(Color.FromRgb(0x1A, 0x1F, 0x2A));  // Panel, raised
            res["BgCardNested"] = new SolidColorBrush(Color.FromRgb(0x11, 0x14, 0x1C));     // Panel, recessed
            res["BgInput"] = new SolidColorBrush(Color.FromRgb(0x0E, 0x11, 0x17));          // Well

            res["BorderSubtle"] = new SolidColorBrush(Color.FromRgb(0x25, 0x2B, 0x38));     // Hairline
            res["BorderLight"] = new SolidColorBrush(Color.FromRgb(0x33, 0x3B, 0x4C));      // Outline
            res["BorderHover"] = new SolidColorBrush(Color.FromRgb(0x4A, 0x56, 0x6E));      // Active
            res["BorderAccent"] = new SolidColorBrush(Color.FromRgb(0xF2, 0x63, 0x2B));     // Signal

            res["TextPrimary"] = new SolidColorBrush(Color.FromRgb(0xF2, 0xF4, 0xF7));      // Paper
            res["TextSecondary"] = new SolidColorBrush(Color.FromRgb(0x9A, 0xA6, 0xB5));    // Mist
            res["TextMuted"] = new SolidColorBrush(Color.FromRgb(0x67, 0x71, 0x82));        // Shadow Mist

            res["AccentOrange"] = new SolidColorBrush(Color.FromRgb(0xF2, 0x63, 0x2B));     // Signal (pressure)
            res["AccentGreen"] = new SolidColorBrush(Color.FromRgb(0x3D, 0xB5, 0x83));      // Nominal
            res["AccentCyan"] = new SolidColorBrush(Color.FromRgb(0x4C, 0xC3, 0xE0));       // Velocity
            res["AccentAmber"] = new SolidColorBrush(Color.FromRgb(0xE3, 0xA9, 0x3C));      // Caution
            res["AccentRed"] = new SolidColorBrush(Color.FromRgb(0xE0, 0x52, 0x4A));        // Critical
            res["AccentBlue"] = new SolidColorBrush(Color.FromRgb(0x51, 0x8C, 0xE0));       // System

            res["DataGridHeaderBg"] = new SolidColorBrush(Color.FromRgb(0x11, 0x14, 0x1C));
            res["DataGridRowAlt"] = new SolidColorBrush(Color.FromRgb(0x17, 0x1B, 0x25));
            res["DataGridRowHover"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x24, 0x31));
            res["DataGridSelected"] = new SolidColorBrush(Color.FromRgb(0x26, 0x2E, 0x40));
            res["ScrollbarThumb"] = new SolidColorBrush(Color.FromRgb(0x2B, 0x32, 0x42));
        }
        else
        {
            // "Pressure Gauge" — Light: chalk-white instrument face, inked markings.
            res["BgCanvas"] = new SolidColorBrush(Color.FromRgb(0xF2, 0xF4, 0xF7));         // Paper
            res["BgSidebar"] = new SolidColorBrush(Color.FromRgb(0xE9, 0xEC, 0xF1));        // Paper, recessed
            res["BgCard"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));           // Panel
            res["BgCardSecondary"] = new SolidColorBrush(Color.FromRgb(0xF8, 0xF9, 0xFB));  // Panel, raised
            res["BgCardNested"] = new SolidColorBrush(Color.FromRgb(0xEE, 0xF1, 0xF5));     // Panel, recessed
            res["BgInput"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));          // Well

            res["BorderSubtle"] = new SolidColorBrush(Color.FromRgb(0xD7, 0xDD, 0xE6));     // Hairline
            res["BorderLight"] = new SolidColorBrush(Color.FromRgb(0xC3, 0xCC, 0xDA));      // Outline
            res["BorderHover"] = new SolidColorBrush(Color.FromRgb(0x8F, 0x9D, 0xB2));      // Active
            res["BorderAccent"] = new SolidColorBrush(Color.FromRgb(0xC7, 0x48, 0x14));     // Signal, deepened

            res["TextPrimary"] = new SolidColorBrush(Color.FromRgb(0x14, 0x1B, 0x26));      // Ink
            res["TextSecondary"] = new SolidColorBrush(Color.FromRgb(0x4A, 0x56, 0x69));    // Mist
            res["TextMuted"] = new SolidColorBrush(Color.FromRgb(0x6E, 0x7A, 0x8C));        // Shadow Mist

            res["AccentOrange"] = new SolidColorBrush(Color.FromRgb(0xC7, 0x48, 0x14));     // Signal, deepened for white text
            res["AccentGreen"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x8A, 0x5F));      // Nominal
            res["AccentCyan"] = new SolidColorBrush(Color.FromRgb(0x0E, 0x87, 0xA8));       // Velocity
            res["AccentAmber"] = new SolidColorBrush(Color.FromRgb(0xA8, 0x73, 0x14));      // Caution
            res["AccentRed"] = new SolidColorBrush(Color.FromRgb(0xB3, 0x36, 0x2F));        // Critical
            res["AccentBlue"] = new SolidColorBrush(Color.FromRgb(0x2D, 0x63, 0xC4));       // System

            res["DataGridHeaderBg"] = new SolidColorBrush(Color.FromRgb(0xE9, 0xEC, 0xF1));
            res["DataGridRowAlt"] = new SolidColorBrush(Color.FromRgb(0xF8, 0xF9, 0xFB));
            res["DataGridRowHover"] = new SolidColorBrush(Color.FromRgb(0xE4, 0xE9, 0xF0));
            res["DataGridSelected"] = new SolidColorBrush(Color.FromRgb(0xD8, 0xE2, 0xF0));
            res["ScrollbarThumb"] = new SolidColorBrush(Color.FromRgb(0xC3, 0xCC, 0xDA));
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
