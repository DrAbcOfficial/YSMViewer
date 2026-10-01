using System.IO.Compression;
using System.Text;
using YSMViewer.Models;
using YSMViewer.Models.Document;
using YSMViewer.Services;

namespace YSMViewer.Core.Tests.Services;

/// <summary>
/// Geometry conversion coverage for the audit's P1 fixes: cube-level
/// mirror/inflate with bone-level fallback (Blockbench parseCube semantics)
/// and the legacy single-object geometry.<name> file layout.
/// </summary>
public sealed class YsmGeometryConversionTests
{
    [Fact]
    public void Expand_WithMirror_FlipsUAndSwapsEastWest()
    {
        var uv = new MinecraftCubeUV(null, null, null, null, null, null) { BoxU = 0f, BoxV = 0f };

        var plain = uv.Expand(4f, 4f, 2f);
        var mirrored = uv.Expand(4f, 4f, 2f, mirror: true);

        // Unmirrored box layout on a 4x4x4 cube (z=2, x=4):
        // east [0,2]/[2,4], west [6,2]/[2,4].
        Assert.Equal([0f, 2f], plain.East!.UvCoords);
        Assert.Equal([2f, 4f], plain.East.UvSize);
        Assert.Equal([6f, 2f], plain.West!.UvCoords);

        // Mirror flips every face's U axis (u += du, du = -du) and swaps E/W.
        Assert.Equal([6f + 2f, 2f], mirrored.East!.UvCoords);
        Assert.Equal([-2f, 4f], mirrored.East.UvSize);
        Assert.Equal([0f + 2f, 2f], mirrored.West!.UvCoords);
        Assert.Equal([-2f, 4f], mirrored.West.UvSize);
        Assert.Equal([2f + 4f, 2f], mirrored.North!.UvCoords);
        Assert.Equal([-4f, 4f], mirrored.North.UvSize);
    }

    [Fact]
    public void LoadDocument_BoneLevelInflateAndMirror_FallThroughToCubes()
    {
        var doc = LoadWithGeometry("""
            {
              "format_version": "1.12.0",
              "minecraft:geometry": [{
                "description": { "identifier": "geometry.test", "texture_width": 16, "texture_height": 16 },
                "bones": [
                  {
                    "name": "body",
                    "pivot": [0, 0, 0],
                    "inflate": 0.5,
                    "mirror": true,
                    "cubes": [
                      { "origin": [-2, 0, -2], "size": [4, 4, 4], "uv": [0, 0] },
                      { "origin": [-2, 4, -2], "size": [4, 1, 4], "uv": [16, 0], "inflate": 0.25, "mirror": false }
                    ]
                  }
                ]
              }]
            }
            """);

        var bone = Assert.Single(doc.Models[0].Bones);
        var inherited = bone.Cubes[0];
        Assert.Equal(0.5f, inherited.Inflate);
        Assert.True(inherited.Mirror);

        // Explicit cube values win over the bone's (?? semantics).
        var overridden = bone.Cubes[1];
        Assert.Equal(0.25f, overridden.Inflate);
        Assert.False(overridden.Mirror);
    }

    [Fact]
    public void LoadDocument_LegacyGeometryRootFormat_IsParsed()
    {
        var doc = LoadWithGeometry("""
            {
              "geometry.legacy_model": {
                "texturewidth": 32,
                "textureheight": 32,
                "visible_bounds_width": 2,
                "visible_bounds_height": 2,
                "bones": [
                  {
                    "name": "head",
                    "pivot": [0, 4, 0],
                    "cubes": [
                      { "origin": [-4, 0, -4], "size": [8, 8, 8], "uv": [0, 0], "inflate": 0.5 }
                    ]
                  }
                ]
              }
            }
            """);

        var model = doc.Models[0];
        Assert.Equal("geometry.legacy_model", model.GeometryIdentifier);
        Assert.Equal(32f, model.TextureWidth);
        Assert.Equal(32f, model.TextureHeight);

        var bone = Assert.Single(model.Bones);
        var cube = Assert.Single(bone.Cubes);
        Assert.Equal(0.5f, cube.Inflate);
        Assert.NotNull(cube.Uv);
        Assert.True(cube.Uv!.IsBoxUV);
    }

