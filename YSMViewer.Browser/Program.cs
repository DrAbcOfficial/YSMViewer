using Avalonia;
using Avalonia.Browser;
using System;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using YSMViewer;

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

        await JSHost.ImportAsync("YsmThreeRenderer", "../js/ysm-three-renderer.js");

        await BuildAvaloniaApp()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}
