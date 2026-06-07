using Avalonia;
using Avalonia.Controls;
using YSMViewer.Models.Document;

namespace YSMViewer.Rendering.ThreeJs;

public sealed class ThreeJsRenderer : IRenderer, IInteractiveRenderer, IDisposable
{
    private readonly ThreeJsViewHost _viewHost;
    private bool _isInitialized;
    private YsmModelDocument? _currentDocument;

    private int _lastX, _lastY, _lastW, _lastH;

    public ThreeJsRenderer()
    {
        _viewHost = new ThreeJsViewHost(OnViewportChanged);
    }

    public Control View => _viewHost;

    public RendererCapabilities Capabilities { get; } = new(
        SupportsAnimation: false,
        SupportsComponentVisibility: true,
        SupportsBoneVisibility: true,
        SupportsTextureProjection: false,
        SupportsAutoRotation: false,
        SupportsFreeCamera: true,
        SupportsGizmo: false);

    public void LoadModel(YsmModelDocument document)
    {
        _currentDocument = document;

        if (!_isInitialized)
        {
            ThreeJsInterop.ShowCanvas();
            ThreeJsInterop.Init("three-canvas");
            _isInitialized = true;
        }
        else
        {
            ThreeJsInterop.ShowCanvas();
        }

        var specJson = ThreeJsPayloadBuilder.BuildSpecJson(document);
        ThreeJsInterop.LoadModelGeometry(specJson);

        var requiredTexIds = ThreeJsPayloadBuilder.GetRequiredTextureIds(document);
        foreach (var tex in document.Textures)
        {
            if (requiredTexIds.Contains(tex.Id) && tex.Data is { Length: > 0 })
                ThreeJsInterop.AddTextureData(tex.Id, tex.Data);
        }
    }

    public void Clear()
    {
        _currentDocument = null;
        if (_isInitialized)
        {
            ThreeJsInterop.ClearScene();
            ThreeJsInterop.HideCanvas();
        }
    }

    public void SetCameraView(RenderCameraView view)
    {
        if (!_isInitialized) return;
        var viewName = view switch
        {
            RenderCameraView.Front => "front",
            RenderCameraView.Side => "side",
            RenderCameraView.Top => "top",
            _ => "front",
        };
        ThreeJsInterop.SetCameraView(viewName);
    }

    public void SetTheme(RenderTheme theme)
    {
        if (_isInitialized)
            ThreeJsInterop.SetBackground(theme.BgR, theme.BgG, theme.BgB);
    }

    public void SetComponentVisible(string componentId, bool visible)
    {
        if (_isInitialized)
            ThreeJsInterop.SetComponentVisible(componentId, visible);
    }

    public void SetBoneVisible(string boneId, bool visible)
    {
        if (_isInitialized)
            ThreeJsInterop.SetBoneVisible(boneId, visible);
    }

    public void Dispose()
    {
        if (_isInitialized)
            ThreeJsInterop.Dispose();
        _isInitialized = false;
    }

    private void OnViewportChanged(int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0) return;
        if (x == _lastX && y == _lastY && w == _lastW && h == _lastH) return;
        _lastX = x;
        _lastY = y;
        _lastW = w;
        _lastH = h;

        if (_isInitialized)
            ThreeJsInterop.SetViewportRect(x, y, w, h);
    }
}

internal sealed class ThreeJsViewHost : Panel
{
    private readonly Action<int, int, int, int> _onBoundsChanged;

    public ThreeJsViewHost(Action<int, int, int, int> onBoundsChanged)
    {
        _onBoundsChanged = onBoundsChanged;
        Background = null;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not null)
        {
            var pos = this.TranslatePoint(new Point(0, 0), topLevel);
            if (pos.HasValue)
            {
                _onBoundsChanged(
                    (int)pos.Value.X,
                    (int)pos.Value.Y,
                    (int)finalSize.Width,
                    (int)finalSize.Height);
            }
        }

        return result;
    }
}
