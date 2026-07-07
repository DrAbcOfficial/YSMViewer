using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace YSMViewer;

public static class YsmLog
{
    private static ILoggerFactory? _factory;

    public static void SetFactory(ILoggerFactory factory)
    {
        _factory = factory;
    }

    public static ILogger<T> For<T>() =>
        _factory?.CreateLogger<T>() ?? NullLogger<T>.Instance;

    public static ILogger For(string category) =>
        _factory?.CreateLogger(category) ?? NullLogger.Instance;
}
