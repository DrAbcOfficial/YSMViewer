using System.Text.Json;
using YSMViewer.Services;

namespace YSMViewer.Core.Tests.Services;

public sealed class YsmMetadataParserTests
{
    [Fact]
    public void Parse_NameAndAuthors_FromYsmJson()
    {
        var ysmJson = JsonSerializer.SerializeToUtf8Bytes(new
        {
            metadata = new
            {
                name = "TestModel",
                authors = new[] { new { name = "Author1" }, new { name = "Author2" } }
            }
        });

        var result = YsmMetadataParser.Parse(ysmJson, null);

        Assert.NotNull(result);
        Assert.Equal("TestModel", result!.Name);
        Assert.Equal(["Author1", "Author2"], result.Authors);
    }

    [Fact]
    public void Parse_FreeModel_FromYsmJson()
    {
        var ysmJson = JsonSerializer.SerializeToUtf8Bytes(new
        {
            properties = new { free = true }
        });

        var result = YsmMetadataParser.Parse(ysmJson, null);

        Assert.NotNull(result);
        Assert.True(result!.IsFree);
    }

    [Fact]
    public void Parse_FreeModel_FromInfoJson_True()
    {
        var infoJson = JsonSerializer.SerializeToUtf8Bytes(new { free = true });

        var result = YsmMetadataParser.Parse(null, infoJson);

        Assert.NotNull(result);
        Assert.True(result!.IsFree);
    }

    [Fact]
    public void Parse_FreeModel_FromInfoJson_NumberGreaterThanHalf()
    {
        var infoJson = JsonSerializer.SerializeToUtf8Bytes(new { free = 0.8f });

        var result = YsmMetadataParser.Parse(null, infoJson);

        Assert.NotNull(result);
        Assert.True(result!.IsFree);
    }

    [Fact]
    public void Parse_ScaleFactors_FromYsmJson()
    {
        var ysmJson = JsonSerializer.SerializeToUtf8Bytes(new
        {
            properties = new { width_scale = 1.5f, height_scale = 2.0f }
        });

        var result = YsmMetadataParser.Parse(ysmJson, null);

        Assert.NotNull(result);
        Assert.Equal(1.5f, result!.WidthScale);
        Assert.Equal(2.0f, result.HeightScale);
    }

    [Fact]
    public void Parse_LicenseAndTips_FromYsmJson()
    {
        var ysmJson = JsonSerializer.SerializeToUtf8Bytes(new
        {
            metadata = new
            {
                license = new { type = "MIT" },
                tips = "Some useful tips"
            }
        });

        var result = YsmMetadataParser.Parse(ysmJson, null);

        Assert.NotNull(result);
        Assert.Equal("MIT", result!.LicenseType);
        Assert.Equal("Some useful tips", result.Tips);
    }

    [Fact]
    public void Parse_StringAuthors_FromInfoJson()
    {
        var infoJson = JsonSerializer.SerializeToUtf8Bytes(new
        {
            authors = new[] { "Alice", "Bob" }
        });

        var result = YsmMetadataParser.Parse(null, infoJson);

        Assert.NotNull(result);
        Assert.Equal(["Alice", "Bob"], result!.Authors);
    }

    [Fact]
    public void Parse_NullInputs_ReturnsNull()
    {
        var result = YsmMetadataParser.Parse(null, null);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_EmptyArrays_ReturnsNull()
    {
        var result = YsmMetadataParser.Parse([], []);
        Assert.Null(result);
    }

    [Fact]
    public void Merge_YsmJsmOverridesInfoJson()
    {
        var ysmJson = JsonSerializer.SerializeToUtf8Bytes(new
        {
            metadata = new { name = "Primary" }
        });
        var infoJson = JsonSerializer.SerializeToUtf8Bytes(new
        {
            name = "Fallback",
            tips = "Info tips"
        });

        var result = YsmMetadataParser.Parse(ysmJson, infoJson);

        Assert.NotNull(result);
        Assert.Equal("Primary", result!.Name);
        Assert.Equal("Info tips", result.Tips);
    }

    [Fact]
    public void Parse_RichMetadata_LinksLicenseDescAuthorDetails()
    {
        var ysmJson = """
            {
              "metadata": {
                "name": "Rich",
                "license": { "type": "CC-BY", "desc": "Attribution required" },
                "link": { "home": "https://example.com", "bilibili": "https://space.bilibili.com/1" },
                "authors": [
                  {
                    "name": "Alice",
                    "role": "Modeler",
                    "comment": "Lead artist",
                    "avatar": "avatars/alice.png",
                    "contact": { "twitter": "https://x.com/alice", "email": "alice@example.com" }
                  },
                  { "name": "Bob" }
                ]
              }
            }
            """u8;

        var result = YsmMetadataParser.Parse(ysmJson.ToArray(), null);

        Assert.NotNull(result);
        Assert.Equal("CC-BY", result!.LicenseType);
        Assert.Equal("Attribution required", result.LicenseDescription);
        Assert.Equal(2, result.Links.Count);
        Assert.Equal("https://example.com", result.Links["home"]);

        Assert.Equal(2, result.AuthorDetails.Count);
        var alice = result.AuthorDetails[0];
        Assert.Equal("Alice", alice.Name);
        Assert.Equal("Modeler", alice.Role);
        Assert.Equal("Lead artist", alice.Comment);
        Assert.Equal("avatars/alice.png", alice.Avatar);
        Assert.Equal(2, alice.Contacts.Count);
        Assert.Equal("https://x.com/alice", alice.Contacts["twitter"]);
        Assert.Equal("Bob", result.AuthorDetails[1].Name);
        Assert.Null(result.AuthorDetails[1].Role);
    }

    [Fact]
    public void Merge_RichFields_FallBackToInfoJson()
    {
        var ysmJson = JsonSerializer.SerializeToUtf8Bytes(new
        {
            metadata = new { name = "Primary" }
        });
        var infoJson = JsonSerializer.SerializeToUtf8Bytes(new
        {
            name = "Fallback",
            authors = new[] { "Alice" }
        });

        var result = YsmMetadataParser.Parse(ysmJson, infoJson);

        Assert.NotNull(result);
        Assert.Equal("Primary", result!.Name);
        Assert.Equal(["Alice"], result.Authors);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsNull()
    {
        var result = YsmMetadataParser.Parse("not valid json"u8.ToArray(), null);
        Assert.Null(result);
    }
}
