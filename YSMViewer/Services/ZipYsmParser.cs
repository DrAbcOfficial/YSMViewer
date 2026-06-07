using System.IO.Compression;
using System.Text;
using YSMParser.Core.Parsers;

namespace YSMViewer.Services;

public sealed class ZipYsmParser : YSMParser.Core.Parsers.YSMParser
{
    private readonly byte[] _buffer;
    private Dictionary<string, byte[]> _resources = [];

    public ZipYsmParser(byte[] buffer)
    {
        _buffer = buffer;
    }

    public override int GetYSGPVersion() => 0;

    public override YsmPeekResult Peek()
    {
        var resourceNames = new List<string>();
        byte[]? ysmJson = null;
        byte[]? infoJson = null;

        try
        {
            using var ms = new MemoryStream(_buffer);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase))
                    continue;

                resourceNames.Add(entry.FullName);

                if (string.Equals(entry.FullName, "ysm.json", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = entry.Open();
                    using var mem = new MemoryStream();
                    stream.CopyTo(mem);
                    ysmJson = mem.ToArray();
                }
                else if (string.Equals(entry.FullName, "info.json", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = entry.Open();
                    using var mem = new MemoryStream();
                    stream.CopyTo(mem);
                    infoJson = mem.ToArray();
                }
            }
        }
        catch { }

        return new YsmPeekResult(0, _buffer.Length, infoJson, ysmJson, resourceNames, null, null, null, null, null, null, null);
    }

    public override void Parse()
    {
        if (_buffer.Length == 0) return;

        var resources = new Dictionary<string, byte[]>();

        try
        {
            using var ms = new MemoryStream(_buffer);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase))
                    continue;

                using var stream = entry.Open();
                using var mem = new MemoryStream();
                stream.CopyTo(mem);
                resources[entry.FullName] = mem.ToArray();
            }
        }
        catch { }

        _resources = resources;
    }

    public override YsmResourceData GetResources()
    {
        var models = new List<YsmResourceEntry>();
        var textures = new List<YsmResourceEntry>();
        var animations = new List<YsmResourceEntry>();
        var animControllers = new List<YsmResourceEntry>();
        var sounds = new List<YsmResourceEntry>();
        var functions = new List<YsmResourceEntry>();
        var languages = new List<YsmResourceEntry>();
        var avatars = new List<YsmResourceEntry>();
        var backgrounds = new List<YsmResourceEntry>();
        var specialImages = new List<YsmResourceEntry>();
        byte[]? ysmJson = null;
        byte[]? infoJson = null;

        foreach (var (name, data) in _resources)
        {
            if (name.EndsWith(".animation.json", StringComparison.OrdinalIgnoreCase) ||
                (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                 (name.Contains("/animations/", StringComparison.OrdinalIgnoreCase) ||
                  name.Contains("\\animations\\", StringComparison.OrdinalIgnoreCase))))
                animations.Add(new(name, data));
            else if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                textures.Add(new(name, data));
            else if (name.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                sounds.Add(new(name, data));
            else if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(name, "ysm.json", StringComparison.OrdinalIgnoreCase))
                    ysmJson = data;
                else if (string.Equals(name, "info.json", StringComparison.OrdinalIgnoreCase))
                    infoJson = data;
                else if (name.Contains("animation_controller"))
                    animControllers.Add(new(name, data));
                else
                    models.Add(new(name, data));
            }
            else if (name.StartsWith("avatars/", StringComparison.OrdinalIgnoreCase))
                avatars.Add(new(name, data));
            else if (name.StartsWith("backgrounds/", StringComparison.OrdinalIgnoreCase))
                backgrounds.Add(new(name, data));
            else if (name.StartsWith("special/", StringComparison.OrdinalIgnoreCase) || name.Contains("specular"))
                specialImages.Add(new(name, data));
            else if (name.EndsWith(".mcfunction", StringComparison.OrdinalIgnoreCase))
                functions.Add(new(name, data));
            else if (name.EndsWith(".lang", StringComparison.OrdinalIgnoreCase))
                languages.Add(new(name, data));
        }

        return new YsmResourceData(
            models, textures, animations, animControllers, sounds, functions, languages,
            avatars, backgrounds, specialImages, infoJson, ysmJson);
    }

    public override byte[] GetDecryptedData() => _buffer;

    public override void SaveToDirectory(string outputDirectory)
    {
        if (_resources.Count == 0 && _buffer.Length > 0)
            Parse();

        Directory.CreateDirectory(outputDirectory);
        foreach (var (fileName, data) in _resources)
        {
            var filePath = Path.Combine(outputDirectory, fileName);
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllBytes(filePath, data);
        }
    }

    public override void PrintInfo(TextWriter output)
    {
        output.WriteLine($"  Version:      0 (Zip archive)");
        output.WriteLine($"  File size:    {_buffer.Length:N0} bytes");

        if (_resources.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("  Resources:");
            int index = 0;
            foreach (var (name, data) in _resources)
                output.WriteLine($"    [{++index}] {name}  ({data.Length:N0} bytes)");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing) return;
        _resources.Clear();
    }
}
