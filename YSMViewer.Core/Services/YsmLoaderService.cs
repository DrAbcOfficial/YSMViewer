using Microsoft.Extensions.Logging;
using System.Numerics;
using System.Text.Json;
using YSMParser.Core.Parsers;
using YSMViewer.Models;
using YSMViewer.Models.Document;
using CoreDocument = YSMParser.Core.YsmModelDocument;
using YsmContainerKind = YSMParser.Core.YsmContainer;
using YsmFile = YSMParser.Core.YsmFile;
using YsmParseOptions = YSMParser.Core.YsmParseOptions;

namespace YSMViewer.Services;

public sealed class YsmLoaderService
{
    private static readonly ILogger Logger = YsmLog.For(nameof(YsmLoaderService));

    private static readonly JsonDocumentOptions s_jsonDocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
    };

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
        var file = JsonSerializer.Deserialize(jsonData, YsmJsonContext.Default.MinecraftGeometryFile)
                   ?? throw new InvalidOperationException("Failed to parse geometry JSON");

        if (file.Geometries is not { Count: > 0 })
            throw new InvalidOperationException("No geometry definitions found");

        return file.Geometries;
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
        return LoadDocumentFromBytes(File.ReadAllBytes(filePath));
    }

    public static YsmModelDocument LoadDocumentFromBytes(byte[] data)
    {
        return LoadDocument(ParseWithGuards(data));
    }

    public static YsmModelDocument LoadDocumentForThumbnail(byte[] data)
    {
        return LoadDocumentThumbnail(ParseWithGuards(data));
    }

    private static CoreDocument ParseWithGuards(byte[] data)
    {
        var container = YsmFile.Detect(data);
        if (container == YsmContainerKind.NewContainer)
            throw new NotSupportedException("This file uses the new YSGP2 asset container, which is not supported yet.");
        if (container == YsmContainerKind.Unsupported)
            throw new InvalidOperationException("Unrecognized YSM file format.");

        return YsmFile.Parse(data, new YsmParseOptions(Diagnostics: YsmParserLogDiagnostics.Instance));
    }

    /// <summary>
    /// Resources of a parsed document, with plain-zip entries re-sanitized
    /// (macOS junk dropped, flat-rule misfits re-routed) — see
    /// <see cref="SanitizeZipResources"/>.
    /// </summary>
    public static YsmResourceData GetSanitizedResources(CoreDocument document) =>
        document.Container == YsmContainerKind.ZipArchive
            ? SanitizeZipResources(document.Resources)
            : document.Resources;

    private static int ContainerToVersion(YsmContainerKind container) => container switch
    {
        YsmContainerKind.LegacyV1 => 1,
        YsmContainerKind.LegacyV2 => 2,
        YsmContainerKind.EncryptedV3 => 3,
        _ => 0,
    };

    private static YsmModelDocument LoadDocumentThumbnail(CoreDocument document)
    {
        var resources = GetSanitizedResources(document);

        if (resources.Models.Count == 0)
            throw new InvalidOperationException("No models found in YSM file");

        var meta = YsmMetadataParser.Parse(resources.YsmJson, resources.InfoJson);

        var info = new YsmDocumentModelInfo(
            Name: meta?.Name ?? "Unknown",
            DisplayName: MinecraftFormatHelper.StripFormatting(meta?.Name ?? "Unknown"),
            Version: ContainerToVersion(document.Container),
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

    private static YsmModelDocument LoadDocument(CoreDocument document)
    {
        var resources = GetSanitizedResources(document);

        if (resources.Models.Count == 0)
            throw new InvalidOperationException("No models found in YSM file");

        var meta = YsmMetadataParser.Parse(resources.YsmJson, resources.InfoJson);

        var info = new YsmDocumentModelInfo(
            Name: meta?.Name ?? "Unknown",
            DisplayName: MinecraftFormatHelper.StripFormatting(meta?.Name ?? "Unknown"),
            Version: ContainerToVersion(document.Container),
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
                Logger.LogWarning(ex, "Skipping non-geometry model '{ModelName}'", modelEntry.Name);
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

        var extraAnimations = ParseExtraAnimations(resources.YsmJson, resources.InfoJson, animationResources.Select(a => a.Name));
        return new YsmModelDocument(info, models, textureResources, animationResources, imageResources, animControllerResources, soundResources, functionResources, extraAnimations);
    }

    private static YsmExtraAnimationLayout ParseExtraAnimations(byte[]? ysmJson, byte[]? infoJson, IEnumerable<string> animationNames)
    {
        List<YsmExtraAnimationEntry> rootEntries = [];
        List<YsmExtraAnimationGroup> groups = [];
        List<YsmExtraAnimationButtonDefinition> buttons = [];

        if (ysmJson is { Length: > 0 })
        {
            try
            {
                using var doc = JsonDocument.Parse(ysmJson, s_jsonDocOptions);
                if (doc.RootElement.TryGetProperty("properties", out var props))
                {
                    rootEntries = props.TryGetProperty("extra_animation", out var rootAnim)
                        ? ParseExtraAnimationObject(rootAnim, string.Empty)
                        : [];

                    groups = props.TryGetProperty("extra_animation_classify", out var classify)
                        ? ParseExtraAnimationGroups(classify)
                        : [];

                    buttons = props.TryGetProperty("extra_animation_buttons", out var buttonsElement)
                        ? ParseExtraAnimationButtonStubs(buttonsElement)
                        : [];
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to parse extra animations from ysmJson");
                rootEntries = [];
                groups = [];
                buttons = [];
            }
        }

        if (rootEntries.Count == 0)
            rootEntries = ParseLegacyExtraAnimationNames(infoJson);

        if (rootEntries.Count == 0)
            rootEntries = BuildDefaultExtraAnimations(animationNames);

        if (rootEntries.Count == 0 && groups.Count == 0 && buttons.Count == 0)
            return YsmExtraAnimationLayout.Empty;

        return new YsmExtraAnimationLayout(rootEntries, groups, buttons);
    }

    private static List<YsmExtraAnimationEntry> ParseLegacyExtraAnimationNames(byte[]? infoJson)
    {
        var result = new List<YsmExtraAnimationEntry>();
        if (infoJson is not { Length: > 0 })
            return result;

        try
        {
            using var doc = JsonDocument.Parse(infoJson, s_jsonDocOptions);
            if (!doc.RootElement.TryGetProperty("extra_animation_names", out var extras) || extras.ValueKind != JsonValueKind.Array)
                return result;

            int index = 0;
            foreach (var item in extras.EnumerateArray())
            {
                var key = $"extra{index}";
                var displayName = item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : string.Empty;
                result.Add(new YsmExtraAnimationEntry(key, string.IsNullOrWhiteSpace(displayName) ? key : displayName, string.Empty, index, null));
                index++;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to parse legacy extra animation names");
            result.Clear();
        }

        return result;
    }

    private static List<YsmExtraAnimationEntry> BuildDefaultExtraAnimations(IEnumerable<string> animationNames)
    {
        var available = new HashSet<string>(animationNames, StringComparer.OrdinalIgnoreCase);
        var result = new List<YsmExtraAnimationEntry>();

        for (int i = 0; i < 8; i++)
        {
            var key = $"extra{i}";
            if (!available.Contains(key))
                continue;

            result.Add(new YsmExtraAnimationEntry(key, key, string.Empty, i, null));
        }

        return result;
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

            if (key.Equals("#return", StringComparison.OrdinalIgnoreCase) || key.StartsWith('#'))
            {
                index++;
                continue;
            }

            var configGroupId = displayName.StartsWith('#') && displayName.Length > 1
                ? displayName[1..]
                : null;

            result.Add(new YsmExtraAnimationEntry(
                key,
                string.IsNullOrWhiteSpace(displayName) || configGroupId is not null ? key : displayName,
                category,
                index,
                configGroupId));
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
                GetOptionalString(item, "description") ?? string.Empty,
                ParseExtraAnimationForms(item)));
        }

        return result;
    }

    private static List<YsmExtraAnimationForm> ParseExtraAnimationForms(JsonElement buttonElement)
    {
        var result = new List<YsmExtraAnimationForm>();
        if (!buttonElement.TryGetProperty("config_forms", out var formsElement) || formsElement.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var formElement in formsElement.EnumerateArray())
        {
            if (formElement.ValueKind != JsonValueKind.Object)
                continue;

            var type = GetOptionalString(formElement, "type") ?? string.Empty;
            var title = GetOptionalString(formElement, "title") ?? string.Empty;
            var description = GetOptionalString(formElement, "description") ?? string.Empty;
            var value = GetOptionalString(formElement, "value") ?? string.Empty;
            var step = GetOptionalFloat(formElement, "step");
            var min = GetOptionalFloat(formElement, "min");
            var max = GetOptionalFloat(formElement, "max");
            var labels = ParseRadioLabels(formElement);

            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(value))
                continue;

            result.Add(new YsmExtraAnimationForm(type, title, description, value, step, min, max, labels));
        }

        return result;
    }

    private static List<YsmExtraAnimationRadioOption> ParseRadioLabels(JsonElement formElement)
    {
        var result = new List<YsmExtraAnimationRadioOption>();
        if (!formElement.TryGetProperty("labels", out var labelsElement) || labelsElement.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var label in labelsElement.EnumerateObject())
        {
            var expression = label.Value.ValueKind == JsonValueKind.String
                ? label.Value.GetString() ?? string.Empty
                : string.Empty;
            if (!string.IsNullOrWhiteSpace(expression))
                result.Add(new YsmExtraAnimationRadioOption(label.Name, expression));
        }

        return result;
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static float GetOptionalFloat(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return 0f;
        if (property.ValueKind == JsonValueKind.Number && property.TryGetSingle(out var value))
            return value;
        if (property.ValueKind == JsonValueKind.String && float.TryParse(property.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
            return value;
        return 0f;
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

    /// <summary>
    /// Restores the viewer's plain-zip classification semantics on top of the
    /// library's directory/extension rules (Core 1.1.0
    /// <c>Output.ResourceClassifier</c>): drops macOS junk entries
    /// (<c>__MACOSX/</c>, <c>._*</c> resource forks) and re-routes entries the
    /// flat rules cannot place (.webp textures, .lang/.mcfunction files,
    /// audio, specular/special images, root animation controllers).
    /// </summary>
    public static YsmResourceData SanitizeZipResources(YsmResourceData resources)
    {
        var models = new List<YsmResourceEntry>();
        var textures = new List<YsmResourceEntry>();
        var animations = new List<YsmResourceEntry>();
        var animControllers = new List<YsmResourceEntry>(resources.AnimationControllers.Where(e => !IsMacOsJunk(e.Name)));
        var sounds = new List<YsmResourceEntry>();
        var functions = new List<YsmResourceEntry>();
        var languages = new List<YsmResourceEntry>();
        var avatars = new List<YsmResourceEntry>(resources.Avatars.Where(e => !IsMacOsJunk(e.Name)));
        var backgrounds = new List<YsmResourceEntry>(resources.Backgrounds.Where(e => !IsMacOsJunk(e.Name)));
        var specialImages = new List<YsmResourceEntry>(resources.SpecialImages.Where(e => !IsMacOsJunk(e.Name)));

        foreach (var entry in resources.Models)
        {
            if (IsMacOsJunk(entry.Name)) continue;
            var name = entry.Name.Replace('\\', '/');

            if (name.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                textures.Add(entry);
            else if (name.EndsWith(".lang", StringComparison.OrdinalIgnoreCase))
                languages.Add(entry);
            else if (name.EndsWith(".mcfunction", StringComparison.OrdinalIgnoreCase))
                functions.Add(entry);
            else if (IsAudioFile(name))
                sounds.Add(entry);
            else
                models.Add(entry);
        }

        foreach (var entry in resources.Textures)
        {
            if (IsMacOsJunk(entry.Name)) continue;
            var name = entry.Name.Replace('\\', '/');

            if (name.StartsWith("special/", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("specular", StringComparison.OrdinalIgnoreCase))
                specialImages.Add(entry);
            else
                textures.Add(entry);
        }

        foreach (var entry in resources.Animations)
        {
            if (IsMacOsJunk(entry.Name)) continue;

            if (entry.Name.Contains("animation_controller", StringComparison.OrdinalIgnoreCase))
                animControllers.Add(entry);
            else
                animations.Add(entry);
        }

        foreach (var entry in resources.Sounds)
            if (!IsMacOsJunk(entry.Name))
                sounds.Add(entry);
        foreach (var entry in resources.Functions)
            if (!IsMacOsJunk(entry.Name))
                functions.Add(entry);
        foreach (var entry in resources.Languages)
            if (!IsMacOsJunk(entry.Name))
                languages.Add(entry);

        return resources with
        {
            Models = models,
            Textures = textures,
            Animations = animations,
            AnimationControllers = animControllers,
            Sounds = sounds,
            Functions = functions,
            Languages = languages,
            Avatars = avatars,
            Backgrounds = backgrounds,
            SpecialImages = specialImages,
        };
    }

    private static bool IsMacOsJunk(string name)
    {
        var normalized = name.Replace('\\', '/');
        if (normalized.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase))
            return true;

        var slash = normalized.LastIndexOf('/');
        var file = slash >= 0 ? normalized[(slash + 1)..] : normalized;
        return file.StartsWith("._", StringComparison.Ordinal);
    }

    private static bool IsAudioFile(string name) =>
        name.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);
}
