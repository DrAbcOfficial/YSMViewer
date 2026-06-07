using Aura3D.Core;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using System.Numerics;
using YSMViewer.Models;
using YSMViewer.Models.Document;

namespace YSMViewer.Rendering.Aura3D;

public static class Aura3DModelBuilder
{
    private const float ExportScale = 1f / 16f;

    public sealed record BuildResult(
        Model RootModel,
        Dictionary<string, Node> BoneNodes,
        Dictionary<string, Vector3> BaseBoneEulers);

    public static BuildResult BuildFromDocument(YsmGeometryModel geoModel, YsmTextureResource? texture)
    {
        var model = new Model { Name = geoModel.Name };
        var boneNodes = new Dictionary<string, Node>();
        var baseEulers = new Dictionary<string, Vector3>();

        Texture? sharedTexture = null;
        if (texture?.Data is { Length: > 0 })
        {
            try
            {
                sharedTexture = TextureLoader.LoadTexture(texture.Data)
                    .SetMinFilter(TextureFilterMode.Nearest)
                    .SetMagFilter(TextureFilterMode.Nearest)
                    .SetWarpS(TextureWrapMode.Repeat)
                    .SetWarpT(TextureWrapMode.Repeat);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Aura3DModelBuilder] Failed to load texture: {ex.Message}");
            }
        }

        var material = new Material
        {
            BaseColor = sharedTexture ?? Texture.CreateFromColor(System.Drawing.Color.White),
            BlendMode = BlendMode.Masked,
            AlphaCutoff = 0.5f,
            DoubleSided = false,
        };

        var bonePivots = new Dictionary<string, Vector3>();
        foreach (var bone in geoModel.Bones)
            bonePivots[bone.Id] = bone.Pivot;

        foreach (var bone in geoModel.Bones)
        {
            var boneNode = new Node { Name = bone.Name };
            if (bone.Rotation != Vector3.Zero)
                boneNode.RotationQuaternion = CreateBlockbenchQuaternion(bone.Rotation);

            if (bone.ParentId is not null && bonePivots.TryGetValue(bone.ParentId, out var parentPivot))
            {
                var relativeOffset = bone.Pivot - parentPivot;
                boneNode.Position = relativeOffset * ExportScale;
            }
            else
            {
                boneNode.Position = bone.Pivot * ExportScale;
            }

            baseEulers[bone.Id] = bone.Rotation;
            boneNodes[bone.Id] = boneNode;
        }

        foreach (var bone in geoModel.Bones)
        {
            if (bone.ParentId is not null && boneNodes.TryGetValue(bone.ParentId, out var parentNode))
            {
                if (boneNodes.TryGetValue(bone.Id, out var childNode))
                    parentNode.AddChild(childNode, AttachToParentRule.KeepLocal);
            }
        }

        foreach (var bone in geoModel.Bones)
        {
            if (bone.ParentId is null && boneNodes.TryGetValue(bone.Id, out var rootNode))
                model.AddChild(rootNode, AttachToParentRule.KeepLocal);
        }

        // initial world matrix bake
        foreach (var bone in geoModel.Bones)
        {
            if (bone.ParentId is null && boneNodes.TryGetValue(bone.Id, out var rootNode))
            {
                using var u = rootNode.BeginTransformUpdate(UpdateTransformMode.ChildrenWorld);
            }
        }

        foreach (var bone in geoModel.Bones)
        {
            var bonePivot = bonePivots[bone.Id];
            int cubeIdx = 0;

            foreach (var cube in bone.Cubes)
            {
                var (from, to) = ConvertCubeBoundsDoc(cube);
                float inflate = cube.Inflate;

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

                var geom = BuildCubeGeometry(cube, geoModel.TextureWidth, geoModel.TextureHeight, lx, ly, lz, hx, hy, hz);

                var cubeMesh = new Mesh
                {
                    Name = $"cube_{bone.Name}_{cubeIdx}",
                    Geometry = geom,
                    Material = material,
                    Scale = Vector3.One,
                };

                var txLocal = cube.Pivot - bonePivot;
                cubeMesh.Position = new Vector3(txLocal.X * ExportScale, txLocal.Y * ExportScale, txLocal.Z * ExportScale);

                if (cube.Rotation != Vector3.Zero)
                    cubeMesh.RotationQuaternion = CreateBlockbenchQuaternion(cube.Rotation);

                if (boneNodes.TryGetValue(bone.Id, out var parentBoneNode))
                    parentBoneNode.AddChild(cubeMesh, AttachToParentRule.KeepLocal);
                else
                    model.AddChild(cubeMesh, AttachToParentRule.KeepLocal);

                cubeIdx++;
            }
        }

        // Re-bake parents under model after cubes added
        foreach (var bone in geoModel.Bones)
        {
            if (bone.ParentId is null && boneNodes.TryGetValue(bone.Id, out var rootNode))
            {
                using var u = rootNode.BeginTransformUpdate(UpdateTransformMode.ChildrenWorld);
            }
        }

