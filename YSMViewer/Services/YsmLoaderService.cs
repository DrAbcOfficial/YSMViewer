using System.Numerics;
using System.Text.Json;
using YSMParser.Core.Parsers;
using YSMViewer.Models;
using YSMViewer.Models.Document;

namespace YSMViewer.Services;

public sealed class YsmLoaderService
{
    public enum ModelCategory
    {
        Main,
        Arm,
        SubEntity,
    }

    private static ModelCategory ClassifyModel(string name)
    {
        if (name == "main") return ModelCategory.Main;
        if (name == "arm") return ModelCategory.Arm;
        return ModelCategory.SubEntity;
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

        return YsmImageHelper.EnsurePng(entry.Data);
    }

    public static YsmModelDocument LoadDocumentFromFile(string filePath)
    {
        using var parser = YSMParserFactory.Create(filePath);
        parser.Parse();
        return LoadDocument(parser);
    }

    public static YsmModelDocument LoadDocumentFromBytes(byte[] data)
    {
        using var parser = YSMParserFactory.CreateFromBytes(data);
        parser.Parse();
        return LoadDocument(parser);
    }

    private static YsmModelDocument LoadDocument(YSMParser.Core.Parsers.YSMParser parser)
    {
        var resources = parser.GetResources();

        if (resources.Models.Count == 0)
            throw new InvalidOperationException("No models found in YSM file");

        var meta = YsmMetadataParser.Parse(resources.YsmJson, resources.InfoJson);

        var info = new YsmDocumentModelInfo(
            Name: meta?.Name ?? "Unknown",
            DisplayName: MinecraftFormatHelper.StripFormatting(meta?.Name ?? "Unknown"),
            Version: parser.GetYSGPVersion(),
            Authors: meta?.Authors is { Length: > 0 } ? string.Join(", ", meta.Authors) : string.Empty,
            License: meta?.LicenseType ?? string.Empty,
            Tips: meta?.Tips ?? string.Empty,
            IsFree: meta?.IsFree ?? false);

        byte[]? fallbackTexture = null;
        if (resources.Textures.Count > 0)
            fallbackTexture = YsmImageHelper.EnsurePng(resources.Textures[0].Data);

        var models = new List<YsmGeometryModel>();
        var textureResources = new List<YsmTextureResource>();
        var animationResources = new List<YsmAnimationResource>();
        var imageResources = new List<YsmImageResource>();

        foreach (var tex in resources.Textures)
        {
            var pngData = YsmImageHelper.EnsurePng(tex.Data) ?? tex.Data;
            var (width, height) = YsmImageHelper.GetPngDimensions(pngData);
            textureResources.Add(new YsmTextureResource(
                Id: tex.Name,
                Name: tex.Name,
                Data: pngData,
                Width: width,
                Height: height));
        }

        foreach (var anim in resources.Animations)
        {
            animationResources.Add(new YsmAnimationResource(
                Name: anim.Name,
                Data: anim.Data));
        }

        AddImageResources(imageResources, resources.Avatars, "Avatar");
        AddImageResources(imageResources, resources.Backgrounds, "Background");
        AddImageResources(imageResources, resources.SpecialImages, "Special");

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
            var categoryDoc = category switch
            {
                ModelCategory.Main => YsmModelCategory.Main,
                ModelCategory.Arm => YsmModelCategory.Arm,
                _ => YsmModelCategory.SubEntity,
            };

            var textureMatch = FindTextureForModel(modelEntry.Name, resources.Textures) ?? fallbackTexture;
            string? textureId = null;
            if (textureMatch is not null)
            {
                var pngData = YsmImageHelper.EnsurePng(textureMatch) ?? textureMatch;
                var texResource = textureResources.FirstOrDefault(t => t.Data.Length == pngData.Length && Enumerable.SequenceEqual(t.Data, pngData));
                if (texResource is null)
                {
                    var (width, height) = YsmImageHelper.GetPngDimensions(pngData);
                    texResource = new YsmTextureResource(
                        Id: $"tex_{modelEntry.Name}",
                        Name: modelEntry.Name,
                        Data: pngData,
                        Width: width,
                        Height: height);
                    textureResources.Add(texResource);
                }
                textureId = texResource.Id;
            }

            var bones = ConvertBones(geometry.Bones);
            var model = new YsmGeometryModel(
                Id: modelEntry.Name,
                Name: modelEntry.Name,
                Category: categoryDoc,
                DefaultVisible: defaultVisible,
                GeometryIdentifier: geometry.Description.Identifier,
                TextureWidth: geometry.Description.TextureWidth,
                TextureHeight: geometry.Description.TextureHeight,
                TextureId: textureId,
                Bones: bones);

            models.Add(model);
        }

