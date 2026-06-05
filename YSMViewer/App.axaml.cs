using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Diagnostics.CodeAnalysis;
using YSMViewer.ViewModels;
using YSMViewer.Views;

namespace YSMViewer;

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "ViewLocator reflection is required for Avalonia MVVM")]
public partial class App : Application
{
    public static string? StartupFilePath { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var vm = new MainViewModel();

        if (StartupFilePath is not null)
        {
            vm.StartupFilePath = StartupFilePath;
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = vm,
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
        {
            single.MainView = new MainView
            {
                DataContext = vm,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
