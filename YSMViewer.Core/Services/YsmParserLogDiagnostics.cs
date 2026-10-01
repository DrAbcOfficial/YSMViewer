using Microsoft.Extensions.Logging;
using YSMParser.Core.Diagnostics;

namespace YSMViewer.Services;

/// <summary>
/// Bridges <c>YSMParser.Core</c> verbose/warn output into the viewer's log,
/// so warnings the parser would otherwise swallow surface during parsing.
/// </summary>
public sealed class YsmParserLogDiagnostics : IParseDiagnostics
{
    public static readonly YsmParserLogDiagnostics Instance = new();

    private static readonly ILogger Logger = YsmLog.For(nameof(YsmParserLogDiagnostics));

    private YsmParserLogDiagnostics()
    {
    }

    public void Verbose(string message) => Logger.LogDebug("{ParserMessage}", message);

    public void Warn(string message) => Logger.LogWarning("{ParserMessage}", message);
}