    [Fact]
    public void LoadDocument_LegacyBoneInflateAndMirror_FallThroughToCubes()
    {
        var doc = LoadWithGeometry("""
            {
              "geometry.legacy_inflate": {
                "texturewidth": 16,
                "textureheight": 16,
                "bones": [
                  {
                    "name": "limb",
                    "pivot": [0, 0, 0],
                    "inflate": 0.25,
                    "mirror": true,
                    "cubes": [
                      { "origin": [-2, 0, -2], "size": [4, 4, 4], "uv": [0, 0] }
                    ]
                  }
                ]
              }
            }
            """);

        var cube = Assert.Single(Assert.Single(doc.Models[0].Bones).Cubes);
        Assert.Equal(0.25f, cube.Inflate);
        Assert.True(cube.Mirror);
    }

    [Fact]
    public void LoadDocument_PerFaceUvRotation_IsParsed()
    {
        var doc = LoadWithGeometry("""
            {
              "format_version": "1.21.0",
              "minecraft:geometry": [{
                "description": { "identifier": "geometry.rot", "texture_width": 16, "texture_height": 16 },
                "bones": [
                  {
                    "name": "panel",
                    "pivot": [0, 0, 0],
                    "cubes": [
                      {
                        "origin": [-2, 0, -2], "size": [4, 4, 0],
                        "uv": {
                          "north": { "uv": [0, 0], "uv_size": [4, 4], "uv_rotation": 90 },
                          "south": { "uv": [8, 0], "uv_size": [4, 4] }
                        }
                      }
                    ]
                  }
                ]
              }]
            }
            """);

        var cube = Assert.Single(Assert.Single(doc.Models[0].Bones).Cubes);
        Assert.Equal(90, cube.Uv!.North!.UvRotation);
        Assert.Null(cube.Uv.South!.UvRotation);
    }

    [Fact]
    public void GetFaceUv_Rotation_CyclesCornersLikeBlockbench()
    {
        // 16x16 texture, face uv [0,0] size [16,16] → corners TL(0,0) TR(1,0) BL(0,1) BR(1,1).
        static MinecraftCubeFaceUV Face(int rotation) =>
            new([0f, 0f], [16f, 16f], null, rotation);

        var identity = CubeFaceUvMapper.GetFaceUv(Face(0), 16f, 16f);
        Assert.Equal((0f, 0f, 1f, 0f, 0f, 1f, 1f, 1f), identity);

        // Each 90° step: TL←BL, TR←TL, BL←BR, BR←TR (Blockbench cube.js updateUV).
        var rot90 = CubeFaceUvMapper.GetFaceUv(Face(90), 16f, 16f);
        Assert.Equal((0f, 1f, 0f, 0f, 1f, 1f, 1f, 0f), rot90);

        var rot180 = CubeFaceUvMapper.GetFaceUv(Face(180), 16f, 16f);
        Assert.Equal((1f, 1f, 0f, 1f, 1f, 0f, 0f, 0f), rot180);

        var rot270 = CubeFaceUvMapper.GetFaceUv(Face(270), 16f, 16f);
        Assert.Equal((1f, 0f, 1f, 1f, 0f, 0f, 0f, 1f), rot270);

        // Missing face degenerates to a single-point sample, as before.
        var missing = CubeFaceUvMapper.GetFaceUv(null, 16f, 16f);
        Assert.Equal((0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f), missing);
    }

    private static YsmModelDocument LoadWithGeometry(string geometryJson)
    {
        var zipBytes = CreateZip(new Dictionary<string, byte[]>
        {
            ["models/main.json"] = Encoding.UTF8.GetBytes(geometryJson),
        });

        return YsmLoaderService.LoadDocumentFromBytes(zipBytes);
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
