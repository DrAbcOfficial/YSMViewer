using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using YSMViewer.Desktop.Views;
using YSMViewer.Rendering.Aura3D;

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
        services.AddSingleton<YSMViewer.Rendering.IRenderer, Aura3DRenderer>();
        App.Services = services.BuildServiceProvider();

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