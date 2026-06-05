using System.Numerics;
using System.Text.Json;
using Aura3D.Core;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using YSMParser.Core.Parsers;
using YSMViewer.Models;

namespace YSMViewer.Services;

public sealed class YsmLoaderService
{
    public enum ModelCategory
    {
        Main,
        Arm,
        SubEntity,
    }

    public sealed record YsmMetadata(
        string? Name,
        string? Tips,
        string? LicenseType,
        bool IsFree,
        string[] Authors,
        float WidthScale,
        float HeightScale);

    public sealed record LoadedModel(
        Model ContainerNode,
        IReadOnlyList<ModelNodeInfo> ModelNodes,
        Dictionary<string, Node> BoneNodes,
        IReadOnlyDictionary<string, Vector3> BaseBoneEulers,
        string ModelName,
        int Version,
        IReadOnlyList<YsmResourceEntry> Models,
        IReadOnlyList<YsmResourceEntry> Textures,
        IReadOnlyList<YsmResourceEntry> Animations,
        IReadOnlyList<YsmResourceEntry> Avatars,
        IReadOnlyList<YsmResourceEntry> Backgrounds,
        IReadOnlyList<YsmResourceEntry> SpecialImages,
        YsmMetadata? Metadata);

    public sealed record ModelNodeInfo(
        string Name,
        Model Node,
        byte[] GeometryData,
        ModelCategory Category,
        bool DefaultVisible,
        int GeometryCount = 1,
        string GeometryIdentifier = "");

    private static ModelCategory ClassifyModel(string name)
    {
        if (name == "main") return ModelCategory.Main;
        if (name == "arm") return ModelCategory.Arm;
        return ModelCategory.SubEntity;
    }

    public static LoadedModel Load(string filePath)
    {
        var parser = YSMParserFactory.Create(filePath);
        parser.Parse();
        return LoadFromParser(parser);
    }

    public static LoadedModel LoadFromBytes(byte[] data)
    {
        var parser = YSMParserFactory.CreateFromBytes(data);
        parser.Parse();
        return LoadFromParser(parser);
    }

    private static LoadedModel LoadFromParser(YSMParser.Core.Parsers.YSMParser parser)
    {
        var resources = parser.GetResources();

        if (resources.Models.Count == 0)
            throw new InvalidOperationException("No models found in YSM file");

        var containerModel = new Model { Name = "ysm_root" };
        var modelNodes = new List<ModelNodeInfo>();

        byte[]? firstTexturePng = null;
        if (resources.Textures.Count > 0)
            firstTexturePng = resources.Textures[0].Data;

        Dictionary<string, Node>? primaryBoneNodes = null;
        Dictionary<string, Vector3>? primaryBaseEulers = null;
        string? primaryModelName = null;

        for (int i = 0; i < resources.Models.Count; i++)
        {
            var modelEntry = resources.Models[i];
            var allGeometries = ParseAllGeometries(modelEntry.Data);
            var geometry = allGeometries[0];
            if (geometry?.Description is null)
                throw new InvalidOperationException($"Geometry has no description for model '{modelEntry.Name}'");
            if (geometry.Bones is null)
                throw new InvalidOperationException($"Geometry has no bones for model '{modelEntry.Name}'");
            var category = ClassifyModel(modelEntry.Name);
            bool defaultVisible = category == ModelCategory.Main;

            var textureData = firstTexturePng ?? [];
            var result = MeshBuilderService.BuildModelNode(
                geometry, textureData,
                geometry.Description.TextureWidth,
                geometry.Description.TextureHeight,
                modelEntry.Name);

            result.RootModel.Enable = defaultVisible;
            containerModel.AddChild(result.RootModel, AttachToParentRule.KeepLocal);

            var info = new ModelNodeInfo(modelEntry.Name, result.RootModel, modelEntry.Data, category, defaultVisible,
                allGeometries.Count, geometry.Description.Identifier);
            modelNodes.Add(info);

            if (i == 0)
            {
                primaryModelName = geometry.Description.Identifier;
                primaryBoneNodes = result.BoneNodes;
                primaryBaseEulers = result.BaseBoneEulers;
            }
        }

        return new LoadedModel(
            containerModel,
            modelNodes,
            primaryBoneNodes ?? [],
            primaryBaseEulers ?? [],
            primaryModelName ?? "Unknown",
            parser.GetYSGPVersion(),
            resources.Models,
            resources.Textures,
            resources.Animations,
            resources.Avatars,
            resources.Backgrounds,
            resources.SpecialImages,
            ParseMetadata(resources.YsmJson, resources.InfoJson));
    }

