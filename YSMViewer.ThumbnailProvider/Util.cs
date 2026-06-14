using System.Diagnostics;

namespace YSMViewer.ThumbnailProvider;

internal sealed class Util
{
    [Conditional("DEBUG")]
    internal static void Log(string msg)
    {
        try
        {
            var logPath = Path.Combine(Path.GetTempPath(), "YsmThumbnail.log");
            File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss.fff} [{Environment.ProcessId}] {msg}{Environment.NewLine}");
        }
        catch { }
    }
}
