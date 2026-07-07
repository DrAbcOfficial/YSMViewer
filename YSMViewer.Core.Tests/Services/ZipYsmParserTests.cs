using System.IO.Compression;
using System.Text;
using YSMViewer.Services;

namespace YSMViewer.Core.Tests.Services;

public sealed class ZipYsmParserTests
{
    [Fact]
    public void Parse_ValidZip_ExtractsEntries()
    {
        var zipBytes = CreateZip(new Dictionary<string, byte[]>
        {
            ["models/entity/model.json"] = Encoding.UTF8.GetBytes("{\"fake\":true}"),
            ["textures/entity/texture.png"] = [0x89, 0x50, 0x4E, 0x47, 0x00],
        });
        using var parser = new ZipYsmParser(zipBytes);

        parser.Parse();
        var resources = parser.GetResources();

        Assert.Single(resources.Models);
        Assert.Equal("models/entity/model.json", resources.Models[0].Name);
        Assert.Single(resources.Textures);
        Assert.Equal("textures/entity/texture.png", resources.Textures[0].Name);
    }

    [Fact]
    public void Parse_SkipsMacOsxEntries()
    {
        var zipBytes = CreateZip(new Dictionary<string, byte[]>
        {
            ["__MACOSX/._model.json"] = [0x00, 0x01],
            ["models/model.json"] = Encoding.UTF8.GetBytes("{\"valid\":true}"),
        });
        using var parser = new ZipYsmParser(zipBytes);

        parser.Parse();
        var resources = parser.GetResources();

        Assert.Single(resources.Models);
        Assert.Equal("models/model.json", resources.Models[0].Name);
    }

    [Fact]
    public void Peek_ReturnsInfoAndYsmJson()
    {
        var zipBytes = CreateZip(new Dictionary<string, byte[]>
        {
            ["info.json"] = Encoding.UTF8.GetBytes("{\"name\":\"Test\"}"),
            ["ysm.json"] = Encoding.UTF8.GetBytes("{\"version\":1}"),
        });
        using var parser = new ZipYsmParser(zipBytes);

        var peek = parser.Peek();

        Assert.NotNull(peek.InfoJson);
        Assert.Contains("\"name\""u8, peek.InfoJson);
        Assert.NotNull(peek.YsmJson);
    }

    [Fact]
    public void GetResources_ClassifiesAnimations()
    {
        var zipBytes = CreateZip(new Dictionary<string, byte[]>
        {
            ["animations/walk.animation.json"] = Encoding.UTF8.GetBytes("{\"loop\":true}"),
        });
        using var parser = new ZipYsmParser(zipBytes);

        parser.Parse();
        var resources = parser.GetResources();

        Assert.Single(resources.Animations);
        Assert.Equal("animations/walk.animation.json", resources.Animations[0].Name);
    }

    [Fact]
    public void GetResources_Empty_ReturnsEmpty()
    {
        var zipBytes = CreateZip([]);
        using var parser = new ZipYsmParser(zipBytes);

        parser.Parse();
        var resources = parser.GetResources();

        Assert.Empty(resources.Models);
        Assert.Empty(resources.Textures);
    }

    private static byte[] CreateZip(Dictionary<string, byte[]> entries)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var (name, data) in entries)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
                using var stream = entry.Open();
                stream.Write(data, 0, data.Length);
            }
        }
        return ms.ToArray();
    }
}
