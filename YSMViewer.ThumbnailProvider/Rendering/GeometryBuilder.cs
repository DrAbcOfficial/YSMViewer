using System.Numerics;
using YSMViewer.Models;
using YSMViewer.Models.Document;

namespace YSMViewer.ThumbnailProvider.Rendering;

public static class GeometryBuilder
{
    private const float ExportScale = 1f / 16f;

    public sealed record TexturedFace(
        Vector3 P0, Vector3 P1, Vector3 P2, Vector3 P3,
        Vector3 WorldNormal,
        float U0, float V0, float U1, float V1, float U2, float V2, float U3, float V3);

    public sealed record ThumbnailScene(
        IReadOnlyList<TexturedFace> Faces,
        YsmTextureResource? Texture,
        Vector3 BoundsMin,
        Vector3 BoundsMax);

    public static ThumbnailScene Build(YsmModelDocument document)
    {
        var allFaces = new List<TexturedFace>();
        YsmTextureResource? resolvedTexture = null;
        var boundsMin = new Vector3(float.MaxValue);
        var boundsMax = new Vector3(float.MinValue);

        foreach (var geoModel in document.Models)
        {
            if (!geoModel.DefaultVisible)
                continue;

            var texResource = ResolveTexture(geoModel, document);
            resolvedTexture ??= texResource;
            var texW = geoModel.TextureWidth > 0 ? geoModel.TextureWidth : 64f;
            var texH = geoModel.TextureHeight > 0 ? geoModel.TextureHeight : 64f;

            var bonePivots = new Dictionary<string, Vector3>();
            var boneWorldMatrices = new Dictionary<string, Matrix4x4>();

            foreach (var bone in geoModel.Bones)
                bonePivots[bone.Id] = bone.Pivot;

            ComputeWorldMatrices(geoModel.Bones, bonePivots, boneWorldMatrices);

            foreach (var bone in geoModel.Bones)
            {
                if (!boneWorldMatrices.TryGetValue(bone.Id, out var boneWorld))
                    continue;

                foreach (var cube in bone.Cubes)
                {
                    var cubeWorld = ComputeCubeWorldMatrix(cube, bone.Pivot, boneWorld);
                    var faces = BuildCubeFaces(cube, cubeWorld, texW, texH);
                    foreach (var f in faces)
                    {
                        allFaces.Add(f);
                        boundsMin = Vector3.Min(boundsMin, Vector3.Min(Vector3.Min(f.P0, f.P1), Vector3.Min(f.P2, f.P3)));
                        boundsMax = Vector3.Max(boundsMax, Vector3.Max(Vector3.Max(f.P0, f.P1), Vector3.Max(f.P2, f.P3)));
                    }
                }
            }
        }

        var texture = resolvedTexture ?? (document.Textures.Count > 0 ? document.Textures[0] : null);

        return new ThumbnailScene(allFaces, texture, boundsMin, boundsMax);
    }

    private static YsmTextureResource? ResolveTexture(YsmGeometryModel model, YsmModelDocument document)
    {
        if (model.TextureId is not null)
        {
            var match = document.Textures.FirstOrDefault(t => t.Id == model.TextureId);
            if (match is not null) return match;
        }
        return document.Textures.Count > 0 ? document.Textures[0] : null;
    }

    private static void ComputeWorldMatrices(
        IReadOnlyList<YsmBoneInfo> bones,
        Dictionary<string, Vector3> bonePivots,
        Dictionary<string, Matrix4x4> worldMatrices)
    {
        var sorted = TopologicalSort(bones);
        foreach (var bone in sorted)
        {
            Matrix4x4 localMatrix;
            if (bone.ParentId is not null && worldMatrices.TryGetValue(bone.ParentId, out var parentWorld))
            {
                var localPos = bone.Pivot - bonePivots[bone.ParentId];
                localMatrix = CreateTransformMatrix(localPos * ExportScale, bone.Rotation, Vector3.One);
                worldMatrices[bone.Id] = localMatrix * parentWorld;
            }
            else
            {
                localMatrix = CreateTransformMatrix(bone.Pivot * ExportScale, bone.Rotation, Vector3.One);
                worldMatrices[bone.Id] = localMatrix;
            }
        }
    }

    private static List<YsmBoneInfo> TopologicalSort(IReadOnlyList<YsmBoneInfo> bones)
    {
        var boneMap = bones.ToDictionary(b => b.Id);
        var visited = new HashSet<string>();
        var sorted = new List<YsmBoneInfo>();

        foreach (var bone in bones)
            Visit(bone, boneMap, visited, sorted);

        return sorted;
    }

    private static void Visit(YsmBoneInfo bone, Dictionary<string, YsmBoneInfo> boneMap,
        HashSet<string> visited, List<YsmBoneInfo> sorted)
    {
        if (visited.Contains(bone.Id)) return;
        visited.Add(bone.Id);

        if (bone.ParentId is not null && boneMap.TryGetValue(bone.ParentId, out var parent))
            Visit(parent, boneMap, visited, sorted);

        sorted.Add(bone);
    }

    private static Matrix4x4 ComputeCubeWorldMatrix(YsmCubeInfo cube, Vector3 bonePivot, Matrix4x4 boneWorld)
    {
        var localPos = (cube.Pivot - bonePivot) * ExportScale;
        var cubeLocal = CreateTransformMatrix(localPos, cube.Rotation, Vector3.One);
        return cubeLocal * boneWorld;
    }

