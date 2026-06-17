using Avalonia;
using Avalonia.Controls;
using YSMViewer.Models.Document;
using YSMViewer.Rendering;
using YSMViewer.Rendering.ThreeJs;
using YSMViewer.Services.Molang;

namespace YSMViewer.Browser.Rendering.ThreeJs;

public sealed class ThreeJsRenderer : IRenderer, IInteractiveRenderer, IAnimationRenderer, IDisposable
{
    private readonly ThreeJsViewHost _viewHost;
    private bool _isInitialized;
    private YsmModelDocument? _currentDocument;
    private readonly List<string> _animationNames = [];
    private float _animationDuration;
    private float _animationCurrentTime;

    private int _lastX, _lastY, _lastW, _lastH;

    public ThreeJsRenderer()
    {
        _viewHost = new ThreeJsViewHost(OnViewportChanged);
    }

    public Control View => _viewHost;
    public Control? GizmoControl => null;

    public RendererCapabilities Capabilities => RendererCapabilities.Browser;

    public IReadOnlyList<string> AnimationNames => _animationNames;
    public float AnimationDuration => _animationDuration;
    public bool HasAnimationController => false;
    public bool UseAnimationController { get; set; }
    public MolangService? MolangService => null;
    public float AnimationCurrentTime
    {
        get
        {
            if (!_isInitialized) return _animationCurrentTime;
            try
            {
                var json = ThreeJsInterop.GetAnimationProgress();
                if (!string.IsNullOrEmpty(json))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("time", out var timeEl))
                        _animationCurrentTime = (float)timeEl.GetDouble();
                }
            }
            catch { }
            return _animationCurrentTime;
        }
    }

    public void LoadModel(YsmModelDocument document)
    {
        _currentDocument = document;
        _animationNames.Clear();

        bool wasUninitialized = !_isInitialized;
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

        // Push the viewport rect now that the canvas is initialized and visible.
        // During the initial layout pass _isInitialized was false, so the cached
        // bounds were stored but SetViewportRect was never called.  Apply them
        // here so the canvas is correctly sized and positioned without waiting
        // for another layout pass (which may not fire if bounds haven't changed).
        if (wasUninitialized && _lastW > 0 && _lastH > 0)
            ThreeJsInterop.SetViewportRect(_lastX, _lastY, _lastW, _lastH);

        var specJson = ThreeJsPayloadBuilder.BuildSpecJson(document);
        ThreeJsInterop.LoadModelGeometry(specJson);

        var requiredTexIds = ThreeJsPayloadBuilder.GetRequiredTextureIds(document);
        foreach (var tex in document.Textures)
        {
            if (requiredTexIds.Contains(tex.Id) && tex.Data is { Length: > 0 })
                ThreeJsInterop.AddTextureData(tex.Id, tex.Data);
        }

        if (Capabilities.SupportsAnimation)
        {
            foreach (var anim in document.Animations)
            {
                var json = System.Text.Encoding.UTF8.GetString(anim.Data ?? []);
                if (!string.IsNullOrEmpty(json))
                {
                    ThreeJsInterop.LoadAnimationData(json);
                    var animFile = System.Text.Json.JsonSerializer.Deserialize(json,
                        Models.YsmJsonContext.Default.MinecraftAnimationFile);
                    if (animFile?.Animations is not null)
                    {
                        foreach (var (name, a) in animFile.Animations)
                        {
                            if (!_animationNames.Contains(name))
                                _animationNames.Add(name);
                            if (a.AnimationLength > _animationDuration)
                                _animationDuration = a.AnimationLength;
                        }
                    }
                }
            }
        }
    }

    public void Clear()
    {
        _currentDocument = null;
        _animationNames.Clear();
        _animationDuration = 0f;
        _animationCurrentTime = 0f;
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

    public void OrbitCamera(float deltaYaw, float deltaPitch) { }
    public void PanCamera(float deltaX, float deltaY) { }
    public void ZoomCamera(float delta) { }
    public void ResetCamera()
    {
        if (_isInitialized)
            ThreeJsInterop.ResetCamera();
    }
    public (float Pitch, float Yaw) GetCameraOrbit() => (0f, 0f);
    public void SyncGizmo() { }

    public void PlayAnimation(string name)
    {
        if (_isInitialized)
        {
            ThreeJsInterop.PlayAnimation(name);
            _animationCurrentTime = 0f;
        }
    }

    public void StopAnimation()
    {
        if (_isInitialized)
        {
            ThreeJsInterop.StopAnimation();
            _animationCurrentTime = 0f;
        }
    }

    public void Update(float deltaTime)
    {
        if (_isInitialized && _animationDuration > 0f)
        {
            _animationCurrentTime += deltaTime;
            if (_animationCurrentTime >= _animationDuration)
                _animationCurrentTime = 0f;
        }
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
