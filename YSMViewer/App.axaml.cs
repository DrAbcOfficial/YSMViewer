using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using System.Diagnostics.CodeAnalysis;
using YSMViewer.Rendering.Aura3D;
using YSMViewer.Rendering.ThreeJs;
using YSMViewer.Services;
using YSMViewer.ViewModels;
using YSMViewer.Views;

namespace YSMViewer;

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "ViewLocator reflection is required for Avalonia MVVM")]
public partial class App : Application
{
    public static string? StartupFilePath { get; set; }

    public static string? StartupFileUrl { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var interFont = "avares://Avalonia.Fonts.Inter#Inter";
        if (ApplicationLifetime is ISingleViewApplicationLifetime)
        {
            interFont +=
                ",avares://YSMViewer.Browser/Assets/fonts/NotoSansSC-Regular.ttf#Noto Sans SC" +
                ",avares://YSMViewer.Browser/Assets/fonts/NotoSansKR-Regular.ttf#Noto Sans KR" +
                ",avares://YSMViewer.Browser/Assets/fonts/NotoSansJP-Regular.ttf#Noto Sans JP" +
                ",avares://YSMViewer.Browser/Assets/fonts/NotoColorEmoji-Regular.ttf#Noto Color Emoji";
        }
        var fontFamily = new FontFamily(interFont);
        var fontStyle = new Style(x => x.OfType<TextBlock>());
        fontStyle.Setters.Add(new Setter(TextBlock.FontFamilyProperty, fontFamily));
        Application.Current!.Styles.Add(fontStyle);

        ThemeService.Instance.ApplyTheme();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var renderer = new Aura3DRenderer();
            var vm = new MainViewModel(renderer);

            if (StartupFilePath is not null)
                vm.StartupFilePath = StartupFilePath;

            if (StartupFileUrl is not null)
                vm.StartupFileUrl = StartupFileUrl;

            desktop.MainWindow = new MainWindow
            {
                DataContext = vm,
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
        {
            var renderer = new ThreeJsRenderer();
            var vm = new MainViewModel(renderer);

            if (StartupFilePath is not null)
                vm.StartupFilePath = StartupFilePath;

            if (StartupFileUrl is not null)
                vm.StartupFileUrl = StartupFileUrl;

            single.MainView = new BrowserMainView
            {
                DataContext = vm,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}