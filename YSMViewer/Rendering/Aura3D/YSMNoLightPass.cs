using global::Aura3D.Core;
using global::Aura3D.Core.Math;
using global::Aura3D.Core.Nodes;
using global::Aura3D.Core.Renderers;
using global::Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Drawing;
using System.Numerics;

namespace YSMViewer.Rendering.Aura3D;

public class YSMNoLightPass : NoLightPass
{
    private readonly global::Aura3D.Core.Resources.Texture _defaultBaseColor;
    private bool _defaultBaseColorUploaded;

    /// <summary>0 = off, >0 = simple shading intensity</summary>
    public float SimpleShadingIntensity { get; set; } = 0.3f;

    public YSMNoLightPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        _defaultBaseColor = global::Aura3D.Core.Resources.Texture.CreateFromColor(Color.White);

        FragmentShader = ShaderResource.NoLightFrag.Replace(
            "precision mediump float;",
            "precision mediump float;\n//{{defines}}");

        VertexShader = ShaderResource.NoLightVert
            .Replace("out vec2 vTexCoord;", "out vec2 vTexCoord;\nout vec3 vNormal;")
            .Replace(
                "\tgl_Position = projectionMatrix * viewMatrix * worldPosition;",
                "\tvNormal = normalize(mat3(normalMatrix) * normal);\n\tgl_Position = projectionMatrix * viewMatrix * worldPosition;");

        FragmentShader = FragmentShader
            .Replace("in vec2 vTexCoord;", "in vec2 vTexCoord;\nin vec3 vNormal;")
            .Replace(
                "uniform float alphaCutoff;",
                "uniform float alphaCutoff;\nuniform float simpleShadingIntensity;")
            .Replace(
                "\toutColor = baseColor;",
@"    vec3 lightDir = normalize(vec3(1.0, 1.0, 1.0));
    float diff = max(dot(normalize(vNormal), lightDir), 0.0);
    float shade = 0.5 + 0.5 * diff;
    baseColor.rgb = mix(baseColor.rgb, baseColor.rgb * shade, simpleShadingIntensity);
    outColor = baseColor;");
    }

    public override void Setup()
    {
        base.Setup();
        if (!_defaultBaseColorUploaded && gl != null)
        {
            _defaultBaseColor.Upload(gl);
            _defaultBaseColorUploaded = true;
        }
    }

    public override void Destroy()
    {
        if (_defaultBaseColorUploaded)
            _defaultBaseColor.Destroy(gl);
        base.Destroy();
    }

    private void SetupUniform(Material? material, Matrix4x4 view, Matrix4x4 projection)
    {
        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);

        UniformTexture("BaseColorTexture", material?.GetTexture("BaseColor") ?? _defaultBaseColor);

        if (material != null)
        {
            if (material.DoubleSided == false)
                gl.Enable(EnableCap.CullFace);
            else
                gl.Disable(EnableCap.CullFace);

            UniformFloat("alphaCutoff", material.AlphaCutoff);
        }
        else
        {
            gl.Enable(EnableCap.CullFace);
            UniformFloat("alphaCutoff", 0.0f);
        }
    }

    public override void Render(Camera camera)
    {
        UseShader();
        RenderVisibleMeshesInCamera(mesh => mesh.IsStaticMesh && IsMaterialBlendMode(mesh, BlendMode.Opaque), camera.View, camera.Projection);

        UseShader("BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => mesh.IsStaticMesh && IsMaterialBlendMode(mesh, BlendMode.Masked), camera.View, camera.Projection);

        UseShader("SKINNED_MESH");
        RenderVisibleMeshesInCamera(mesh => mesh.IsSkinnedMesh && IsMaterialBlendMode(mesh, BlendMode.Opaque), camera.View, camera.Projection);

        UseShader("SKINNED_MESH", "BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => mesh.IsSkinnedMesh && IsMaterialBlendMode(mesh, BlendMode.Masked), camera.View, camera.Projection);

        UseShader("INSTANCED_MESH");
        RenderVisibleInstancedMeshesInCamera(instancedMesh => IsMaterialBlendMode(instancedMesh.Material, BlendMode.Opaque), camera.View, camera.Projection);

        UseShader("INSTANCED_MESH", "BLENDMODE_MASKED");
        RenderVisibleInstancedMeshesInCamera(instancedMesh => IsMaterialBlendMode(instancedMesh.Material, BlendMode.Masked), camera.View, camera.Projection);
    }

    public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();
        SetupUniform(mesh.Material, view, projection);

        var nm = mesh.WorldTransform.Inverse();
        nm = Matrix4x4.Transpose(nm);
        UniformMatrix4("normalMatrix", nm);

        UniformFloat("simpleShadingIntensity", SimpleShadingIntensity);

        if (mesh.IsSkinnedMesh)
        {
            var skeleton = mesh.Skeleton;
            if (mesh.Model.AnimationSampler != null)
            {
                for (int i = 0; i < skeleton.Bones.Count; i++)
                    UniformMatrix4($"BoneMatrices[{i}]", skeleton.Bones[i].InverseWorldMatrix * mesh.Model.AnimationSampler.BonesTransform[i]);
            }
            else
            {
                for (int i = 0; i < skeleton.Bones.Count; i++)
                    UniformMatrix4($"BoneMatrices[{i}]", skeleton.Bones[i].InverseWorldMatrix * skeleton.Bones[i].WorldMatrix);
            }
        }
        base.RenderMesh(mesh, view, projection);
    }

    public override void RenderInstancedMesh(InstancedMesh instancedMesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();
        SetupUniform(instancedMesh.Material, view, projection);

        UniformFloat("simpleShadingIntensity", SimpleShadingIntensity);

        base.RenderInstancedMesh(instancedMesh, view, projection);
    }
}
