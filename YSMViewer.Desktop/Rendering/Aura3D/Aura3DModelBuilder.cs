using Aura3D.Core;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using System.Numerics;
using System.Security.Cryptography;
using YSMViewer.Models;
using YSMViewer.Models.Document;

namespace YSMViewer.Desktop.Rendering.Aura3D;

public static class Aura3DModelBuilder
{
    private const float ExportScale = 1f / 16f;
    private const int MaxCachedTextures = 32;
    private static readonly Dictionary<string, Texture> _textureCache = [];
    private static readonly Lock _textureCacheLock = new();

    public static void ClearTextureCache()
    {
        lock (_textureCacheLock)
        {
            _textureCache.Clear();
        }
    }

    public sealed record BuildResult(
        Model RootModel,
        Dictionary<string, Node> BoneNodes,
        Dictionary<string, Vector3> BaseBoneEulers,
        int MergedMeshCount);

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
                string hash;
                var hashBytes = SHA256.HashData(texture.Data);
                hash = Convert.ToHexString(hashBytes);

                lock (_textureCacheLock)
                {
                    if (!_textureCache.TryGetValue(hash, out sharedTexture))
                    {
                        if (_textureCache.Count >= MaxCachedTextures)
                        {
                            _textureCache.Clear();
                        }

                        sharedTexture = TextureLoader.LoadTexture(texture.Data)
                            .SetMinFilter(TextureFilterMode.Nearest)
                            .SetMagFilter(TextureFilterMode.Nearest)
                            .SetWrapS(TextureWrapMode.Repeat)
                            .SetWrapT(TextureWrapMode.Repeat);
                        _textureCache[hash] = sharedTexture;
                    }
                }
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

        // One merged mesh per bone: every cube's rotation/pivot offset is baked into
        // vertices, so animating the bone node still moves all of its cubes while
        // draw calls drop from one-per-cube to one-per-bone.
        int meshCount = 0;
        foreach (var bone in geoModel.Bones)
        {
            if (bone.Cubes.Count == 0) continue;

            var bonePivot = bonePivots[bone.Id];
            int vertexCount = 0;
            foreach (var cube in bone.Cubes)
                vertexCount += 24;

            var positions = new List<float>(vertexCount * 3);
            var normals = new List<float>(vertexCount * 3);
            var uvs = new List<float>(vertexCount * 2);
            var indices = new List<uint>(bone.Cubes.Count * 36);

            foreach (var cube in bone.Cubes)
            {
                AppendCube(positions, normals, uvs, indices, cube, geoModel.TextureWidth, geoModel.TextureHeight, bonePivot);
            }

            var geometry = new Geometry();
            geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, positions);
            geometry.SetVertexAttribute(BuildInVertexAttribute.Normal, 3, normals);
            geometry.SetVertexAttribute(BuildInVertexAttribute.TexCoord_0, 2, uvs);
            geometry.SetIndices(indices);

            var mesh = new Mesh
            {
                Name = $"bone_{bone.Name}",
                Geometry = geometry,
                Material = material,
                Scale = Vector3.One,
            };

            if (boneNodes.TryGetValue(bone.Id, out var parentBoneNode))
                parentBoneNode.AddChild(mesh, AttachToParentRule.KeepLocal);
            else
                model.AddChild(mesh, AttachToParentRule.KeepLocal);

