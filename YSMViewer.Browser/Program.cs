using Avalonia;
using Avalonia.Browser;
using System;
using System.Threading.Tasks;
using YSMViewer;

internal sealed partial class Program
{
    private static Task Main(string[] args)
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

        return BuildAvaloniaApp()
            // TODO: This suck, all utf-8 char turn into mess
            // Use custom font instead
            //.WithInterFont()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}
