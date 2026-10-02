using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Drawing;
using System.Numerics;

namespace YSMViewer.Desktop.Rendering.Aura3D;

/// <summary>
/// No-light pass with Minecraft-style simple shading. YSM geometry is always
/// node-animated static meshes (no Skeleton, no InstancedMesh), so only the
/// static Opaque/Masked/Translucent variants are driven; skinned and instanced
/// branches of the base pass are dead weight here and are not issued.
/// </summary>
public class YSMNoLightPass : NoLightPass
{
    private readonly global::Aura3D.Core.Resources.Texture _defaultBaseColor;

    /// <summary>0 = off, >0 = simple shading intensity</summary>
    public float SimpleShadingIntensity
    {
        get => _simpleShadingIntensity;
        set
        {
            if (_simpleShadingIntensity == value) return;
            _simpleShadingIntensity = value;
            _perProgramUniformsShader = null;
        }
    }
    private float _simpleShadingIntensity = 0.5f;

    public YSMNoLightPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        _defaultBaseColor = global::Aura3D.Core.Resources.Texture.CreateFromColor(Color.White);

        VertexShader = _VertexShader;
        FragmentShader = _FragmentShader;
    }

    private const string _VertexShader = @"#version 300 es
precision mediump float;

//{{defines}}

layout(location = 0) in vec3 position;
layout(location = 1) in vec2 texCoord;
layout(location = 3) in vec3 normal;

uniform mat4 modelMatrix;
uniform mat4 normalMatrix;
uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;

out vec2 vTexCoord;
out vec3 vNormal;

void main()
{
	vTexCoord = texCoord;
	vNormal = normalize(mat3(normalMatrix) * normal);
	gl_Position = projectionMatrix * viewMatrix * modelMatrix * vec4(position, 1.0);
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

	vec3 lightDir = normalize(vec3(-1.0, 1.0, -1.0));
	float diff = max(dot(normalize(vNormal), lightDir), 0.0);
	float shade = 0.2 + 0.8 * diff;
	baseColor.rgb = mix(baseColor.rgb, baseColor.rgb * shade, simpleShadingIntensity);
	outColor = baseColor;
}
";

    public override void Setup()
    {
        base.Setup();
        renderPipeline.EnsureSynced(_defaultBaseColor);
    }

    private global::Aura3D.Core.Renderers.Shader? _perProgramUniformsShader;
    private Matrix4x4 _perProgramUniformsView;
    private Matrix4x4 _perProgramUniformsProjection;

    /// <summary>
    /// viewMatrix/projectionMatrix/simpleShadingIntensity only change per camera or per
    /// program, not per mesh; the engine binds the program before RenderMesh is called,
    /// so uniforms persist in the program object until the next change.
    /// </summary>
    private void EnsurePerProgramUniforms(Matrix4x4 view, Matrix4x4 projection)
    {
        if (ReferenceEquals(_perProgramUniformsShader, CurrentShader)
            && _perProgramUniformsView == view
            && _perProgramUniformsProjection == projection)
            return;

        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);
        UniformFloat("simpleShadingIntensity", SimpleShadingIntensity);
        _perProgramUniformsShader = CurrentShader;
        _perProgramUniformsView = view;
        _perProgramUniformsProjection = projection;
    }

    private Material? _lastUniformMaterial;

    private void SetupUniform(Material? material)
    {
        if (ReferenceEquals(material, _lastUniformMaterial))
            return;

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

        _lastUniformMaterial = material;
    }

    public override void Render(Camera camera)
    {
        UseShader();
        RenderVisibleMeshesInCamera(mesh => mesh.IsStaticMesh && IsMaterialBlendMode(mesh, BlendMode.Opaque), camera.View, camera.Projection);

        UseShader("BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => mesh.IsStaticMesh && IsMaterialBlendMode(mesh, BlendMode.Masked), camera.View, camera.Projection);
    }

    public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        EnsurePerProgramUniforms(view, projection);
        ClearTextureUnit();
        SetupUniform(mesh.Material);

        var nm = mesh.WorldTransform.Inverse();
        nm = Matrix4x4.Transpose(nm);
        UniformMatrix4("normalMatrix", nm);

        base.RenderMesh(mesh, view, projection);
    }
}