            meshCount++;
        }

        foreach (var bone in geoModel.Bones)
        {
            if (bone.ParentId is null && boneNodes.TryGetValue(bone.Id, out var rootNode))
            {
                using var u = rootNode.BeginTransformUpdate(UpdateTransformMode.ChildrenWorld);
            }
        }

        return new BuildResult(model, boneNodes, baseEulers, meshCount);
    }

    /// <summary>
    /// Appends the six faces of one cube into the bone-level vertex buffers, baking the
    /// cube's pivot offset (relative to the bone) and its local rotation into each vertex.
    /// </summary>
    private static void AppendCube(
        List<float> positions, List<float> normals, List<float> uvs, List<uint> indices,
        YsmCubeInfo cube, float texW, float texH, Vector3 bonePivot)
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

        var mn = min * ExportScale;
        var mx = max * ExportScale;
        var offset = (cube.Pivot - bonePivot) * ExportScale;
        var rotation = cube.Rotation != Vector3.Zero
            ? CreateBlockbenchQuaternion(cube.Rotation)
            : Quaternion.Identity;

        Vector3 Xform(Vector3 v) => Vector3.Transform(v, rotation) + offset;

        float tw = texW > 0 ? texW : 64f;
        float th = texH > 0 ? texH : 64f;

        var cubeUV = cube.Uv;
        if (cubeUV?.IsBoxUV == true && cube.Size != Vector3.Zero)
            cubeUV = cubeUV.Expand(cube.Size.X, cube.Size.Y, cube.Size.Z, cube.Mirror);

        AddFace(positions, normals, uvs, indices,
            Xform(new Vector3(mx.X, mx.Y, mx.Z)), Xform(new Vector3(mx.X, mx.Y, mn.Z)),
            Xform(new Vector3(mx.X, mn.Y, mx.Z)), Xform(new Vector3(mx.X, mn.Y, mn.Z)),
            Vector3.Transform(Vector3.UnitX, rotation),
            CubeFaceUvMapper.GetFaceUv(cubeUV?.East, tw, th));

        AddFace(positions, normals, uvs, indices,
            Xform(new Vector3(mn.X, mx.Y, mn.Z)), Xform(new Vector3(mn.X, mx.Y, mx.Z)),
            Xform(new Vector3(mn.X, mn.Y, mn.Z)), Xform(new Vector3(mn.X, mn.Y, mx.Z)),
            Vector3.Transform(-Vector3.UnitX, rotation),
            CubeFaceUvMapper.GetFaceUv(cubeUV?.West, tw, th));

        AddFace(positions, normals, uvs, indices,
            Xform(new Vector3(mn.X, mx.Y, mn.Z)), Xform(new Vector3(mx.X, mx.Y, mn.Z)),
            Xform(new Vector3(mn.X, mx.Y, mx.Z)), Xform(new Vector3(mx.X, mx.Y, mx.Z)),
            Vector3.Transform(Vector3.UnitY, rotation),
            CubeFaceUvMapper.GetFaceUv(cubeUV?.Up, tw, th));

        AddFace(positions, normals, uvs, indices,
            Xform(new Vector3(mn.X, mn.Y, mx.Z)), Xform(new Vector3(mx.X, mn.Y, mx.Z)),
            Xform(new Vector3(mn.X, mn.Y, mn.Z)), Xform(new Vector3(mx.X, mn.Y, mn.Z)),
            Vector3.Transform(-Vector3.UnitY, rotation),
            CubeFaceUvMapper.GetFaceUv(cubeUV?.Down, tw, th));

        AddFace(positions, normals, uvs, indices,
            Xform(new Vector3(mn.X, mx.Y, mx.Z)), Xform(new Vector3(mx.X, mx.Y, mx.Z)),
            Xform(new Vector3(mn.X, mn.Y, mx.Z)), Xform(new Vector3(mx.X, mn.Y, mx.Z)),
            Vector3.Transform(Vector3.UnitZ, rotation),
            CubeFaceUvMapper.GetFaceUv(cubeUV?.South, tw, th));

        AddFace(positions, normals, uvs, indices,
            Xform(new Vector3(mx.X, mx.Y, mn.Z)), Xform(new Vector3(mn.X, mx.Y, mn.Z)),
            Xform(new Vector3(mx.X, mn.Y, mn.Z)), Xform(new Vector3(mn.X, mn.Y, mn.Z)),
            Vector3.Transform(-Vector3.UnitZ, rotation),
            CubeFaceUvMapper.GetFaceUv(cubeUV?.North, tw, th));
    }

    private static (Vector3 From, Vector3 To) ConvertCubeBoundsDoc(YsmCubeInfo cube)
    {
        var from = new Vector3(cube.Origin.X - cube.Size.X, cube.Origin.Y, cube.Origin.Z);
        var to = new Vector3(from.X + cube.Size.X, from.Y + cube.Size.Y, from.Z + cube.Size.Z);
        return (from, to);
    }

    private static void AddFace(
        List<float> positions, List<float> normals, List<float> uvs, List<uint> indices,
        Vector3 c0, Vector3 c1, Vector3 c2, Vector3 c3, Vector3 normal,
        (float u0, float v0, float u1, float v1, float u2, float v2, float u3, float v3) faceUV)
    {
        uint baseIndex = (uint)(positions.Count / 3);

        positions.AddRange([c0.X, c0.Y, c0.Z]);
        positions.AddRange([c1.X, c1.Y, c1.Z]);
        positions.AddRange([c2.X, c2.Y, c2.Z]);
        positions.AddRange([c3.X, c3.Y, c3.Z]);

        normals.AddRange([normal.X, normal.Y, normal.Z]);
        normals.AddRange([normal.X, normal.Y, normal.Z]);
        normals.AddRange([normal.X, normal.Y, normal.Z]);
        normals.AddRange([normal.X, normal.Y, normal.Z]);

        uvs.AddRange([faceUV.u0, faceUV.v0]);
        uvs.AddRange([faceUV.u1, faceUV.v1]);
        uvs.AddRange([faceUV.u2, faceUV.v2]);
        uvs.AddRange([faceUV.u3, faceUV.v3]);

        indices.AddRange([baseIndex, baseIndex + 2, baseIndex + 1]);
        indices.AddRange([baseIndex + 2, baseIndex + 3, baseIndex + 1]);
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
}
