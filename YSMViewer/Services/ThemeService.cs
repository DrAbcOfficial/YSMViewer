using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace YSMViewer.Services;

public enum AppThemeMode
{
    System,
    Light,
    Dark,
}

public sealed class ThemeService
{
    private static ThemeService? _instance;
    public static ThemeService Instance => _instance ??= new ThemeService();

    private AppThemeMode _currentMode = AppThemeMode.Dark;
    public AppThemeMode CurrentMode
    {
        get => _currentMode;
        private set
        {
            if (_currentMode == value) return;
            _currentMode = value;
            ApplyTheme();
            ModeChanged?.Invoke(value);
        }
    }

    public event Action<AppThemeMode>? ModeChanged;

    private ThemeService() { }

    public void CycleTheme()
    {
        CurrentMode = CurrentMode switch
        {
            AppThemeMode.Dark => AppThemeMode.Light,
            AppThemeMode.Light => AppThemeMode.System,
            _ => AppThemeMode.Dark,
        };
    }

    public void SetTheme(AppThemeMode mode)
    {
        CurrentMode = mode;
    }

    public void ApplyTheme()
    {
        if (Application.Current is null) return;

        var requested = _currentMode switch
        {
            AppThemeMode.Light => ThemeVariant.Light,
            AppThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };

        Application.Current.RequestedThemeVariant = requested;

        var resources = Application.Current.Resources;
        resources.MergedDictionaries.Clear();

        bool isDark = requested == ThemeVariant.Dark || requested == ThemeVariant.Default;

        resources.MergedDictionaries.Add(isDark ? CreateDarkPalette() : CreateLightPalette());
    }

    public bool IsDarkTheme()
    {
        if (Application.Current is null) return true;

        return _currentMode switch
        {
            AppThemeMode.Light => false,
            AppThemeMode.Dark => true,
            _ => Application.Current.ActualThemeVariant == ThemeVariant.Dark,
        };
    }

    public byte[] GetViewportBackgroundColor()
    {
        return IsDarkTheme()
            ? [255, 13, 17, 23]
            : [255, 240, 242, 245];
    }

    private static ResourceDictionary CreateDarkPalette() => new ResourceDictionary
    {
        ["ThemeBgViewport"] = Avalonia.Media.Color.FromRgb(0x0d, 0x11, 0x17),
        ["ThemeBgSurface"] = Avalonia.Media.Color.FromRgb(0x16, 0x1b, 0x22),
        ["ThemeBgElevated"] = Avalonia.Media.Color.FromRgb(0x1c, 0x23, 0x33),
        ["ThemeBgHover"] = Avalonia.Media.Color.FromRgb(0x22, 0x2d, 0x3d),
        ["ThemeBgActive"] = Avalonia.Media.Color.FromRgb(0x2a, 0x3a, 0x55),
        ["ThemeBgButton"] = Avalonia.Media.Color.FromRgb(0x22, 0x2d, 0x3d),
        ["ThemeBgItemRow"] = Avalonia.Media.Color.FromRgb(0x1c, 0x23, 0x33),
        ["ThemeBgCard"] = Avalonia.Media.Color.FromRgb(0x1c, 0x23, 0x33),
        ["ThemeTextPrimary"] = Avalonia.Media.Color.FromRgb(0xe6, 0xed, 0xf3),
        ["ThemeTextSecondary"] = Avalonia.Media.Color.FromRgb(0x8b, 0x94, 0x9e),
        ["ThemeTextMuted"] = Avalonia.Media.Color.FromRgb(0x48, 0x4f, 0x58),
        ["ThemeTextLabel"] = Avalonia.Media.Color.FromRgb(0x6e, 0x76, 0x81),
        ["ThemeAccent"] = Avalonia.Media.Color.FromRgb(0x58, 0xa6, 0xff),
        ["ThemeAccentGreen"] = Avalonia.Media.Color.FromRgb(0x3f, 0xb9, 0x50),
        ["ThemeAccentRed"] = Avalonia.Media.Color.FromRgb(0xf8, 0x51, 0x49),
        ["ThemeAccentYellow"] = Avalonia.Media.Color.FromRgb(0xe3, 0xb3, 0x41),
        ["ThemeBorder"] = Avalonia.Media.Color.FromRgb(0x30, 0x36, 0x3d),
        ["ThemeToggleChecked"] = Avalonia.Media.Color.FromRgb(0x1a, 0x3a, 0x2a),
        ["ThemeToggleUnchecked"] = Avalonia.Media.Color.FromRgb(0x3d, 0x1a, 0x1a),
        ["ThemeToggleDefault"] = Avalonia.Media.Colors.Transparent,
        ["ThemeErrorBg"] = Avalonia.Media.Color.FromRgb(0x2d, 0x15, 0x20),
        ["ThemeErrorText"] = Avalonia.Media.Color.FromRgb(0xf8, 0x51, 0x49),
        ["ThemeTabActiveBg"] = Avalonia.Media.Color.FromRgb(0x1c, 0x23, 0x33),
        ["ThemeTabHoverBg"] = Avalonia.Media.Color.FromRgb(0x22, 0x2d, 0x3d),
        ["ThemeTreeArrow"] = Avalonia.Media.Color.FromRgb(0x58, 0xa6, 0xff),
        ["ThemeAnimSelected"] = Avalonia.Media.Color.FromRgb(0x1a, 0x30, 0x50),
        ["ThemeAnimTransport"] = Avalonia.Media.Color.FromRgb(0x16, 0x1b, 0x22),
        ["ThemeDivider"] = Avalonia.Media.Color.FromRgb(0x21, 0x26, 0x2d),
    };