    private static Matrix4x4 CreateTransformMatrix(Vector3 position, Vector3 eulerDegrees, Vector3 scale)
    {
        float rx = eulerDegrees.X * MathF.PI / 180f;
        float ry = eulerDegrees.Y * MathF.PI / 180f;
        float rz = eulerDegrees.Z * MathF.PI / 180f;

        var rot = Matrix4x4.CreateRotationX(rx)
                * Matrix4x4.CreateRotationY(ry)
                * Matrix4x4.CreateRotationZ(rz);

        var scl = Matrix4x4.CreateScale(scale);
        var pos = Matrix4x4.CreateTranslation(position);

        return scl * rot * pos;
    }

    private static List<TexturedFace> BuildCubeFaces(
        YsmCubeInfo cube, Matrix4x4 worldMatrix, float texW, float texH)
    {
        var cubeUV = cube.Uv;
        if (cubeUV?.IsBoxUV == true && cube.Size != Vector3.Zero)
            cubeUV = cubeUV.Expand(cube.Size.X, cube.Size.Y, cube.Size.Z);

        float inflate = cube.Inflate;
        var from = new Vector3(cube.Origin.X - cube.Size.X, cube.Origin.Y, cube.Origin.Z);
        var to = from + cube.Size;

        var center = (from + to) * 0.5f;
        var halfSize = (to - from) * 0.5f;
        var min = center - new Vector3(halfSize.X + inflate, halfSize.Y + inflate, halfSize.Z + inflate) - cube.Pivot;
        var max = center + new Vector3(halfSize.X + inflate, halfSize.Y + inflate, halfSize.Z + inflate) - cube.Pivot;

        if (min.X == max.X) max.X += 0.001f;
        if (min.Y == max.Y) max.Y += 0.001f;
        if (min.Z == max.Z) max.Z += 0.001f;

        float lx = min.X * ExportScale;
        float ly = min.Y * ExportScale;
        float lz = min.Z * ExportScale;
        float hx = max.X * ExportScale;
        float hy = max.Y * ExportScale;
        float hz = max.Z * ExportScale;

        float tw = texW > 0 ? texW : 64f;
        float th = texH > 0 ? texH : 64f;

        var faces = new List<TexturedFace>();

        AddTransformedFace(faces, worldMatrix, hx, hy, hz, hx, hy, lz, hx, ly, lz, hx, ly, hz,
            Vector3.UnitX, GetFaceUV(cubeUV?.East, tw, th));
        AddTransformedFace(faces, worldMatrix, lx, hy, lz, lx, hy, hz, lx, ly, hz, lx, ly, lz,
            -Vector3.UnitX, GetFaceUV(cubeUV?.West, tw, th));
        AddTransformedFace(faces, worldMatrix, lx, hy, lz, hx, hy, lz, hx, hy, hz, lx, hy, hz,
            Vector3.UnitY, GetFaceUV(cubeUV?.Up, tw, th));
        AddTransformedFace(faces, worldMatrix, lx, ly, hz, hx, ly, hz, hx, ly, lz, lx, ly, lz,
            -Vector3.UnitY, GetFaceUV(cubeUV?.Down, tw, th));
        AddTransformedFace(faces, worldMatrix, lx, hy, hz, hx, hy, hz, hx, ly, hz, lx, ly, hz,
            Vector3.UnitZ, GetFaceUV(cubeUV?.South, tw, th));
        AddTransformedFace(faces, worldMatrix, hx, hy, lz, lx, hy, lz, lx, ly, lz, hx, ly, lz,
            -Vector3.UnitZ, GetFaceUV(cubeUV?.North, tw, th));

        return faces;
    }

    private static void AddTransformedFace(
        List<TexturedFace> faces,
        Matrix4x4 worldMatrix,
        float x0, float y0, float z0, float x1, float y1, float z1,
        float x2, float y2, float z2, float x3, float y3, float z3,
        Vector3 localNormal,
        (float u0, float v0, float u1, float v1, float u2, float v2, float u3, float v3) uv)
    {
        var v0 = Vector3.Transform(new Vector3(x0, y0, z0), worldMatrix);
        var v1 = Vector3.Transform(new Vector3(x1, y1, z1), worldMatrix);
        var v2 = Vector3.Transform(new Vector3(x2, y2, z2), worldMatrix);
        var v3 = Vector3.Transform(new Vector3(x3, y3, z3), worldMatrix);
        var worldNormal = Vector3.TransformNormal(localNormal, worldMatrix);

        faces.Add(new TexturedFace(v0, v1, v2, v3, worldNormal, uv.u0, uv.v0, uv.u1, uv.v1, uv.u2, uv.v2, uv.u3, uv.v3));
    }

    private static (float u0, float v0, float u1, float v1, float u2, float v2, float u3, float v3) GetFaceUV(
        MinecraftCubeFaceUV? faceUv, float texW, float texH)
    {
        if (faceUv?.UvCoords is { Count: >= 2 })
        {
            float fu = faceUv.UvCoords[0];
            float fv = faceUv.UvCoords[1];
            float du = faceUv.UvSize is { Count: >= 2 } ? faceUv.UvSize[0] : 0f;
            float dv = faceUv.UvSize is { Count: >= 2 } ? faceUv.UvSize[1] : 0f;

            float u0 = fu / texW;
            float v0 = fv / texH;
            float u1 = (fu + du) / texW;
            float v1 = (fv + dv) / texH;

            return (u0, v0, u1, v0, u0, v1, u1, v1);
        }
        return (0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
    }
}
