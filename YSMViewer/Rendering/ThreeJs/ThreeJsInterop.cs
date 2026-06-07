using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace YSMViewer.Rendering.ThreeJs;

[SupportedOSPlatform("browser")]
public static partial class ThreeJsInterop
{
    private const string ModuleName = "YsmThreeRenderer";

    public static Task InitializeAsync() =>
        JSHost.ImportAsync(ModuleName, "../js/ysm-three-renderer.js");

    [JSImport("init", ModuleName)]
    public static partial void Init(string canvasId);

    [JSImport("setViewportRect", ModuleName)]
    public static partial void SetViewportRect(int x, int y, int width, int height);

    [JSImport("showCanvas", ModuleName)]
    public static partial void ShowCanvas();

    [JSImport("hideCanvas", ModuleName)]
    public static partial void HideCanvas();

    [JSImport("loadModelGeometry", ModuleName)]
    public static partial void LoadModelGeometry(string specJson);

    [JSImport("addTextureData", ModuleName)]
    public static partial void AddTextureData(string textureId, byte[] data);

    [JSImport("clearScene", ModuleName)]
    public static partial void ClearScene();

    [JSImport("setCameraView", ModuleName)]
    public static partial void SetCameraView(string viewName);

    [JSImport("setAutoRotate", ModuleName)]
    public static partial void SetAutoRotate(bool enabled);

    [JSImport("setBackground", ModuleName)]
    public static partial void SetBackground(byte r, byte g, byte b);

    [JSImport("setComponentVisible", ModuleName)]
    public static partial void SetComponentVisible(string componentId, bool visible);

    [JSImport("setBoneVisible", ModuleName)]
    public static partial void SetBoneVisible(string boneId, bool visible);

    [JSImport("dispose", ModuleName)]
    public static partial void Dispose();
}
