using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Scenes;

namespace YSMViewer.Desktop.Rendering.Aura3D;

public sealed class YSMPipeline : RenderPipeline
{
    private readonly YSMNoLightPass _noLightPass;

    /// <summary>0 = off, >0 = simple shading intensity.</summary>
    public float SimpleShadingIntensity
    {
        get => _noLightPass.SimpleShadingIntensity;
        set => _noLightPass.SimpleShadingIntensity = value;
    }

    public YSMPipeline(Scene scene) : base(scene)
    {
        RegisterRenderPass(new BackgroundPass(this).SetOutPutRenderTarget("BaseRenderTarget"), RenderPassGroup.EveryCamera);
        _noLightPass = new YSMNoLightPass(this);
        RegisterRenderPass(_noLightPass.SetOutPutRenderTarget("BaseRenderTarget"), RenderPassGroup.EveryCamera);
        RegisterRenderPass(new NoLightTranslucentPass(this).SetOutPutRenderTarget("BaseRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new GammaCorrectionPass(this, "BaseRenderTarget", "Color").SetOutPutRenderTarget("GammaOutput"), RenderPassGroup.EveryCamera);
        RegisterRenderPass(new FxaaPass(this, "GammaOutput", "Color"), RenderPassGroup.EveryCamera);
        RegisterRenderPass(new DebugDrawPass(this, "BaseRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderTarget("BaseRenderTarget")
            .AddTexture("Color", TextureFormat.Rgba8)
            .SetDepthTexture(Settings.DepthFormat);

        RegisterRenderTarget("GammaOutput")
            .AddTexture("Color", TextureFormat.Rgba8)
            .SetDepthTexture(Settings.DepthFormat);
    }

    public override void BeforeCameraRender(Camera camera)
    {
        if (gl == null) return;
        SortMeshes(VisibleMeshesInCamera, camera);
        gl.Viewport(0, 0, camera.RenderTarget.Width, camera.RenderTarget.Height);
    }
}
