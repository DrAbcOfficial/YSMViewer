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
        var baseRenderTarget = RegisterRenderTarget("BaseRenderTarget")
            .AddTexture("Color", TextureFormat.Rgba8)
            .SetDepthTexture(Settings.DepthFormat);

        var gammaOutput = RegisterRenderTarget("GammaOutput")
            .AddTexture("Color", TextureFormat.Rgba8)
            .SetDepthTexture(Settings.DepthFormat);

        RegisterRenderPass(new BackgroundPass(this).SetOutput(baseRenderTarget), RenderPassGroup.EveryCamera);
        _noLightPass = new YSMNoLightPass(this);
        RegisterRenderPass(_noLightPass.SetOutput(baseRenderTarget), RenderPassGroup.EveryCamera);
        RegisterRenderPass(new NoLightTranslucentPass(this).SetOutput(baseRenderTarget), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new GammaCorrectionPass(this, baseRenderTarget.GetTexture("Color")).SetOutput(gammaOutput), RenderPassGroup.EveryCamera);
        RegisterRenderPass(new FxaaPass(this, gammaOutput.GetTexture("Color")).SetOutput(CameraOutput), RenderPassGroup.EveryCamera);

        RegisterDebugPass(baseRenderTarget);
    }

    public override void BeforeCameraRender(Camera camera)
    {
        if (gl == null) return;
        SortMeshes(VisibleMeshesInCamera, camera);
        gl.Viewport(0, 0, camera.Width, camera.Height);
    }
}
