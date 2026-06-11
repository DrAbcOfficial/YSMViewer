using System.Numerics;
using System.Text.Json;
using YSMViewer.Models;
using YSMViewer.Models.Document;

namespace YSMViewer.Rendering.ThreeJs;

public static class ThreeJsPayloadBuilder
{
    private const float ExportScale = 1f / 16f;

    public sealed record ThreeJsModelSpec(List<ThreeJsModelGroup> Models);

    public sealed record ThreeJsModelGroup(
        string Id,
        string Name,
        bool DefaultVisible,
        float TextureWidth,
        float TextureHeight,
        string? TextureId,
        List<ThreeJsBoneData> Bones,
        List<ThreeJsMeshData> MeshGroups);

    public sealed record ThreeJsBoneData(
        string Id,
        string Name,
        string? ParentId,
        float[] LocalPosition,
        float[] LocalRotation);

    public sealed record ThreeJsMeshData(
        string Id,
        string BoneId,
        float[] LocalPosition,
        float[] LocalRotation,
        float[] Positions,
        float[] Normals,
        float[] Uvs,
        int[] Indices);

    public static string BuildSpecJson(YsmModelDocument document)
    {
        var models = new List<ThreeJsModelGroup>();

        foreach (var geoModel in document.Models)
        {
            if (!geoModel.DefaultVisible)
                continue;

            var bones = new List<ThreeJsBoneData>();
            var meshGroups = new List<ThreeJsMeshData>();
            var bonePivots = new Dictionary<string, Vector3>();

            foreach (var bone in geoModel.Bones)
                bonePivots[$"{geoModel.Id}:{bone.Id}"] = bone.Pivot;

            foreach (var bone in geoModel.Bones)
            {
                var boneId = $"{geoModel.Id}:{bone.Id}";
                var parentId = bone.ParentId is not null ? $"{geoModel.Id}:{bone.ParentId}" : null;
                var localPosition = parentId is not null && bonePivots.TryGetValue(parentId, out var parentPivot)
                    ? (bone.Pivot - parentPivot) * ExportScale
                    : bone.Pivot * ExportScale;

                bones.Add(new ThreeJsBoneData(
                    Id: boneId,
                    Name: bone.Name,
                    ParentId: parentId,
                    LocalPosition: ToArray(localPosition),
                    LocalRotation: ToArray(CreateBlockbenchQuaternion(bone.Rotation))));

                int cubeIdx = 0;

                foreach (var cube in bone.Cubes)
                {
                    var meshData = BuildCubeMeshData(
                        cube, bone.Pivot,
                        geoModel.TextureWidth, geoModel.TextureHeight,
                        $"{boneId}_{cubeIdx}", boneId);
                    if (meshData is not null)
                        meshGroups.Add(meshData);
                    cubeIdx++;
                }
            }

            var texIdx = ResolveTextureId(geoModel, document);
            models.Add(new ThreeJsModelGroup(
                Id: geoModel.Id,
                Name: geoModel.Name,
                DefaultVisible: geoModel.DefaultVisible,
                TextureWidth: geoModel.TextureWidth,
                TextureHeight: geoModel.TextureHeight,
                TextureId: texIdx,
                Bones: bones,
                MeshGroups: meshGroups));
        }

        var spec = new ThreeJsModelSpec(models);
        return JsonSerializer.Serialize(spec, ThreeJsSpecJsonContext.Default.ThreeJsModelSpec);
    }

    public static HashSet<string> GetRequiredTextureIds(YsmModelDocument document)
    {
        var ids = new HashSet<string>();
        foreach (var model in document.Models)
        {
            var texId = ResolveTextureId(model, document);
            if (texId is not null)
                ids.Add(texId);
        }
        return ids;
    }

    private static string? ResolveTextureId(YsmGeometryModel model, YsmModelDocument document)
    {
        if (model.TextureId is not null)
        {
            var match = document.Textures.FirstOrDefault(t => t.Id == model.TextureId);
            if (match is not null)
                return match.Id;
        }
        if (document.Textures.Count > 0)
            return document.Textures[0].Id;
        return null;
    }

    private static ThreeJsMeshData? BuildCubeMeshData(
        YsmCubeInfo cube,
        Vector3 bonePivot,
        float texW, float texH,
        string meshId, string boneId)
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

