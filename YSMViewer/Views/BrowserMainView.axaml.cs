using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Svg.Skia;
using Svg.Model;
using YSMViewer.Rendering;
using YSMViewer.Services;
using YSMViewer.ViewModels;

namespace YSMViewer.Views;

public partial class BrowserMainView : UserControl
{
    private static readonly string[] ThemeSvgPaths =
    [
        "avares://YSMViewer/Assets/svg/mode-system.svg",
        "avares://YSMViewer/Assets/svg/mode-light.svg",
        "avares://YSMViewer/Assets/svg/mode-dark.svg",
    ];

    public BrowserMainView()
    {
        InitializeComponent();
        Loaded += OnLoaded;

        ThemeService.Instance.ModeChanged += OnThemeChanged;
        UpdateThemeIcon();
    }

    private void OnThemeChanged(AppThemeMode mode)
    {
        UpdateThemeIcon();
        ApplyAllSvgColors();
    }

    private void UpdateThemeIcon()
    {
        var img = this.FindControl<Image>("ThemeSvgImage");
        if (img is null) return;

        var mode = ThemeService.Instance.CurrentMode;
        LoadSvgWithColor(img, ThemeSvgPaths[(int)mode]);
    }

    private void ApplyAllSvgColors()
    {
        var paths = new (string Name, string Path)[]
        {
            ("LangSvgImage", "avares://YSMViewer/Assets/svg/lang.svg"),
            ("ThemeSvgImage", ThemeSvgPaths[(int)ThemeService.Instance.CurrentMode]),
            ("GitHubSvgImage", "avares://YSMViewer/Assets/svg/github.svg"),
            ("CameraFrontImg", "avares://YSMViewer/Assets/svg/up-junction.svg"),
            ("CameraTopImg", "avares://YSMViewer/Assets/svg/down-junction.svg"),
        };

        foreach (var (name, path) in paths)
        {
            var img = this.FindControl<Image>(name);
            if (img is not null)
                LoadSvgWithColor(img, path);
        }
    }

    private static void LoadSvgWithColor(Image image, string svgPath)
    {
        var color = ThemeService.Instance.IsDarkTheme() ? "#8b949e" : "#656d76";
        try
        {
            var source = SvgSource.Load(svgPath, new Uri("avares://YSMViewer/"));
            source.ReLoad(new SvgParameters(null, $":root {{ color: {color}; }}"));
            image.Source = new SvgImage { Source = source };
        }
        catch { }
    }

    private void OnThemeToggleClick(object? sender, RoutedEventArgs e)
    {
        ThemeService.Instance.CycleTheme();
    }

    private async void OnGitHubClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;
        await topLevel.Launcher.LaunchUriAsync(new Uri("https://github.com/DrAbcOfficial/YSMViewer"));
    }

    private void OnLanguageButtonClick(object? sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();

        var enIcon = new Image { Width = 18, Height = 18 };
        LoadSvgWithColor(enIcon, "avares://YSMViewer/Assets/svg/lang-en.svg");
        var enItem = new MenuItem { Header = "English", Icon = enIcon };
        enItem.Click += (_, _) =>
        {
            LocalizationService.Instance.SetLanguage("en");
            menu.Close();
        };
        menu.Items.Add(enItem);

        var zhIcon = new Image { Width = 18, Height = 18 };
        LoadSvgWithColor(zhIcon, "avares://YSMViewer/Assets/svg/lang-cn.svg");
        var zhItem = new MenuItem { Header = "中文", Icon = zhIcon };
        zhItem.Click += (_, _) =>
        {
            LocalizationService.Instance.SetLanguage("zh");
            menu.Close();
        };
        menu.Items.Add(zhItem);

        menu.Open(sender as Control ?? this);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        ApplyAllSvgColors();

        if (DataContext is MainViewModel vm)
        {
            _ = vm.LoadStartupFileIfNeeded();
        }
    }

    private async void OnOpenButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storage) return;

        var files = await storage.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open YSM Model",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("YSM Models")
                    {
                        Patterns = ["*.ysm"],
                    },
                ],
            });

        if (files is not { Count: > 0 }) return;

        await using var stream = await files[0].OpenReadAsync();
        using var ms = new System.IO.MemoryStream();
        await stream.CopyToAsync(ms);
        await vm.LoadFromBytesAsync(ms.ToArray());
    }

    private void OnDismissErrorClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.HasError = false;
    }

    private async void OnCopyErrorClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard is not null)
            {
                var data = new Avalonia.Input.DataTransfer();
                data.Add(Avalonia.Input.DataTransferItem.CreateText(vm.ErrorDetail));
                await topLevel.Clipboard.SetDataAsync(data);
                vm.Notifications.Show("Copied to clipboard", NotificationType.Info, 2000);
            }
        }
    }

    private void OnCameraFrontClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.Renderer.SetCameraView(RenderCameraView.Front);
    }

    private void OnCameraSideClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.Renderer.SetCameraView(RenderCameraView.Side);
    }

    private void OnCameraTopClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.Renderer.SetCameraView(RenderCameraView.Top);
    }

    private void OnAutoRotateClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.Renderer is IAutoRotateRenderer rot)
        {
            rot.IsAutoRotating = !rot.IsAutoRotating;

            var text = this.FindControl<TextBlock>("AutoRotateText");
            if (text is not null)
                text.Text = rot.IsAutoRotating ? "Auto Rotate: ON" : "Auto Rotate: OFF";
        }
    }
}
