using System.IO.Compression;
using System.Text;
using YSMViewer.Services;

namespace YSMViewer.Core.Tests.Services;

/// <summary>
/// Plain-zip (zipver) container coverage on top of YSMParser.Core's YsmFile
/// API plus the viewer's <see cref="YsmLoaderService.SanitizeZipResources"/>
/// gap compensation. Since Core 1.1.1 the library itself filters macOS junk,
/// routes .webp to Textures and keeps animation controllers out of
/// Animations; these tests lock that end-to-end behavior through the
/// viewer's public entry points.
/// </summary>
public sealed class YsmFileZipContainerTests
{
    [Fact]
    public void Parse_ValidZip_ClassifiesEntries()
    {
        var zipBytes = CreateZip(new Dictionary<string, byte[]>
        {
            ["models/entity/model.json"] = Encoding.UTF8.GetBytes("{\"fake\":true}"),
            ["textures/entity/texture.png"] = [0x89, 0x50, 0x4E, 0x47, 0x00],
        });

        var doc = YSMParser.Core.YsmFile.Parse(zipBytes);

        Assert.Equal(YSMParser.Core.YsmContainer.ZipArchive, doc.Container);
        Assert.Single(doc.Resources.Models);
        Assert.Equal("models/entity/model.json", doc.Resources.Models[0].Name);
        Assert.Single(doc.Resources.Textures);
        Assert.Equal("textures/entity/texture.png", doc.Resources.Textures[0].Name);
    }

    [Fact]
    public void Parse_ExtractsInfoAndYsmJson()
    {
        var zipBytes = CreateZip(new Dictionary<string, byte[]>
        {
            ["info.json"] = Encoding.UTF8.GetBytes("{\"name\":\"Test\"}"),
            ["ysm.json"] = Encoding.UTF8.GetBytes("{\"version\":1}"),
        });

        var doc = YSMParser.Core.YsmFile.Peek(zipBytes);

        Assert.NotNull(doc.Resources.InfoJson);
        Assert.Contains("\"name\""u8, doc.Resources.InfoJson);
        Assert.NotNull(doc.Resources.YsmJson);
    }

    [Fact]
    public void Parse_EmptyZip_IsNotDetectedAsArchive()
    {
        var zipBytes = CreateZip([]);

        // A zero-entry zip starts with the EOCD record (PK\x05\x06), not a
        // local file header, so Detect reports Unsupported — identical to the
        // old PK\x03\x04 magic check. The loader surfaces a friendly error.
        Assert.Equal(YSMParser.Core.YsmContainer.Unsupported, YSMParser.Core.YsmFile.Detect(zipBytes));
        Assert.Throws<InvalidOperationException>(() => YsmLoaderService.LoadDocumentFromBytes(zipBytes));
    }

    [Fact]
    public void Parse_StripsSingleRootWrapFolder()
    {
        var zipBytes = CreateZip(new Dictionary<string, byte[]>
        {
            ["2b/models/main.json"] = Encoding.UTF8.GetBytes("{\"fake\":true}"),
            ["2b/ysm.json"] = Encoding.UTF8.GetBytes("{\"version\":1}"),
        });

        var doc = YSMParser.Core.YsmFile.Parse(zipBytes);

        Assert.Single(doc.Resources.Models);
        Assert.Equal("models/main.json", doc.Resources.Models[0].Name);
        Assert.NotNull(doc.Resources.YsmJson);
    }

    [Fact]
    public void Sanitize_FiltersMacOsxJunkEntries()
    {
        // Locked by the library since 1.1.1 (dropped at zip read time); kept
        // as an end-to-end regression guard through the viewer entry point.
        var zipBytes = CreateZip(new Dictionary<string, byte[]>
        {
            ["__MACOSX/._model.json"] = [0x00, 0x01],
            ["__MACOSX/models/._texture.png"] = [0x00, 0x02],
            ["models/model.json"] = Encoding.UTF8.GetBytes("{\"valid\":true}"),
        });

        var resources = YsmLoaderService.GetSanitizedResources(YSMParser.Core.YsmFile.Parse(zipBytes));

        Assert.Single(resources.Models);
        Assert.Equal("models/model.json", resources.Models[0].Name);
        Assert.Empty(resources.Textures);
    }

