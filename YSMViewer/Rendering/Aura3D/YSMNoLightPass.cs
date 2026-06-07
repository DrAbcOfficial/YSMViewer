using global::Aura3D.Core;
using global::Aura3D.Core.Nodes;
using global::Aura3D.Core.Renderers;
using global::Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace YSMViewer.Rendering.Aura3D;

public class YSMNoLightPass : NoLightPass
{
    public YSMNoLightPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        FragmentShader = ShaderResource.NoLightFrag.Replace(
            "precision mediump float;",
            "precision mediump float;\n//{{defines}}");
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
}
