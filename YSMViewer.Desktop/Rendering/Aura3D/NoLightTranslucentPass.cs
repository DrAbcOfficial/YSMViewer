using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace YSMViewer.Desktop.Rendering.Aura3D;

public sealed class NoLightTranslucentPass(RenderPipeline renderPipeline) : YSMNoLightPass(renderPipeline)
{
    public override void BeforeRender(Camera camera)
    {
        BindOutPutRenderTarget(camera);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
        gl.DepthMask(false);
    }

    public override void Render(Camera camera)
    {
        CurrentBoneCapacity = ComputeSkinnedBoneCapacity();

        UseShader("BLENDMODE_TRANSLUCENT");
        RenderVisibleMeshesInCamera(mesh => mesh.IsStaticMesh && IsMaterialBlendMode(mesh, BlendMode.Translucent), camera.View, camera.Projection);

        UseShader("SKINNED_MESH", "BLENDMODE_TRANSLUCENT", $"BONE_NUMBER {CurrentBoneCapacity}");
        RenderVisibleMeshesInCamera(mesh => mesh.IsSkinnedMesh && IsMaterialBlendMode(mesh, BlendMode.Translucent), camera.View, camera.Projection);

        UseShader("INSTANCED_MESH", "BLENDMODE_TRANSLUCENT");
        RenderVisibleInstancedMeshesInCamera(instancedMesh => IsMaterialBlendMode(instancedMesh.Material, BlendMode.Translucent), camera.View, camera.Projection);
    }

    public override void AfterRender(Camera camera)
    {
    }
}
