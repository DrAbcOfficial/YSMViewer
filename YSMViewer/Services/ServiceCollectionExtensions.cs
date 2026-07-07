using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YSMViewer.ViewModels;

namespace YSMViewer.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddYsmViewerServices(this IServiceCollection services)
    {
        services.AddSingleton<ThemeService>();
        services.AddSingleton<LocalizationService>();
        services.AddTransient<MainViewModel>();

        return services;
    }

    public static IServiceCollection AddPlatformLogging(this IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
        });
        return services;
    }

    public static void InitializeLogging(IServiceProvider services)
    {
        YsmLog.SetFactory(services.GetRequiredService<ILoggerFactory>());
    }
}
