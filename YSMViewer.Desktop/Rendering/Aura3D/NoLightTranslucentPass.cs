using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace YSMViewer.Desktop.Rendering.Aura3D;

public sealed class NoLightTranslucentPass(RenderPipeline renderPipeline) : YSMNoLightPass(renderPipeline)
{
    public override void BeforeRender(Camera camera)
    {
        BindOutputRenderTarget(camera);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
        gl.DepthMask(false);
    }

    public override void Render(Camera camera)
    {
        UseShader("BLENDMODE_TRANSLUCENT");
        RenderVisibleMeshesInCamera(mesh => mesh.IsStaticMesh && IsMaterialBlendMode(mesh, BlendMode.Translucent), camera.View, camera.Projection);
    }

    public override void AfterRender(Camera camera)
    {
    }
}