    [Fact]
    public void Sanitize_ReroutesFlatRuleMisfits()
    {
        // .webp is routed to Textures by the library since 1.1.1; the viewer
        // sanitize layer still re-routes .lang/.mcfunction and non-.ogg audio.
        var zipBytes = CreateZip(new Dictionary<string, byte[]>
        {
            ["skin.webp"] = [0x12, 0x34],
            ["zh_CN.lang"] = Encoding.UTF8.GetBytes("key=value"),
            ["tick.mcfunction"] = Encoding.UTF8.GetBytes("say hi"),
            ["bg.mp3"] = [0xFF, 0xFB, 0x00],
        });

        var resources = YsmLoaderService.GetSanitizedResources(YSMParser.Core.YsmFile.Parse(zipBytes));

        Assert.Empty(resources.Models);
        Assert.Single(resources.Textures);
        Assert.Equal("skin.webp", resources.Textures[0].Name);
        Assert.Single(resources.Languages);
        Assert.Single(resources.Functions);
        Assert.Single(resources.Sounds);
        Assert.Equal("bg.mp3", resources.Sounds[0].Name);
    }

    [Fact]
    public void Sanitize_ReroutesRootAnimationControllers()
    {
        // The library keeps animation_controller names out of Animations
        // since 1.1.1; kept as an end-to-end regression guard.
        var zipBytes = CreateZip(new Dictionary<string, byte[]>
        {
            ["npc.animation_controller.json"] = Encoding.UTF8.GetBytes("{}"),
            ["walk.animation.json"] = Encoding.UTF8.GetBytes("{}"),
        });

        var resources = YsmLoaderService.GetSanitizedResources(YSMParser.Core.YsmFile.Parse(zipBytes));

        Assert.Single(resources.Animations);
        Assert.Equal("walk.animation.json", resources.Animations[0].Name);
        Assert.Single(resources.AnimationControllers);
        Assert.Equal("npc.animation_controller.json", resources.AnimationControllers[0].Name);
    }

    [Fact]
    public void Sanitize_ReroutesSpecialAndSpecularTextures()
    {
        var zipBytes = CreateZip(new Dictionary<string, byte[]>
        {
            // "special/" has no directory rule in the library classifier and
            // falls through to Textures; "specular" outside textures/ too.
            ["special/glow.png"] = [0x00, 0x0A],
            ["specular.png"] = [0x00, 0x0B],
            ["textures/sword.png"] = [0x00, 0x0C],
        });

        var resources = YsmLoaderService.GetSanitizedResources(YSMParser.Core.YsmFile.Parse(zipBytes));

        Assert.Single(resources.Textures);
        Assert.Equal("textures/sword.png", resources.Textures[0].Name);
        Assert.Equal(2, resources.SpecialImages.Count);
    }

    [Fact]
    public void Parse_ClassifiesAnimationEntries()
    {
        var zipBytes = CreateZip(new Dictionary<string, byte[]>
        {
            ["animations/walk.animation.json"] = Encoding.UTF8.GetBytes("{\"loop\":true}"),
        });

        var doc = YSMParser.Core.YsmFile.Parse(zipBytes);

        Assert.Single(doc.Resources.Animations);
        Assert.Equal("animations/walk.animation.json", doc.Resources.Animations[0].Name);
    }

    [Fact]
    public void LoadDocumentFromBytes_Ysgp2Container_ThrowsFriendlyError()
    {
        // UTF-8 BOM + "YSGP2" + 0x0A 0x0A — the new official asset container.
        byte[] ysgp2 = [0xEF, 0xBB, 0xBF, (byte)'Y', (byte)'S', (byte)'G', (byte)'P', (byte)'2', 0x0A, 0x0A];

        Assert.Equal(YSMParser.Core.YsmContainer.NewContainer, YSMParser.Core.YsmFile.Detect(ysgp2));
        Assert.Throws<NotSupportedException>(() => YsmLoaderService.LoadDocumentFromBytes(ysgp2));
    }

    [Fact]
    public void LoadDocumentFromBytes_UnrecognizedData_ThrowsFriendlyError()
    {
        byte[] junk = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];

        Assert.Equal(YSMParser.Core.YsmContainer.Unsupported, YSMParser.Core.YsmFile.Detect(junk));
        Assert.Throws<InvalidOperationException>(() => YsmLoaderService.LoadDocumentFromBytes(junk));
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