        return new BuildResult(model, boneNodes, baseEulers);
    }

    private static (Vector3 From, Vector3 To) ConvertCubeBoundsDoc(YsmCubeInfo cube)
    {
        var from = new Vector3(cube.Origin.X - cube.Size.X, cube.Origin.Y, cube.Origin.Z);
        var to = new Vector3(from.X + cube.Size.X, from.Y + cube.Size.Y, from.Z + cube.Size.Z);
        return (from, to);
    }

    private static Geometry BuildCubeGeometry(
        YsmCubeInfo cube, float texW, float texH,
        float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
    {
        var positions = new List<float>();
        var normals = new List<float>();
        var uvs = new List<float>();
        var indices = new List<uint>();

        float tw = texW > 0 ? texW : 64f;
        float th = texH > 0 ? texH : 64f;

        var cubeUV = cube.Uv;
        if (cubeUV?.IsBoxUV == true && cube.Size != Vector3.Zero)
            cubeUV = cubeUV.Expand(cube.Size.X, cube.Size.Y, cube.Size.Z);

        AddFace(positions, normals, uvs, indices,
            maxX, maxY, maxZ, maxX, maxY, minZ, maxX, minY, maxZ, maxX, minY, minZ,
            1, 0, 0,
            GetFaceUV(cubeUV?.East, tw, th));

        AddFace(positions, normals, uvs, indices,
            minX, maxY, minZ, minX, maxY, maxZ, minX, minY, minZ, minX, minY, maxZ,
            -1, 0, 0,
            GetFaceUV(cubeUV?.West, tw, th));

        AddFace(positions, normals, uvs, indices,
            minX, maxY, minZ, maxX, maxY, minZ, minX, maxY, maxZ, maxX, maxY, maxZ,
            0, 1, 0,
            GetFaceUV(cubeUV?.Up, tw, th));

        AddFace(positions, normals, uvs, indices,
            minX, minY, maxZ, maxX, minY, maxZ, minX, minY, minZ, maxX, minY, minZ,
            0, -1, 0,
            GetFaceUV(cubeUV?.Down, tw, th));

        AddFace(positions, normals, uvs, indices,
            minX, maxY, maxZ, maxX, maxY, maxZ, minX, minY, maxZ, maxX, minY, maxZ,
            0, 0, 1,
            GetFaceUV(cubeUV?.South, tw, th));

        AddFace(positions, normals, uvs, indices,
            maxX, maxY, minZ, minX, maxY, minZ, maxX, minY, minZ, minX, minY, minZ,
            0, 0, -1,
            GetFaceUV(cubeUV?.North, tw, th));

        var geometry = new Geometry();
        geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, positions);
        geometry.SetVertexAttribute(BuildInVertexAttribute.Normal, 3, normals);
        geometry.SetVertexAttribute(BuildInVertexAttribute.TexCoord_0, 2, uvs);
        geometry.SetIndices(indices);

        return geometry;
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

    private static void AddFace(
        List<float> positions, List<float> normals, List<float> uvs, List<uint> indices,
        float x0, float y0, float z0, float x1, float y1, float z1,
        float x2, float y2, float z2, float x3, float y3, float z3,
        float nx, float ny, float nz,
        (float u0, float v0, float u1, float v1, float u2, float v2, float u3, float v3) faceUV)
    {
        uint baseIndex = (uint)(positions.Count / 3);

        positions.AddRange([x0, y0, z0]);
        positions.AddRange([x1, y1, z1]);
        positions.AddRange([x2, y2, z2]);
        positions.AddRange([x3, y3, z3]);

        for (int i = 0; i < 4; i++)
            normals.AddRange([nx, ny, nz]);

        uvs.AddRange([faceUV.u0, faceUV.v0]);
        uvs.AddRange([faceUV.u1, faceUV.v1]);
        uvs.AddRange([faceUV.u2, faceUV.v2]);
        uvs.AddRange([faceUV.u3, faceUV.v3]);

        indices.AddRange([baseIndex, baseIndex + 2, baseIndex + 1]);
        indices.AddRange([baseIndex + 2, baseIndex + 3, baseIndex + 1]);
    }

    internal static Quaternion CreateBlockbenchQuaternion(Vector3 eulerDegrees)
    {
        float rx = eulerDegrees.X * MathF.PI / 180f;
        float ry = eulerDegrees.Y * MathF.PI / 180f;
        float rz = eulerDegrees.Z * MathF.PI / 180f;
        var m = Matrix4x4.CreateRotationX(rx)
              * Matrix4x4.CreateRotationY(ry)
              * Matrix4x4.CreateRotationZ(rz);
        return Quaternion.CreateFromRotationMatrix(m);
    }
}