    private static ResourceDictionary CreateLightPalette() => new ResourceDictionary
    {
        ["ThemeBgViewport"] = Avalonia.Media.Color.FromRgb(0xf0, 0xf2, 0xf5),
        ["ThemeBgSurface"] = Avalonia.Media.Color.FromRgb(0xff, 0xff, 0xff),
        ["ThemeBgElevated"] = Avalonia.Media.Color.FromRgb(0xf6, 0xf8, 0xfa),
        ["ThemeBgHover"] = Avalonia.Media.Color.FromRgb(0xea, 0xee, 0xf2),
        ["ThemeBgActive"] = Avalonia.Media.Color.FromRgb(0xda, 0xe0, 0xe8),
        ["ThemeBgButton"] = Avalonia.Media.Color.FromRgb(0xea, 0xee, 0xf2),
        ["ThemeBgItemRow"] = Avalonia.Media.Color.FromRgb(0xf6, 0xf8, 0xfa),
        ["ThemeBgCard"] = Avalonia.Media.Color.FromRgb(0xf6, 0xf8, 0xfa),
        ["ThemeTextPrimary"] = Avalonia.Media.Color.FromRgb(0x1f, 0x23, 0x28),
        ["ThemeTextSecondary"] = Avalonia.Media.Color.FromRgb(0x65, 0x6d, 0x76),
        ["ThemeTextMuted"] = Avalonia.Media.Color.FromRgb(0xa1, 0xa9, 0xb3),
        ["ThemeTextLabel"] = Avalonia.Media.Color.FromRgb(0x80, 0x88, 0x93),
        ["ThemeAccent"] = Avalonia.Media.Color.FromRgb(0x09, 0x69, 0xd6),
        ["ThemeAccentGreen"] = Avalonia.Media.Color.FromRgb(0x1a, 0x7f, 0x37),
        ["ThemeAccentRed"] = Avalonia.Media.Color.FromRgb(0xcf, 0x22, 0x2e),
        ["ThemeAccentYellow"] = Avalonia.Media.Color.FromRgb(0x9a, 0x67, 0x04),
        ["ThemeBorder"] = Avalonia.Media.Color.FromRgb(0xd0, 0xd7, 0xde),
        ["ThemeToggleChecked"] = Avalonia.Media.Color.FromRgb(0xae, 0xda, 0xc0),
        ["ThemeToggleUnchecked"] = Avalonia.Media.Color.FromRgb(0xef, 0xb1, 0xb5),
        ["ThemeToggleDefault"] = Avalonia.Media.Colors.Transparent,
        ["ThemeErrorBg"] = Avalonia.Media.Color.FromRgb(0xff, 0xee, 0xf0),
        ["ThemeErrorText"] = Avalonia.Media.Color.FromRgb(0xcf, 0x22, 0x2e),
        ["ThemeTabActiveBg"] = Avalonia.Media.Color.FromRgb(0xf6, 0xf8, 0xfa),
        ["ThemeTabHoverBg"] = Avalonia.Media.Color.FromRgb(0xea, 0xee, 0xf2),
        ["ThemeTreeArrow"] = Avalonia.Media.Color.FromRgb(0x09, 0x69, 0xd6),
        ["ThemeAnimSelected"] = Avalonia.Media.Color.FromRgb(0xdc, 0xe8, 0xf9),
        ["ThemeAnimTransport"] = Avalonia.Media.Color.FromRgb(0xff, 0xff, 0xff),
        ["ThemeDivider"] = Avalonia.Media.Color.FromRgb(0xd0, 0xd7, 0xde),
    };
}