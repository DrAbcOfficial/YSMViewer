using global::Aura3D.Core.Renderers;
using global::Aura3D.Core.Nodes;
using global::Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace YSMViewer.Rendering.Aura3D;

public sealed class NoLightTranslucentPass : YSMNoLightPass
{
    public NoLightTranslucentPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
    }

    public override void BeforeRender(Camera camera)
    {
        BindOutPutRenderTarget(camera);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
        gl.DepthMask(false);
    }

    public override void Render(Camera camera)
    {
        UseShader("BLENDMODE_TRANSLUCENT");
        RenderVisibleMeshesInCamera(mesh => mesh.IsStaticMesh && IsMaterialBlendMode(mesh, BlendMode.Translucent), camera.View, camera.Projection);

        UseShader("SKINNED_MESH", "BLENDMODE_TRANSLUCENT");
        RenderVisibleMeshesInCamera(mesh => mesh.IsSkinnedMesh && IsMaterialBlendMode(mesh, BlendMode.Translucent), camera.View, camera.Projection);

        UseShader("INSTANCED_MESH", "BLENDMODE_TRANSLUCENT");
        RenderVisibleInstancedMeshesInCamera(instancedMesh => IsMaterialBlendMode(instancedMesh.Material, BlendMode.Translucent), camera.View, camera.Projection);
    }

    public override void AfterRender(Camera camera)
    {
    }
}
