using Avalonia.Controls;
using YSMViewer.Models.Document;

namespace YSMViewer.Rendering;

public interface IRenderer
{
    Control View { get; }
    RendererCapabilities Capabilities { get; }
    void LoadModel(YsmModelDocument document);
    void Clear();
    void SetCameraView(RenderCameraView view);
    void SetTheme(RenderTheme theme);
}

public interface IInteractiveRenderer : IRenderer
{
    void SetComponentVisible(string componentId, bool visible);
    void SetBoneVisible(string boneId, bool visible);
    void OrbitCamera(float deltaYaw, float deltaPitch);
    void PanCamera(float deltaX, float deltaY);
    void ZoomCamera(float delta);
    void ResetCamera();
    (float Pitch, float Yaw) GetCameraOrbit();
}

public interface IAnimationRenderer : IRenderer
{
    IReadOnlyList<string> AnimationNames { get; }
    float AnimationDuration { get; }
    float AnimationCurrentTime { get; }
    void PlayAnimation(string name);
    void StopAnimation();
    void Update(float deltaTime);
}
