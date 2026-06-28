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

    private static string NormalizeModelName(string name)
    {
        return NormalizeResourceName(name, "models/");
    }

    private static string NormalizeTextureName(string name)
    {
        return NormalizeResourceName(name, "textures/");
    }

    private static string NormalizeResourceName(string name, string prefix)
    {
        var result = name.Replace('\\', '/');
        if (result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            result = result[prefix.Length..];

        var lastSlash = result.LastIndexOf('/');
        if (lastSlash >= 0)
            result = result[(lastSlash + 1)..];

        var ext = Path.GetExtension(result);
        if (!string.IsNullOrEmpty(ext))
            result = result[..^ext.Length];

        return result;
    }

    private static ModelCategory ClassifyModel(string name)
    {
        var normalized = NormalizeModelName(name);
        if (string.Equals(normalized, "main", StringComparison.OrdinalIgnoreCase)) return ModelCategory.Main;
        if (string.Equals(normalized, "arm", StringComparison.OrdinalIgnoreCase)) return ModelCategory.Arm;
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

        string normalizedModel = NormalizeModelName(modelName);

        YsmResourceEntry? exactMatch = null;
        YsmResourceEntry? containsMatch = null;
        YsmResourceEntry? defaultMatch = null;

        foreach (var tex in textures)
        {
            string normalizedTex = NormalizeTextureName(tex.Name);

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

    private static byte[]? FindRawTexture(string modelName, IReadOnlyList<YsmResourceEntry> textures)
    {
        if (textures.Count == 0) return null;

        string normalizedModel = NormalizeModelName(modelName);

        YsmResourceEntry? exactMatch = null;
        YsmResourceEntry? containsMatch = null;
        YsmResourceEntry? defaultMatch = null;

        foreach (var tex in textures)
        {
            string normalizedTex = NormalizeTextureName(tex.Name);

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
        return entry?.Data;
    }

    public static YsmModelDocument LoadDocumentFromFile(string filePath)
    {
        if (IsZipFile(filePath))
        {
            var data = File.ReadAllBytes(filePath);
            return LoadDocumentFromBytes(data);
        }

        using var parser = YSMParserFactory.Create(filePath);
        parser.Parse();
        return LoadDocument(parser);
    }

    public static YsmModelDocument LoadDocumentFromBytes(byte[] data)
    {
        using var parser = IsZipData(data) ? new ZipYsmParser(data) : YSMParserFactory.CreateFromBytes(data);
        parser.Parse();
        return LoadDocument(parser);
    }

    public static YsmModelDocument LoadDocumentForThumbnail(byte[] data)
    {
        using var parser = IsZipData(data) ? new ZipYsmParser(data) : YSMParserFactory.CreateFromBytes(data);
        parser.Parse();
        return LoadDocumentThumbnail(parser);
    }

    public static bool IsZipFile(string filePath) =>
        filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

    public static bool IsZipData(byte[] data) =>
        data.Length >= 4 && data[0] == 0x50 && data[1] == 0x4B && data[2] == 0x03 && data[3] == 0x04;

    private static YsmModelDocument LoadDocumentThumbnail(YSMParser.Core.Parsers.YSMParser parser)
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

        var textureResources = new List<YsmTextureResource>();
        var models = new List<YsmGeometryModel>();

        YsmResourceEntry? mainModelEntry = null;
        foreach (var m in resources.Models)
        {
            if (ClassifyModel(m.Name) == ModelCategory.Main)
            {
                mainModelEntry = m;
                break;
            }
        }
        mainModelEntry ??= resources.Models[0];

        var allGeometries = ParseAllGeometries(mainModelEntry.Data);
        var geometry = allGeometries[0];
        if (geometry?.Description is null)
            throw new InvalidOperationException($"Geometry has no description for model '{mainModelEntry.Name}'");
        if (geometry.Bones is null)
            throw new InvalidOperationException($"Geometry has no bones for model '{mainModelEntry.Name}'");

        var textureMatch = FindRawTexture(mainModelEntry.Name, resources.Textures);
        if (textureMatch is null && resources.Textures.Count > 0)
            textureMatch = resources.Textures[0].Data;

        string? textureId = null;
        if (textureMatch is not null)
        {
            var pngData = YsmImageHelper.EnsurePng(textureMatch) ?? textureMatch;
            var (width, height) = YsmImageHelper.GetPngDimensions(pngData);
            var texResource = new YsmTextureResource(
                Id: $"tex_{mainModelEntry.Name}",
                Name: mainModelEntry.Name,
                Data: pngData,
                Width: width,
                Height: height);
            textureResources.Add(texResource);
            textureId = texResource.Id;
        }

        var bones = ConvertBones(geometry.Bones);
        var model = new YsmGeometryModel(
            Id: mainModelEntry.Name,
            Name: NormalizeModelName(mainModelEntry.Name),
            Category: YsmModelCategory.Main,
            DefaultVisible: true,
            GeometryIdentifier: geometry.Description.Identifier,
            TextureWidth: geometry.Description.TextureWidth,
            TextureHeight: geometry.Description.TextureHeight,
            TextureId: textureId,
            Bones: bones);
        models.Add(model);

        return new YsmModelDocument(
            info,
            models,
            textureResources,
            [],
            [],
            [],
            [],
            [],
            YsmExtraAnimationLayout.Empty);
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
                Name: NormalizeModelName(modelEntry.Name),
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

        var animControllerResources = new List<YsmAnimationControllerResource>();
        foreach (var ac in resources.AnimationControllers)
            animControllerResources.Add(new YsmAnimationControllerResource(ac.Name, ac.Data));

        var soundResources = new List<YsmSoundResource>();
        foreach (var snd in resources.Sounds)
            soundResources.Add(new YsmSoundResource(snd.Name, snd.Data));

        var functionResources = new List<YsmFunctionResource>();
        foreach (var fn in resources.Functions)
            functionResources.Add(new YsmFunctionResource(fn.Name, fn.Data));

        var extraAnimations = ParseExtraAnimations(resources.YsmJson);
        return new YsmModelDocument(info, models, textureResources, animationResources, imageResources, animControllerResources, soundResources, functionResources, extraAnimations);
    }

    private static YsmExtraAnimationLayout ParseExtraAnimations(byte[]? ysmJson)
    {
        if (ysmJson is not { Length: > 0 })
            return YsmExtraAnimationLayout.Empty;

        try
        {
            using var doc = JsonDocument.Parse(StripJsonComments(System.Text.Encoding.UTF8.GetString(ysmJson)));
            if (!doc.RootElement.TryGetProperty("properties", out var props))
                return YsmExtraAnimationLayout.Empty;

            var rootEntries = props.TryGetProperty("extra_animation", out var rootAnim)
                ? ParseExtraAnimationObject(rootAnim, string.Empty)
                : [];

            var groups = props.TryGetProperty("extra_animation_classify", out var classify)
                ? ParseExtraAnimationGroups(classify)
                : [];

            var buttons = props.TryGetProperty("extra_animation_buttons", out var buttonsElement)
                ? ParseExtraAnimationButtonStubs(buttonsElement)
                : [];

            if (rootEntries.Count == 0 && groups.Count == 0 && buttons.Count == 0)
                return YsmExtraAnimationLayout.Empty;

            return new YsmExtraAnimationLayout(rootEntries, groups, buttons);
        }
        catch
        {
            return YsmExtraAnimationLayout.Empty;
        }
    }

    private static List<YsmExtraAnimationEntry> ParseExtraAnimationObject(JsonElement element, string category)
    {
        var result = new List<YsmExtraAnimationEntry>();
        if (element.ValueKind != JsonValueKind.Object)
            return result;

        int index = 0;
        foreach (var property in element.EnumerateObject())
        {
            var key = property.Name;
            var displayName = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString() ?? string.Empty
                : string.Empty;

            if (key.Equals("#return", StringComparison.OrdinalIgnoreCase) || key.StartsWith('#') || displayName.StartsWith('#'))
            {
                index++;
                continue;
            }

            result.Add(new YsmExtraAnimationEntry(
                key,
                string.IsNullOrWhiteSpace(displayName) ? key : displayName,
                category,
                index));
            index++;
        }

        return result;
    }

    private static List<YsmExtraAnimationGroup> ParseExtraAnimationGroups(JsonElement element)
    {
        var result = new List<YsmExtraAnimationGroup>();
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var groupElement in element.EnumerateArray())
            {
                if (TryParseExtraAnimationGroup(groupElement, out var group))
                    result.Add(group);
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var entries = ParseExtraAnimationObject(property.Value, property.Name);
                if (entries.Count > 0)
                    result.Add(new YsmExtraAnimationGroup(property.Name, property.Name, entries));
            }
        }

        return result;
    }

    private static bool TryParseExtraAnimationGroup(JsonElement element, out YsmExtraAnimationGroup group)
    {
        group = new YsmExtraAnimationGroup(string.Empty, string.Empty, []);
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        var id = GetOptionalString(element, "id")
            ?? GetOptionalString(element, "name")
            ?? GetOptionalString(element, "key")
            ?? string.Empty;
        var displayName = GetOptionalString(element, "name")
            ?? GetOptionalString(element, "display_name")
            ?? id;

        if (!element.TryGetProperty("extra_animation", out var animations))
            return false;

        var entries = ParseExtraAnimationObject(animations, id);
        if (entries.Count == 0)
            return false;

        group = new YsmExtraAnimationGroup(id, string.IsNullOrWhiteSpace(displayName) ? id : displayName, entries);
        return true;
    }

    private static List<YsmExtraAnimationButtonDefinition> ParseExtraAnimationButtonStubs(JsonElement element)
    {
        var result = new List<YsmExtraAnimationButtonDefinition>();
        if (element.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var id = GetOptionalString(item, "id") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id))
                continue;

            result.Add(new YsmExtraAnimationButtonDefinition(
                id,
                GetOptionalString(item, "name") ?? id,
                GetOptionalString(item, "description") ?? string.Empty));
        }

        return result;
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
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
