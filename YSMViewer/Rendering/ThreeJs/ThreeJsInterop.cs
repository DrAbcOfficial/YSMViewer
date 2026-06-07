using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace YSMViewer.Rendering.ThreeJs;

[SupportedOSPlatform("browser")]
public static partial class ThreeJsInterop
{
    [JSImport("globalThis.ysmInit")]
    public static partial void Init(string canvasId);

    [JSImport("globalThis.ysmSetViewportRect")]
    public static partial void SetViewportRect(int x, int y, int width, int height);

    [JSImport("globalThis.ysmShowCanvas")]
    public static partial void ShowCanvas();

    [JSImport("globalThis.ysmHideCanvas")]
    public static partial void HideCanvas();

    [JSImport("globalThis.ysmLoadModelGeometry")]
    public static partial void LoadModelGeometry(string specJson);

    [JSImport("globalThis.ysmAddTextureData")]
    public static partial void AddTextureData(string textureId, byte[] data);

    [JSImport("globalThis.ysmClearScene")]
    public static partial void ClearScene();

    [JSImport("globalThis.ysmSetCameraView")]
    public static partial void SetCameraView(string viewName);

    [JSImport("globalThis.ysmSetAutoRotate")]
    public static partial void SetAutoRotate(bool enabled);

    [JSImport("globalThis.ysmSetBackground")]
    public static partial void SetBackground(byte r, byte g, byte b);

    [JSImport("globalThis.ysmSetComponentVisible")]
    public static partial void SetComponentVisible(string componentId, bool visible);

    [JSImport("globalThis.ysmSetBoneVisible")]
    public static partial void SetBoneVisible(string boneId, bool visible);

    [JSImport("globalThis.ysmDispose")]
    public static partial void Dispose();
}
