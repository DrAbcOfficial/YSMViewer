using Aura3D.Core;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Avalonia.Media.Imaging;
using System.Numerics;
using System.Text.Json;
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
        using var parser = YSMParserFactory.Create(filePath);
        parser.Parse();
        return LoadFromParser(parser);
    }

    public static LoadedModel LoadFromBytes(byte[] data)
    {
        using var parser = YSMParserFactory.CreateFromBytes(data);
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

        byte[]? fallbackTexture = null;
        if (resources.Textures.Count > 0)
            fallbackTexture = EnsurePng(resources.Textures[0].Data);

        Dictionary<string, Node>? primaryBoneNodes = null;
        Dictionary<string, Vector3>? primaryBaseEulers = null;
        string? primaryModelName = null;

        for (int i = 0; i < resources.Models.Count; i++)
        {
            var modelEntry = resources.Models[i];

            List<MinecraftGeometry> allGeometries;
            try
            {
                allGeometries = ParseAllGeometries(modelEntry.Data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YsmLoaderService] Skipping non-geometry model '{modelEntry.Name}': {ex.Message}");
                continue;
            }

            var geometry = allGeometries[0];
            if (geometry?.Description is null)
                throw new InvalidOperationException($"Geometry has no description for model '{modelEntry.Name}'");
            if (geometry.Bones is null)
                throw new InvalidOperationException($"Geometry has no bones for model '{modelEntry.Name}'");
            var category = ClassifyModel(modelEntry.Name);
            bool defaultVisible = category == ModelCategory.Main;

            var textureData = FindTextureForModel(modelEntry.Name, resources.Textures) ?? fallbackTexture ?? [];
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

            if (modelNodes.Count == 1 || primaryModelName is null)
            {
                primaryModelName = geometry.Description.Identifier;
                primaryBoneNodes = result.BoneNodes;
                primaryBaseEulers = result.BaseBoneEulers;
            }
        }

        if (modelNodes.Count == 0)
            throw new InvalidOperationException("No valid geometry models found in YSM file");

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
        var json = StripJsonComments(System.Text.Encoding.UTF8.GetString(jsonData));
        var cleanData = System.Text.Encoding.UTF8.GetBytes(json);

        var file = JsonSerializer.Deserialize(cleanData, YsmJsonContext.Default.MinecraftGeometryFile)
                   ?? throw new InvalidOperationException("Failed to parse geometry JSON");

        if (file.Geometries is not { Count: > 0 })
            throw new InvalidOperationException("No geometry definitions found");

        return file.Geometries;
    }

    private static string StripJsonComments(string json)
    {
        var sb = new System.Text.StringBuilder(json.Length);
        var lines = json.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("//"))
                continue;
            var commentIdx = line.IndexOf("//");
            if (commentIdx >= 0)
            {
                var before = line[..commentIdx];
                if (before.Any(c => c == '"'))
                    sb.AppendLine(line);
                else
                    sb.AppendLine(before.TrimEnd());
            }
            else
            {
                sb.AppendLine(line);
            }
        }
        return sb.ToString();
    }

    public static void ApplyTextureToModel(Model model, byte[] textureData)
    {
        var pngData = EnsurePng(textureData) ?? textureData;
        var texture = TextureLoader.LoadTexture(pngData);
        texture.SetMinFilter(TextureFilterMode.Nearest)
               .SetMagFilter(TextureFilterMode.Nearest);
        texture.SetWarpS(TextureWrapMode.Repeat)
               .SetWarpT(TextureWrapMode.Repeat);

        var meshes = model.GetNodesInChildren<InstancedMesh>();
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

    private static byte[]? FindTextureForModel(string modelName, IReadOnlyList<YsmResourceEntry> textures)
    {
        if (textures.Count == 0) return null;

        string normalizedModel = modelName.Replace("models/", "").Replace(".json", "");
        if (normalizedModel.Contains('/'))
            normalizedModel = normalizedModel[(normalizedModel.LastIndexOf('/') + 1)..];
        if (normalizedModel.Contains('\\'))
            normalizedModel = normalizedModel[(normalizedModel.LastIndexOf('\\') + 1)..];

        YsmResourceEntry? exactMatch = null;
        YsmResourceEntry? containsMatch = null;
        YsmResourceEntry? defaultMatch = null;

        foreach (var tex in textures)
        {
            string normalizedTex = tex.Name.Replace("textures/", "").Replace(".png", "").Replace(".webp", "");
            if (normalizedTex.Contains('/'))
                normalizedTex = normalizedTex[(normalizedTex.LastIndexOf('/') + 1)..];
            if (normalizedTex.Contains('\\'))
                normalizedTex = normalizedTex[(normalizedTex.LastIndexOf('\\') + 1)..];

            if (string.Equals(normalizedTex, normalizedModel, StringComparison.OrdinalIgnoreCase))
                exactMatch = tex;

            if (defaultMatch is null && string.Equals(normalizedTex, "default", StringComparison.OrdinalIgnoreCase))
                defaultMatch = tex;

            if (containsMatch is null &&
                (normalizedTex.Contains(normalizedModel, StringComparison.OrdinalIgnoreCase) ||
                 normalizedModel.Contains(normalizedTex, StringComparison.OrdinalIgnoreCase)))
                containsMatch = tex;
        }

        var entry = exactMatch ?? containsMatch ?? defaultMatch;
        if (entry is null) return null;

        return EnsurePng(entry.Data);
    }

    private static byte[]? EnsurePng(byte[]? data)
    {
        if (data is null or { Length: 0 }) return null;

        if (data.Length >= 8)
        {
            if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
                return data;

            return ConvertImageToPng(data);
        }

        return data;
    }

    private static byte[] ConvertImageToPng(byte[] imageData)
    {
        try
        {
            using var bitmap = new Bitmap(new MemoryStream(imageData));
            using var ms = new MemoryStream();
            bitmap.Save(ms);
            return ms.ToArray();
        }
        catch
        {
            return imageData;
        }
    }
}