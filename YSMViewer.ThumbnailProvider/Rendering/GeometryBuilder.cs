using System.Numerics;
using YSMViewer.Models;
using YSMViewer.Models.Document;

namespace YSMViewer.ThumbnailProvider.Rendering;

public static class GeometryBuilder
{
    private const float ExportScale = 1f / 16f;

    public sealed record TexturedFace(
        Vector3 P0, Vector3 P1, Vector3 P2, Vector3 P3,
        YsmTextureResource? Texture,
        Vector3 WorldNormal,
        float U0, float V0, float U1, float V1, float U2, float V2, float U3, float V3);

    public sealed record ThumbnailScene(
        IReadOnlyList<TexturedFace> Faces,
        Vector3 BoundsMin,
        Vector3 BoundsMax);

    public static ThumbnailScene Build(YsmModelDocument document)
    {
        int estimatedFaces = 0;
        foreach (var m in document.Models)
            if (m.DefaultVisible)
                foreach (var b in m.Bones)
                    estimatedFaces += b.Cubes.Count * 6;

        var allFaces = new List<TexturedFace>(estimatedFaces);
        float bMinX = float.MaxValue, bMinY = float.MaxValue, bMinZ = float.MaxValue;
        float bMaxX = float.MinValue, bMaxY = float.MinValue, bMaxZ = float.MinValue;

        YsmTextureResource? defaultTex = document.Textures.Count > 0 ? document.Textures[0] : null;

        Dictionary<string, YsmTextureResource>? texById = null;
        if (document.Textures.Count > 1)
        {
            texById = new Dictionary<string, YsmTextureResource>(document.Textures.Count);
            foreach (var t in document.Textures)
                if (t.Id is not null)
                    texById[t.Id] = t;
        }

        foreach (var geoModel in document.Models)
        {
            if (!geoModel.DefaultVisible)
                continue;

            YsmTextureResource? texResource;
            if (geoModel.TextureId is not null && texById is not null && texById.TryGetValue(geoModel.TextureId, out var matched))
                texResource = matched;
            else if (geoModel.TextureId is null && defaultTex is not null)
                texResource = defaultTex;
            else
                texResource = defaultTex;
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
                    var faces = BuildCubeFaces(cube, cubeWorld, texResource, texW, texH);
                    foreach (var f in faces)
                    {
                        allFaces.Add(f);
                        float fMinX = MathF.Min(MathF.Min(f.P0.X, f.P1.X), MathF.Min(f.P2.X, f.P3.X));
                        float fMinY = MathF.Min(MathF.Min(f.P0.Y, f.P1.Y), MathF.Min(f.P2.Y, f.P3.Y));
                        float fMinZ = MathF.Min(MathF.Min(f.P0.Z, f.P1.Z), MathF.Min(f.P2.Z, f.P3.Z));
                        float fMaxX = MathF.Max(MathF.Max(f.P0.X, f.P1.X), MathF.Max(f.P2.X, f.P3.X));
                        float fMaxY = MathF.Max(MathF.Max(f.P0.Y, f.P1.Y), MathF.Max(f.P2.Y, f.P3.Y));
                        float fMaxZ = MathF.Max(MathF.Max(f.P0.Z, f.P1.Z), MathF.Max(f.P2.Z, f.P3.Z));
                        bMinX = MathF.Min(bMinX, fMinX);
                        bMinY = MathF.Min(bMinY, fMinY);
                        bMinZ = MathF.Min(bMinZ, fMinZ);
                        bMaxX = MathF.Max(bMaxX, fMaxX);
                        bMaxY = MathF.Max(bMaxY, fMaxY);
                        bMaxZ = MathF.Max(bMaxZ, fMaxZ);
                    }
                }
            }
        }

        if (allFaces.Count == 0)
            return new ThumbnailScene(allFaces, new Vector3(-0.5f), new Vector3(0.5f));

        var boundsMin = new Vector3(bMinX, bMinY, bMinZ);
        var boundsMax = new Vector3(bMaxX, bMaxY, bMaxZ);

        return new ThumbnailScene(allFaces, boundsMin, boundsMax);
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
        YsmCubeInfo cube, Matrix4x4 worldMatrix, YsmTextureResource? texture, float texW, float texH)
    {
        var cubeUV = cube.Uv;
        if (cubeUV?.IsBoxUV == true && cube.Size != Vector3.Zero)
            cubeUV = cubeUV.Expand(cube.Size.X, cube.Size.Y, cube.Size.Z);

        float ox = cube.Origin.X, oy = cube.Origin.Y, oz = cube.Origin.Z;
        float sx = cube.Size.X, sy = cube.Size.Y, sz = cube.Size.Z;
        float inflate = cube.Inflate;
        float px = cube.Pivot.X, py = cube.Pivot.Y, pz = cube.Pivot.Z;

        // Algebraically simplified from: center +/- halfSize +/- inflate - pivot
        float minX = ox - sx - inflate - px;
        float minY = oy - inflate - py;
        float minZ = oz - inflate - pz;
        float maxX = ox + inflate - px;
        float maxY = oy + sy + inflate - py;
        float maxZ = oz + sz + inflate - pz;

        if (maxX == minX) maxX += 0.001f;
        if (maxY == minY) maxY += 0.001f;
        if (maxZ == minZ) maxZ += 0.001f;

        float lx = minX * ExportScale;
        float ly = minY * ExportScale;
        float lz = minZ * ExportScale;
        float hx = maxX * ExportScale;
        float hy = maxY * ExportScale;
        float hz = maxZ * ExportScale;

        float tw = texW > 0 ? texW : 64f;
        float th = texH > 0 ? texH : 64f;

        var faces = new List<TexturedFace>();

        AddTransformedFace(faces, worldMatrix, texture, hx, hy, hz, hx, hy, lz, hx, ly, lz, hx, ly, hz,
            Vector3.UnitX, GetFaceUV(cubeUV?.East, tw, th));
        AddTransformedFace(faces, worldMatrix, texture, lx, hy, lz, lx, hy, hz, lx, ly, hz, lx, ly, lz,
            -Vector3.UnitX, GetFaceUV(cubeUV?.West, tw, th));
        AddTransformedFace(faces, worldMatrix, texture, lx, hy, lz, hx, hy, lz, hx, hy, hz, lx, hy, hz,
            Vector3.UnitY, GetFaceUV(cubeUV?.Up, tw, th));
        AddTransformedFace(faces, worldMatrix, texture, lx, ly, hz, hx, ly, hz, hx, ly, lz, lx, ly, lz,
            -Vector3.UnitY, GetFaceUV(cubeUV?.Down, tw, th));
        AddTransformedFace(faces, worldMatrix, texture, lx, hy, hz, hx, hy, hz, hx, ly, hz, lx, ly, hz,
            Vector3.UnitZ, GetFaceUV(cubeUV?.South, tw, th));
        AddTransformedFace(faces, worldMatrix, texture, hx, hy, lz, lx, hy, lz, lx, ly, lz, hx, ly, lz,
            -Vector3.UnitZ, GetFaceUV(cubeUV?.North, tw, th));

        return faces;
    }

    private static void AddTransformedFace(
        List<TexturedFace> faces,
        Matrix4x4 worldMatrix,
        YsmTextureResource? texture,
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

        faces.Add(new TexturedFace(v0, v1, v2, v3, texture, worldNormal, uv.u0, uv.v0, uv.u1, uv.v1, uv.u2, uv.v2, uv.u3, uv.v3));
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
