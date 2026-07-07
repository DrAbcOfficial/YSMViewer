using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using YSMViewer.Desktop.Rendering.Aura3D;
using YSMViewer.Desktop.Views;
using YSMViewer.Services;

namespace YSMViewer.Desktop;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

        if (args.Length > 0)
            App.StartupFilePath = args[0];

        var services = new ServiceCollection();
        services.AddPlatformLogging();
        services.AddYsmViewerServices();
        services.AddSingleton<YSMViewer.Rendering.IRenderer, Aura3DRenderer>();
        App.Services = services.BuildServiceProvider();
        ServiceCollectionExtensions.InitializeLogging(App.Services);

        App.CreateDesktopMainView = vm => new MainWindow { DataContext = vm };
        App.CreateBrowserMainView = null;

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}