    private static List<MinecraftGeometry> ParseAllGeometries(byte[] jsonData)
    {
        var file = JsonSerializer.Deserialize(jsonData, YsmJsonContext.Default.MinecraftGeometryFile)
                   ?? throw new InvalidOperationException("Failed to parse geometry JSON");

        if (file.Geometries is not { Count: > 0 })
            throw new InvalidOperationException("No geometry definitions found");

        return file.Geometries;
    }

    public static void ApplyTextureToModel(Model model, byte[] texturePng)
    {
        var texture = TextureLoader.LoadTexture(texturePng);
        texture.SetMinFilter(TextureFilterMode.Nearest)
               .SetMagFilter(TextureFilterMode.Nearest);
        texture.SetWarpS(TextureWrapMode.Repeat)
               .SetWarpT(TextureWrapMode.Repeat);

        var meshes = model.GetNodesInChildren<Mesh>();
        foreach (var mesh in meshes)
        {
            mesh.Material?.BaseColor = texture;
        }
    }

    private static YsmMetadata? ParseMetadata(byte[]? ysmJson, byte[]? infoJson)
    {
        try
        {
            if (ysmJson is { Length: > 0 })
                return ParseYsmJson(ysmJson);
            if (infoJson is { Length: > 0 })
                return ParseInfoJson(infoJson);
        }
        catch { }
        return null;
    }

    private static YsmMetadata? ParseYsmJson(byte[] data)
    {
        using var doc = JsonDocument.Parse(data);
        var root = doc.RootElement;

        string? name = null;
        string? tips = null;
        string? licenseType = null;
        bool isFree = false;
        var authors = new List<string>();
        float widthScale = 1f;
        float heightScale = 1f;

        if (root.TryGetProperty("metadata", out var meta))
        {
            if (meta.TryGetProperty("name", out var n)) name = n.GetString();
            if (meta.TryGetProperty("tips", out var t)) tips = t.GetString();
            if (meta.TryGetProperty("license", out var lic)
                && lic.TryGetProperty("type", out var lt)) licenseType = lt.GetString();
            if (meta.TryGetProperty("authors", out var auths))
            {
                foreach (var a in auths.EnumerateArray())
                {
                    if (a.TryGetProperty("name", out var an)) authors.Add(an.GetString() ?? "");
                }
            }
        }

        if (root.TryGetProperty("properties", out var props))
        {
            if (props.TryGetProperty("free", out var fr) && fr.ValueKind == JsonValueKind.True) isFree = true;
            if (props.TryGetProperty("width_scale", out var ws)
                && ws.TryGetSingle(out var wsv)) widthScale = wsv;
            if (props.TryGetProperty("height_scale", out var hs)
                && hs.TryGetSingle(out var hsv)) heightScale = hsv;
        }

        return new YsmMetadata(name, tips, licenseType, isFree, [.. authors], widthScale, heightScale);
    }

    private static YsmMetadata? ParseInfoJson(byte[] data)
    {
        using var doc = JsonDocument.Parse(data);
        var root = doc.RootElement;

        string? name = null;
        string? tips = null;
        string? licenseType = null;
        bool isFree = false;
        var authors = new List<string>();

        if (root.TryGetProperty("name", out var n)) name = n.GetString();
        if (root.TryGetProperty("tips", out var t)) tips = t.GetString();
        if (root.TryGetProperty("license", out var lic)) licenseType = lic.GetString();
        if (root.TryGetProperty("free", out var fr)
            && (fr.ValueKind == JsonValueKind.True || (fr.ValueKind == JsonValueKind.Number && fr.TryGetSingle(out var fsv) && fsv > 0.5f)))
            isFree = true;
        if (root.TryGetProperty("authors", out var auths))
        {
            foreach (var a in auths.EnumerateArray())
            {
                if (a.ValueKind == JsonValueKind.String) authors.Add(a.GetString() ?? "");
                else if (a.TryGetProperty("name", out var an)) authors.Add(an.GetString() ?? "");
            }
        }

        return new YsmMetadata(name, tips, licenseType, isFree, [.. authors], 1f, 1f);
    }
}