        if (models.Count == 0)
            throw new InvalidOperationException("No valid geometry models found in YSM file");

        return new YsmModelDocument(info, models, textureResources, animationResources, imageResources);
    }

    private static List<YsmBoneInfo> ConvertBones(List<MinecraftBone> bones)
    {
        var bonePivots = new Dictionary<string, Vector3>();
        foreach (var bone in bones)
        {
            bonePivots[bone.Name] = bone.Pivot is { Count: >= 3 }
                ? ConvertBedrockPivotDoc(bone.Pivot)
                : Vector3.Zero;
        }

        var result = new List<YsmBoneInfo>();
        foreach (var bone in bones)
        {
            var rotation = bone.Rotation is { Count: >= 3 }
                ? ConvertBedrockRotationDoc(bone.Rotation)
                : Vector3.Zero;

            var cubes = new List<YsmCubeInfo>();
            if (bone.Cubes is not null)
            {
                int cubeIdx = 0;
                foreach (var cube in bone.Cubes)
                {
                    if (cube.Origin is not { Count: >= 3 } || cube.Size is not { Count: >= 3 })
                        continue;

                    var cubePivot = cube.Pivot is { Count: >= 3 }
                        ? ConvertBedrockPivotDoc(cube.Pivot)
                        : Vector3.Zero;

                    var origin = new Vector3(-cube.Origin[0], cube.Origin[1], cube.Origin[2]);
                    var size = new Vector3(cube.Size[0], cube.Size[1], cube.Size[2]);
                    var cubeRotation = cube.Rotation is { Count: >= 3 }
                        ? ConvertBedrockRotationDoc(cube.Rotation)
                        : Vector3.Zero;

                    cubes.Add(new YsmCubeInfo(
                        Id: $"cube_{bone.Name}_{cubeIdx}",
                        Origin: origin,
                        Size: size,
                        Pivot: cubePivot,
                        Rotation: cubeRotation,
                        Inflate: cube.Inflate,
                        Uv: cube.Uv));
                    cubeIdx++;
                }
            }

            result.Add(new YsmBoneInfo(
                Id: bone.Name,
                Name: bone.Name,
                ParentId: bone.Parent,
                Pivot: bonePivots[bone.Name],
                Rotation: rotation,
                Cubes: cubes));
        }

        return result;
    }

    private static Vector3 ConvertBedrockPivotDoc(List<float> pivot)
    {
        return new Vector3(-pivot[0], pivot[1], pivot[2]);
    }

    private static Vector3 ConvertBedrockRotationDoc(List<float> rotation)
    {
        return new Vector3(-rotation[0], -rotation[1], rotation[2]);
    }

    private static void AddImageResources(List<YsmImageResource> list, IReadOnlyList<YsmResourceEntry> entries, string category)
    {
        foreach (var entry in entries)
        {
            var pngData = YsmImageHelper.EnsurePng(entry.Data) ?? entry.Data;
            var (width, height) = YsmImageHelper.GetPngDimensions(pngData);
            list.Add(new YsmImageResource(
                Name: entry.Name,
                Category: category,
                Data: pngData,
                Width: width,
                Height: height));
        }
    }
}
