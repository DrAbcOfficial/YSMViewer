using System.Text.RegularExpressions;

namespace YSMViewer;

public static partial class MinecraftFormatHelper
{
    [GeneratedRegex("§.", RegexOptions.CultureInvariant)]
    private static partial Regex FormatCodeRegex();

    public static string StripFormatting(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        return FormatCodeRegex().Replace(text, "");
    }
}