        float lx = min.X * ExportScale;
        float ly = min.Y * ExportScale;
        float lz = min.Z * ExportScale;
        float hx = max.X * ExportScale;
        float hy = max.Y * ExportScale;
        float hz = max.Z * ExportScale;

        if (lx == hx) hx += 0.001f;
        if (ly == hy) hy += 0.001f;
        if (lz == hz) hz += 0.001f;

        float tw = texW > 0 ? texW : 64f;
        float th = texH > 0 ? texH : 64f;

        var positions = new List<float>();
        var normals = new List<float>();
        var uvs = new List<float>();
        var indices = new List<int>();

        AddQuadFace(positions, normals, uvs, indices,
            hx, hy, hz, hx, hy, lz, hx, ly, hz, hx, ly, lz,
            1, 0, 0, GetFaceUV(cubeUV?.East, tw, th));

        AddQuadFace(positions, normals, uvs, indices,
            lx, hy, lz, lx, hy, hz, lx, ly, lz, lx, ly, hz,
            -1, 0, 0, GetFaceUV(cubeUV?.West, tw, th));

        AddQuadFace(positions, normals, uvs, indices,
            lx, hy, lz, hx, hy, lz, lx, hy, hz, hx, hy, hz,
            0, 1, 0, GetFaceUV(cubeUV?.Up, tw, th));

        AddQuadFace(positions, normals, uvs, indices,
            lx, ly, hz, hx, ly, hz, lx, ly, lz, hx, ly, lz,
            0, -1, 0, GetFaceUV(cubeUV?.Down, tw, th));

        AddQuadFace(positions, normals, uvs, indices,
            lx, hy, hz, hx, hy, hz, lx, ly, hz, hx, ly, hz,
            0, 0, 1, GetFaceUV(cubeUV?.South, tw, th));

        AddQuadFace(positions, normals, uvs, indices,
            hx, hy, lz, lx, hy, lz, hx, ly, lz, lx, ly, lz,
            0, 0, -1, GetFaceUV(cubeUV?.North, tw, th));

        var localPosition = (cube.Pivot - bonePivot) * ExportScale;

        return new ThreeJsMeshData(
            Id: meshId,
            BoneId: boneId,
            LocalPosition: ToArray(localPosition),
            LocalRotation: ToArray(CreateBlockbenchQuaternion(cube.Rotation)),
            Positions: [.. positions],
            Normals: [.. normals],
            Uvs: [.. uvs],
            Indices: [.. indices]);
    }

    private static void AddQuadFace(
        List<float> positions, List<float> normals, List<float> uvs, List<int> indices,
        float x0, float y0, float z0, float x1, float y1, float z1,
        float x2, float y2, float z2, float x3, float y3, float z3,
        float nx, float ny, float nz,
        (float u0, float v0, float u1, float v1, float u2, float v2, float u3, float v3) uv)
    {
        int baseIdx = positions.Count / 3;

        positions.AddRange([x0, y0, z0]);
        positions.AddRange([x1, y1, z1]);
        positions.AddRange([x2, y2, z2]);
        positions.AddRange([x3, y3, z3]);

        for (int i = 0; i < 4; i++)
            normals.AddRange([nx, ny, nz]);

        uvs.AddRange([uv.u0, uv.v0]);
        uvs.AddRange([uv.u1, uv.v1]);
        uvs.AddRange([uv.u2, uv.v2]);
        uvs.AddRange([uv.u3, uv.v3]);

        indices.AddRange([baseIdx, baseIdx + 2, baseIdx + 1]);
        indices.AddRange([baseIdx + 2, baseIdx + 3, baseIdx + 1]);
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

    private static Quaternion CreateBlockbenchQuaternion(Vector3 eulerDegrees)
    {
        float rx = eulerDegrees.X * MathF.PI / 180f;
        float ry = eulerDegrees.Y * MathF.PI / 180f;
        float rz = eulerDegrees.Z * MathF.PI / 180f;
        var m = Matrix4x4.CreateRotationX(rx)
              * Matrix4x4.CreateRotationY(ry)
              * Matrix4x4.CreateRotationZ(rz);
        return Quaternion.CreateFromRotationMatrix(m);
    }

    private static float[] ToArray(Vector3 value) => [value.X, value.Y, value.Z];

    private static float[] ToArray(Quaternion value) => [value.X, value.Y, value.Z, value.W];
}
