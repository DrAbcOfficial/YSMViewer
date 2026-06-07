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
        List<ThreeJsMeshData> MeshGroups);

    public sealed record ThreeJsMeshData(
        string Id,
        string BoneId,
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

            var boneMap = new Dictionary<string, YsmBoneInfo>();
            foreach (var bone in geoModel.Bones)
                boneMap[bone.Id] = bone;

            var meshGroups = new List<ThreeJsMeshData>();

            foreach (var bone in geoModel.Bones)
            {
                var worldMatrix = ComputeBoneWorldMatrix(bone, boneMap);
                int cubeIdx = 0;

                foreach (var cube in bone.Cubes)
                {
                    var meshData = BuildCubeMeshData(
                        cube, worldMatrix,
                        geoModel.TextureWidth, geoModel.TextureHeight,
                        $"{bone.Id}_{cubeIdx}", bone.Id);
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

    private static Matrix4x4 ComputeBoneWorldMatrix(
        YsmBoneInfo bone,
        Dictionary<string, YsmBoneInfo> boneMap)
    {
        var local = ComputeBoneLocalMatrix(bone);
        if (bone.ParentId is not null && boneMap.TryGetValue(bone.ParentId, out var parent))
            return local * ComputeBoneWorldMatrix(parent, boneMap);
        return local;
    }

    private static Matrix4x4 ComputeBoneLocalMatrix(YsmBoneInfo bone)
    {
        var translation = bone.Pivot * ExportScale;
        var rot = bone.Rotation * MathF.PI / 180f;
        var rotation = Matrix4x4.CreateRotationX(rot.X)
                     * Matrix4x4.CreateRotationY(rot.Y)
                     * Matrix4x4.CreateRotationZ(rot.Z);
        var mat = Matrix4x4.CreateTranslation(translation) * rotation;

        if (bone.ParentId is not null)
        {
            var parentPivot = bone.Pivot * ExportScale;
            mat = mat * Matrix4x4.CreateTranslation(-parentPivot);
        }

        return mat;
    }

    private static ThreeJsMeshData? BuildCubeMeshData(
        YsmCubeInfo cube,
        Matrix4x4 boneWorldMatrix,
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

        var cubeLocalPos = -cube.Pivot * ExportScale;
        var cubeRotMat = cube.Rotation != Vector3.Zero
            ? Matrix4x4.CreateRotationX(cube.Rotation.X * MathF.PI / 180f)
            * Matrix4x4.CreateRotationY(cube.Rotation.Y * MathF.PI / 180f)
            * Matrix4x4.CreateRotationZ(cube.Rotation.Z * MathF.PI / 180f)
            : Matrix4x4.Identity;

        var localMatrix = Matrix4x4.CreateTranslation(cubeLocalPos) * cubeRotMat;
        var worldMatrix = localMatrix * boneWorldMatrix;

        float tw = texW > 0 ? texW : 64f;
        float th = texH > 0 ? texH : 64f;

        var positions = new List<float>();
        var normals = new List<float>();
        var uvs = new List<float>();
        var indices = new List<int>();

        AddQuadFace(positions, normals, uvs, indices, worldMatrix,
            lx, hy, hz, hx, hy, hz, lx, hy, lz, hx, hy, lz,
            0, 1, 0, GetFaceUV(cubeUV?.Up, tw, th));

        AddQuadFace(positions, normals, uvs, indices, worldMatrix,
            hx, ly, hz, lx, ly, hz, hx, ly, lz, lx, ly, lz,
            0, -1, 0, GetFaceUV(cubeUV?.Down, tw, th));

        AddQuadFace(positions, normals, uvs, indices, worldMatrix,
            hx, hy, hz, hx, hy, lz, hx, ly, hz, hx, ly, lz,
            1, 0, 0, GetFaceUV(cubeUV?.East, tw, th));

        AddQuadFace(positions, normals, uvs, indices, worldMatrix,
            lx, hy, lz, lx, hy, hz, lx, ly, lz, lx, ly, hz,
            -1, 0, 0, GetFaceUV(cubeUV?.West, tw, th));

        AddQuadFace(positions, normals, uvs, indices, worldMatrix,
            lx, hy, hz, hx, hy, hz, lx, ly, hz, hx, ly, hz,
            0, 0, 1, GetFaceUV(cubeUV?.South, tw, th));

        AddQuadFace(positions, normals, uvs, indices, worldMatrix,
            hx, hy, lz, lx, hy, lz, hx, ly, lz, lx, ly, lz,
            0, 0, -1, GetFaceUV(cubeUV?.North, tw, th));

        return new ThreeJsMeshData(
            Id: meshId,
            BoneId: boneId,
            Positions: positions.ToArray(),
            Normals: normals.ToArray(),
            Uvs: uvs.ToArray(),
            Indices: indices.ToArray());
    }

    private static void AddQuadFace(
        List<float> positions, List<float> normals, List<float> uvs, List<int> indices,
        Matrix4x4 worldMatrix,
        float x0, float y0, float z0, float x1, float y1, float z1,
        float x2, float y2, float z2, float x3, float y3, float z3,
        float nx, float ny, float nz,
        (float u0, float v0, float u1, float v1, float u2, float v2, float u3, float v3) uv)
    {
        var v0 = Vector3.Transform(new Vector3(x0, y0, z0), worldMatrix);
        var v1 = Vector3.Transform(new Vector3(x1, y1, z1), worldMatrix);
        var v2 = Vector3.Transform(new Vector3(x2, y2, z2), worldMatrix);
        var v3 = Vector3.Transform(new Vector3(x3, y3, z3), worldMatrix);

        var normal = Vector3.TransformNormal(new Vector3(nx, ny, nz), worldMatrix);
        normal = Vector3.Normalize(normal);

        int baseIdx = positions.Count / 3;

        positions.AddRange([v0.X, v0.Y, v0.Z]);
        positions.AddRange([v1.X, v1.Y, v1.Z]);
        positions.AddRange([v2.X, v2.Y, v2.Z]);
        positions.AddRange([v3.X, v3.Y, v3.Z]);

        for (int i = 0; i < 4; i++)
            normals.AddRange([normal.X, normal.Y, normal.Z]);

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
}
