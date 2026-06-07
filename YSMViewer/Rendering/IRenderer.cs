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
}

public interface IAnimationRenderer : IRenderer
{
    IReadOnlyList<string> AnimationNames { get; }
    void PlayAnimation(string name);
    void StopAnimation();
    void Update(float deltaTime);
}
