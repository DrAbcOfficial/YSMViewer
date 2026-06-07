namespace YSMViewer.Rendering;

public enum RenderCameraView
{
    Front,
    Side,
    Top,
}

public sealed record RenderTheme(
    byte BgR, byte BgG, byte BgB, byte BgA,
    bool IsDark);

public sealed record RendererCapabilities(
    bool SupportsAnimation,
    bool SupportsComponentVisibility,
    bool SupportsBoneVisibility,
    bool SupportsTextureProjection,
    bool SupportsAutoRotation,
    bool SupportsFreeCamera,
    bool SupportsGizmo)
{
    public static RendererCapabilities Desktop { get; } = new(
        SupportsAnimation: true,
        SupportsComponentVisibility: true,
        SupportsBoneVisibility: true,
        SupportsTextureProjection: false,
        SupportsAutoRotation: false,
        SupportsFreeCamera: true,
        SupportsGizmo: true);

    public static RendererCapabilities Browser { get; } = new(
        SupportsAnimation: false,
        SupportsComponentVisibility: false,
        SupportsBoneVisibility: false,
        SupportsTextureProjection: true,
        SupportsAutoRotation: true,
        SupportsFreeCamera: false,
        SupportsGizmo: false);
}
