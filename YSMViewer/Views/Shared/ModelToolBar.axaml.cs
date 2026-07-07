using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Svg.Skia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Svg.Model;
using YSMViewer.Services;
using YSMViewer.ViewModels;

namespace YSMViewer.Views.Shared;

public partial class ModelToolBar : UserControl
{
    private static ThemeService ThemeSvc => App.Services.GetRequiredService<ThemeService>();
    private static LocalizationService Loc => App.Services.GetRequiredService<LocalizationService>();

    public ModelToolBar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        ApplyAllSvgColors();
        ThemeSvc.ModeChanged += OnThemeModeChanged;
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        ThemeSvc.ModeChanged -= OnThemeModeChanged;
    }

    private void OnThemeModeChanged(AppThemeMode mode) => ApplyAllSvgColors();

    private void ApplyAllSvgColors()
    {
        ApplySvgColor(LangSvgImage, "avares://YSMViewer/Assets/svg/lang.svg");
        ApplySvgColor(ThemeSvgImage, ThemeSvgPath());
        ApplySvgColor(GitHubSvgImage, "avares://YSMViewer/Assets/svg/github.svg");
    }

    private static string ThemeSvgPath() => $"avares://YSMViewer/Assets/svg/mode-{ThemeSvc.CurrentMode switch { AppThemeMode.Dark => "dark", AppThemeMode.System => "system", _ => "light" }}.svg";

    private static void ApplySvgColor(Image image, string svgPath)
    {
        var color = ThemeSvc.IsDarkTheme() ? "#8b949e" : "#656d76";
        try
        {
            var source = SvgSource.Load(svgPath, new Uri("avares://YSMViewer/"));
            source.ReLoad(new SvgParameters(null, $":root {{ color: {color}; }}"));
            image.Source = new SvgImage { Source = source };
        }
        catch (Exception ex)
        {
            YsmLog.For<ModelToolBar>().LogWarning(ex, "Failed to load SVG icon '{Path}'", svgPath);
        }
    }

    private async void OnOpenButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (vm.TryOpenPlatformFilePickerAsync is not null && await vm.TryOpenPlatformFilePickerAsync())
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open YSM Model",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("YSM/ZIP Models")
            {
                Patterns = ["*.ysm", "*.zip"],
                MimeTypes = ["application/vnd.ysm.model+encrypted", "application/zip", "application/x-zip-compressed"],
            }],
        });
        if (files is not { Count: > 0 }) return;
        await using var stream = await files[0].OpenReadAsync();
        using var ms = new System.IO.MemoryStream();
        await stream.CopyToAsync(ms);
        await vm.LoadFromBytesAsync(ms.ToArray());
    }

    private void OnThemeToggleClick(object? sender, RoutedEventArgs e)
    {
        ThemeSvc.CycleTheme();
    }

    private void OnGitHubClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        topLevel?.Launcher.LaunchUriAsync(new Uri("https://github.com/DrAbcOfficial/YSMViewer"));
    }

    private void OnLanguageButtonClick(object? sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        var enItem = new MenuItem { Header = "English" };
        enItem.Click += (_, _) => { Loc.SetLanguage("en"); menu.Close(); };
        menu.Items.Add(enItem);
        var zhItem = new MenuItem { Header = "中文" };
        zhItem.Click += (_, _) => { Loc.SetLanguage("zh"); menu.Close(); };
        menu.Items.Add(zhItem);
        menu.Open(sender as Control ?? this);
    }
}
