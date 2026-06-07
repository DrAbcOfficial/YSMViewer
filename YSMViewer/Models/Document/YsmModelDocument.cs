using System.Numerics;
using YSMViewer.Models;

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
    IReadOnlyList<YsmImageResource> Images);

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
