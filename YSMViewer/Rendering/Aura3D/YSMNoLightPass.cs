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
    public float SimpleShadingIntensity { get; set; } = 0.5f;

    public YSMNoLightPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        _defaultBaseColor = global::Aura3D.Core.Resources.Texture.CreateFromColor(Color.White);

        FragmentShader = ShaderResource.NoLightFrag.Replace(
            "precision mediump float;",
            "precision mediump float;\n//{{defines}}");

        VertexShader = _VertexShader;
        FragmentShader = _FragmentShader;
    }

private const string _VertexShader = @"#version 300 es
precision mediump float;

#define BONE_NUMBER 150

//{{defines}}

layout(location = 0) in vec3 position;
layout(location = 1) in vec2 texCoord;
layout(location = 2) in vec4 color;
layout(location = 3) in vec3 normal;
layout(location = 4) in vec3 tangent;
layout(location = 5) in vec3 bitangent;
layout(location = 6) in vec4 boneIndices;
layout(location = 7) in vec4 boneWeights;

#ifdef INSTANCED_MESH
layout(location = 8) in mat4 modelMatrix;
layout(location = 12) in mat4 normalMatrix;
#endif

#ifdef SKINNED_MESH
uniform mat4 BoneMatrices[BONE_NUMBER];
#endif

#ifndef INSTANCED_MESH
uniform mat4 modelMatrix;
uniform mat4 normalMatrix;
#endif

uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;

out vec2 vTexCoord;
out vec3 vNormal;

void main()
{
	vTexCoord = texCoord;

#ifdef SKINNED_MESH
	
	int idx0 = clamp(int(boneIndices.x), 0, BONE_NUMBER - 1);
    int idx1 = clamp(int(boneIndices.y), 0, BONE_NUMBER - 1);
    int idx2 = clamp(int(boneIndices.z), 0, BONE_NUMBER - 1);
    int idx3 = clamp(int(boneIndices.w), 0, BONE_NUMBER - 1);

	float sum = boneWeights.x + boneWeights.y + boneWeights.z + boneWeights.w;
    vec4 w = (sum > 0.0001) ? boneWeights / sum : vec4(1.0, 0.0, 0.0, 0.0);

	mat4 skinMatrix = w.x * BoneMatrices[idx0];
    skinMatrix      += w.y * BoneMatrices[idx1];
    skinMatrix      += w.z * BoneMatrices[idx2];
    skinMatrix      += w.w * BoneMatrices[idx3];

	vec4 worldPosition = modelMatrix * skinMatrix * vec4(position, 1.0);

#else
	vec4 worldPosition = modelMatrix * vec4(position, 1.0);
#endif

	vNormal = normalize(mat3(normalMatrix) * normal);
	gl_Position = projectionMatrix * viewMatrix * worldPosition;
}
";

    private const string _FragmentShader = @"#version 300 es
precision mediump float;
//{{defines}}
out vec4 outColor;

in vec2 vTexCoord;
in vec3 vNormal;

uniform sampler2D BaseColorTexture;
uniform float alphaCutoff;
uniform float simpleShadingIntensity;

void main()
{
	vec4 baseColor = texture(BaseColorTexture, vTexCoord);

#if defined(BLENDMODE_MASKED) || defined(BLENDMODE_TRANSLUCENT)
	if (baseColor.a <= alphaCutoff)
		discard;
#endif

	vec3 lightDir = normalize(vec3(1.0, 1.0, 1.0));
	float diff = max(dot(normalize(vNormal), lightDir), 0.0);
	float shade = 0.2 + 0.8 * diff;
	baseColor.rgb = mix(baseColor.rgb, baseColor.rgb * shade, simpleShadingIntensity);
	outColor = baseColor;
}
";

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
