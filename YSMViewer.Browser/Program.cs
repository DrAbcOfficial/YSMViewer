using Avalonia;
using Avalonia.Browser;
using Microsoft.Extensions.DependencyInjection;
using YSMViewer;
using YSMViewer.Browser.Rendering.ThreeJs;
using YSMViewer.Browser.Views;

internal sealed partial class Program
{
    private static async Task Main(string[] args)
    {
        if (args.Length > 0 && !string.IsNullOrEmpty(args[0]))
        {
            try
            {
                var url = args[0];
                var queryIndex = url.IndexOf('?');
                if (queryIndex >= 0)
                {
                    var query = url[(queryIndex + 1)..];
                    foreach (var pair in query.Split('&'))
                    {
                        var kv = pair.Split('=', 2);
                        if (kv.Length == 2 && string.Equals(kv[0], "file", System.StringComparison.OrdinalIgnoreCase))
                        {
                            var fileValue = Uri.UnescapeDataString(kv[1]);
                            if (!string.IsNullOrEmpty(fileValue))
                                App.StartupFileUrl = fileValue;
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        var services = new ServiceCollection();
        services.AddSingleton<YSMViewer.Rendering.IRenderer, ThreeJsRenderer>();
        App.Services = services.BuildServiceProvider();

        App.CreateDesktopMainView = null;
        App.CreateBrowserMainView = vm => new BrowserMainView { DataContext = vm };

        await BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}