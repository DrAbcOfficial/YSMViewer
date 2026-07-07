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
    public void Parse_InvalidJson_ReturnsNull()
    {
        var result = YsmMetadataParser.Parse("not valid json"u8.ToArray(), null);
        Assert.Null(result);
    }
}
