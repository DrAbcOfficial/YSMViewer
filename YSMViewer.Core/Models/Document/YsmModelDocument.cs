using System.Numerics;

namespace YSMViewer.Models.Document;

public enum YsmModelCategory
{
    Main,
    Arm,
    SubEntity,
}

public sealed record YsmModelDocument(
    YsmDocumentModelInfo Info,
    IReadOnlyList<YsmGeometryModel> Models,
    IReadOnlyList<YsmTextureResource> Textures,
    IReadOnlyList<YsmAnimationResource> Animations,
    IReadOnlyList<YsmImageResource> Images,
    IReadOnlyList<YsmAnimationControllerResource> AnimControllers,
    IReadOnlyList<YsmSoundResource> Sounds,
    IReadOnlyList<YsmFunctionResource> Functions,
    YsmExtraAnimationLayout ExtraAnimations);

public sealed record YsmExtraAnimationLayout(
    IReadOnlyList<YsmExtraAnimationEntry> RootEntries,
    IReadOnlyList<YsmExtraAnimationGroup> Groups,
    IReadOnlyList<YsmExtraAnimationButtonDefinition> ButtonDefinitions)
{
    public static YsmExtraAnimationLayout Empty { get; } = new([], [], []);

    public bool HasEntries => RootEntries.Count > 0 || Groups.Any(g => g.Entries.Count > 0);
}

public sealed record YsmExtraAnimationGroup(
    string Id,
    string DisplayName,
    IReadOnlyList<YsmExtraAnimationEntry> Entries);

public sealed record YsmExtraAnimationEntry(
    string Key,
    string DisplayName,
    string Category,
    int OriginalIndex,
    string? ConfigGroupId);

public sealed record YsmExtraAnimationButtonDefinition(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<YsmExtraAnimationForm> Forms);

public sealed record YsmExtraAnimationForm(
    string Type,
    string Title,
    string Description,
    string Value,
    float Step,
    float Min,
    float Max,
    IReadOnlyList<YsmExtraAnimationRadioOption> Labels);

public sealed record YsmExtraAnimationRadioOption(
    string Label,
    string Expression);

public sealed record YsmDocumentModelInfo(
    string Name,
    string DisplayName,
    int Version,
    string Authors,
    string License,
    string Tips,
    bool IsFree);

public sealed record YsmGeometryModel(
    string Id,
    string Name,
    YsmModelCategory Category,
    bool DefaultVisible,
    string GeometryIdentifier,
    float TextureWidth,
    float TextureHeight,
    string? TextureId,
    IReadOnlyList<YsmBoneInfo> Bones);

public sealed record YsmBoneInfo(
    string Id,
    string Name,
    string? ParentId,
    Vector3 Pivot,
    Vector3 Rotation,
    IReadOnlyList<YsmCubeInfo> Cubes);

public sealed record YsmCubeInfo(
    string Id,
    Vector3 Origin,
    Vector3 Size,
    Vector3 Pivot,
    Vector3 Rotation,
    float Inflate,
    MinecraftCubeUV? Uv);

public sealed record YsmTextureResource(
    string Id,
    string Name,
    byte[] Data,
    int Width,
    int Height);

public sealed record YsmAnimationResource(
    string Name,
    byte[] Data);

public sealed record YsmImageResource(
    string Name,
    string Category,
    byte[] Data,
    int Width,
    int Height);
