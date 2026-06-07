using System.Numerics;

namespace YSMViewer.Rendering.Skia;

internal sealed record TriangleFace(
    Vector3 P0, Vector3 P1, Vector3 P2,
    Vector3 Nrm,
    float U0, float V0, float U1, float V1, float U2, float V2,
    int TextureIndex);

internal sealed class MeshData
{
    public List<TriangleFace> Triangles { get; } = [];
    public List<SkiaSharp.SKBitmap> Textures { get; } = [];
}

internal static class TriangleMeshBuilder
{
    private const float ExportScale = 1f / 16f;

    public static MeshData BuildFromDocument(YSMViewer.Models.Document.YsmModelDocument document)
    {
        var mesh = new MeshData();

        foreach (var tex in document.Textures)
        {
            try
            {
                var bitmap = SkiaSharp.SKBitmap.Decode(tex.Data);
                mesh.Textures.Add(bitmap);
            }
            catch
            {
                mesh.Textures.Add(new SkiaSharp.SKBitmap(1, 1));
            }
        }

        foreach (var geoModel in document.Models)
        {
            if (!geoModel.DefaultVisible)
                continue;

            int texIdx = -1;
            if (geoModel.TextureId is not null)
            {
                texIdx = document.Textures.ToList().FindIndex(t => t.Id == geoModel.TextureId);
                if (texIdx < 0)
                    texIdx = document.Textures.Count > 0 ? 0 : -1;
            }

            var boneMap = new Dictionary<string, YSMViewer.Models.Document.YsmBoneInfo>();
            foreach (var bone in geoModel.Bones)
                boneMap[bone.Id] = bone;

            foreach (var bone in geoModel.Bones)
            {
                var worldMatrix = ComputeBoneWorldMatrix(bone, boneMap);
                int cubeIdx = 0;

                foreach (var cube in bone.Cubes)
                {
                    BuildCubeTriangles(mesh, cube, worldMatrix, geoModel.TextureWidth, geoModel.TextureHeight, texIdx);
                    cubeIdx++;
                }
            }
        }

        return mesh;
    }

    private static Matrix4x4 ComputeBoneWorldMatrix(
        YSMViewer.Models.Document.YsmBoneInfo bone,
        Dictionary<string, YSMViewer.Models.Document.YsmBoneInfo> boneMap)
    {
        var local = ComputeBoneLocalMatrix(bone);
        if (bone.ParentId is not null && boneMap.TryGetValue(bone.ParentId, out var parent))
            return local * ComputeBoneWorldMatrix(parent, boneMap);
        return local;
    }

    private static Matrix4x4 ComputeBoneLocalMatrix(YSMViewer.Models.Document.YsmBoneInfo bone)
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

    private static void BuildCubeTriangles(
        MeshData mesh,
        YSMViewer.Models.Document.YsmCubeInfo cube,
        Matrix4x4 boneWorldMatrix,
        float texW, float texH,
        int texIdx)
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

        var cubeLocalPos = (cube.Pivot - cube.Pivot) * ExportScale;
        var cubeRotMat = cube.Rotation != Vector3.Zero
            ? Matrix4x4.CreateRotationX(cube.Rotation.X * MathF.PI / 180f)
            * Matrix4x4.CreateRotationY(cube.Rotation.Y * MathF.PI / 180f)
            * Matrix4x4.CreateRotationZ(cube.Rotation.Z * MathF.PI / 180f)
            : Matrix4x4.Identity;

        var localMatrix = Matrix4x4.CreateTranslation(cubeLocalPos) * cubeRotMat;
        var worldMatrix = localMatrix * boneWorldMatrix;

        float tw = texW > 0 ? texW : 64f;
        float th = texH > 0 ? texH : 64f;

        AddQuadFace(mesh, worldMatrix,
            lx, hy, hz, hx, hy, hz, lx, hy, lz, hx, hy, lz,
            0, 1, 0, GetFaceUV(cubeUV?.Up, tw, th), texIdx);

        AddQuadFace(mesh, worldMatrix,
            hx, ly, hz, lx, ly, hz, hx, ly, lz, lx, ly, lz,
            0, -1, 0, GetFaceUV(cubeUV?.Down, tw, th), texIdx);

        AddQuadFace(mesh, worldMatrix,
            hx, hy, hz, hx, hy, lz, hx, ly, hz, hx, ly, lz,
            1, 0, 0, GetFaceUV(cubeUV?.East, tw, th), texIdx);

        AddQuadFace(mesh, worldMatrix,
            lx, hy, lz, lx, hy, hz, lx, ly, lz, lx, ly, hz,
            -1, 0, 0, GetFaceUV(cubeUV?.West, tw, th), texIdx);

        AddQuadFace(mesh, worldMatrix,
            lx, hy, hz, hx, hy, hz, lx, ly, hz, hx, ly, hz,
            0, 0, 1, GetFaceUV(cubeUV?.South, tw, th), texIdx);

        AddQuadFace(mesh, worldMatrix,
            hx, hy, lz, lx, hy, lz, hx, ly, lz, lx, ly, lz,
            0, 0, -1, GetFaceUV(cubeUV?.North, tw, th), texIdx);
    }

    private static void AddQuadFace(
        MeshData mesh, Matrix4x4 worldMatrix,
        float x0, float y0, float z0, float x1, float y1, float z1,
        float x2, float y2, float z2, float x3, float y3, float z3,
        float nx, float ny, float nz,
        (float u0, float v0, float u1, float v1, float u2, float v2, float u3, float v3) uv,
        int texIdx)
    {
        var v0 = Vector3.Transform(new Vector3(x0, y0, z0), worldMatrix);
        var v1 = Vector3.Transform(new Vector3(x1, y1, z1), worldMatrix);
        var v2 = Vector3.Transform(new Vector3(x2, y2, z2), worldMatrix);
        var v3 = Vector3.Transform(new Vector3(x3, y3, z3), worldMatrix);

        var normal = Vector3.TransformNormal(new Vector3(nx, ny, nz), worldMatrix);
        normal = Vector3.Normalize(normal);

        // Triangle 1: v0-v2-v1
        mesh.Triangles.Add(new TriangleFace(v0, v2, v1,
            normal,
            uv.u0, uv.v0, uv.u2, uv.v2, uv.u1, uv.v1, texIdx));

        // Triangle 2: v2-v3-v1
        mesh.Triangles.Add(new TriangleFace(v2, v3, v1,
            normal,
            uv.u2, uv.v2, uv.u3, uv.v3, uv.u1, uv.v1, texIdx));
    }

    private static (float u0, float v0, float u1, float v1, float u2, float v2, float u3, float v3) GetFaceUV(
        YSMViewer.Models.MinecraftCubeFaceUV? faceUv, float texW, float texH)
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
