using Aura3D.Avalonia;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Avalonia.Controls;
using System.Numerics;
using YSMViewer.Models.Document;
using YSMViewer.Services;

namespace YSMViewer.Rendering.Aura3D;

public sealed class Aura3DRenderer : IAnimationRenderer, IInteractiveRenderer
{
    private readonly Aura3DView _view;
    private Model? _loadedModel;
    private YsmModelDocument? _document;
    private readonly Dictionary<string, Model> _componentModels = [];
    private readonly Dictionary<string, Node> _boneNodes = [];
    private readonly Dictionary<string, Vector3> _baseBoneEulers = [];
    private readonly List<Model> _sceneRoots = [];
    private readonly AnimationService _animService = new();
    private Dictionary<string, IAnimatableBone>? _animBones;
    private bool _sceneInitialized;

    public Vector3 CameraOrbitTarget => _cameraOrbitTarget;
    public float CameraYaw => _cameraYaw;
    public float CameraPitch => _cameraPitch;
    public float CameraDistance => _cameraDistance;

    public (float Pitch, float Yaw) GetCameraOrbit() => (_cameraPitch, _cameraYaw);

    private Vector3 _cameraOrbitTarget = Vector3.Zero;
    private float _cameraDistance = 30f;
    private float _cameraYaw = 180f;
    private float _cameraPitch = -15f;

    public Aura3DRenderer()
    {
        _view = new Aura3DView
        {
            CreateRenderPipeline = scene => new YSMPipeline(scene)
        };
        _view.SceneInitialized += OnSceneInitialized;
        _view.SceneUpdated += OnSceneUpdated;
    }

    public Control View => _view;
    public RendererCapabilities Capabilities => RendererCapabilities.Desktop;
    public IReadOnlyList<string> AnimationNames => _animService.AnimationNames;
    public float AnimationDuration => _animService.AnimationLength;
    public float AnimationCurrentTime => _animService.CurrentTime;

    public void SetTheme(RenderTheme theme)
    {
        var color = System.Drawing.Color.FromArgb(theme.BgA, theme.BgR, theme.BgG, theme.BgB);
        _view.Scene?.Background = Texture.CreateFromColor(color);
    }

    public void LoadModel(YsmModelDocument document)
    {
        Clear();
        _document = document;

        foreach (var geoModel in document.Models)
        {
            YsmTextureResource? tex = null;
            if (geoModel.TextureId is not null)
                tex = document.Textures.FirstOrDefault(t => t.Id == geoModel.TextureId)
                      ?? document.Textures.FirstOrDefault();

            var result = Aura3DModelBuilder.BuildFromDocument(geoModel, tex);
            result.RootModel.Enable = geoModel.DefaultVisible;
            _componentModels[geoModel.Id] = result.RootModel;

            foreach (var kv in result.BoneNodes)
                _boneNodes[kv.Key] = kv.Value;

            foreach (var kv in result.BaseBoneEulers)
                _baseBoneEulers[kv.Key] = kv.Value;

            if (_sceneInitialized && _view.Scene is not null)
            {
                AddModelToScene(result.RootModel);
            }
            else
            {
                _loadedModel ??= new Model { Name = "ysm_root" };
                _loadedModel.AddChild(result.RootModel, AttachToParentRule.KeepLocal);
            }
        }

        if (_sceneInitialized && _view.Scene is not null && document.Models.Count > 0)
            FitCameraToContent();

        _animBones = [];
        foreach (var kv in _boneNodes)
            _animBones[kv.Key] = new Aura3DBoneNode(kv.Value);
        _animService.SetBoneNodes(_animBones, _baseBoneEulers);

        foreach (var anim in document.Animations)
            _animService.LoadAnimations(anim.Data);
    }

    public void Clear()
    {
        _animService.IsPlaying = false;
        _componentModels.Clear();
        _boneNodes.Clear();
        _baseBoneEulers.Clear();

        if (_view.Scene is not null)
        {
            foreach (var root in _sceneRoots)
                _view.Scene.RemoveNode(root);

            if (_loadedModel is not null)
                _view.Scene.RemoveNode(_loadedModel);
        }

        _loadedModel = null;
        _sceneRoots.Clear();
        _animBones = null;
        _document = null;
    }

    public void SetCameraView(RenderCameraView view)
    {
        if (_view.Scene is null) return;

        switch (view)
        {
            case RenderCameraView.Front:
                _cameraYaw = 180f; _cameraPitch = 0f;
                break;
            case RenderCameraView.Side:
                _cameraYaw = -90f; _cameraPitch = 0f;
                break;
            case RenderCameraView.Top:
                _cameraYaw = 180f; _cameraPitch = -89f;
                break;
        }
        UpdateCameraPosition();
    }

    public void SetComponentVisible(string componentId, bool visible)
    {
        if (_componentModels.TryGetValue(componentId, out var model))
            model.Enable = visible;
    }

    public void SetBoneVisible(string boneId, bool visible)
    {
        if (_boneNodes.TryGetValue(boneId, out var node))
            node.Enable = visible;
    }

    public void PlayAnimation(string name)
    {
        _animService.PlayAnimation(name);
        _animService.IsPlaying = true;
    }

    public void StopAnimation()
    {
        _animService.IsPlaying = false;
        _animService.ResetBones();
    }

    public void Update(float deltaTime)
    {
        _animService.Update(deltaTime);
    }

    public void OrbitCamera(float deltaYaw, float deltaPitch)
    {
        _cameraYaw -= deltaYaw;
        _cameraPitch += deltaPitch;
        _cameraPitch = Math.Clamp(_cameraPitch, -89f, 89f);
        UpdateCameraPosition();
    }

    public void ZoomCamera(float delta)
    {
        _cameraDistance *= 1f - delta * 0.1f;
        _cameraDistance = MathF.Max(_cameraDistance, 0.5f);
        UpdateCameraPosition();
    }

    public void ResetCamera()
    {
        _cameraOrbitTarget = Vector3.Zero;
        _cameraDistance = 30f;
        _cameraYaw = 180f;
        _cameraPitch = -15f;
        UpdateCameraPosition();
    }

    public void PanCamera(float deltaX, float deltaY)
    {
        float pitchRad = _cameraPitch * MathF.PI / 180f;
        float yawRad = _cameraYaw * MathF.PI / 180f;

        var forward = new Vector3(
            MathF.Cos(pitchRad) * MathF.Sin(yawRad),
            MathF.Sin(pitchRad),
            MathF.Cos(pitchRad) * MathF.Cos(yawRad));
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var up = Vector3.Normalize(Vector3.Cross(right, forward));

        float speed = _cameraDistance * 0.002f;
        _cameraOrbitTarget += right * (deltaX * speed) + up * (deltaY * speed);
        UpdateCameraPosition();
    }

    private void OnSceneInitialized(object? sender, InitializedRoutedEventArgs args)
    {
        _sceneInitialized = true;
        var scene = args.Scene!;

        try
        {

            scene.RenderPipeline.EnableFrustumCulling = true;

            var camera = _view.MainCamera;
            camera.FieldOfView = 50f;
            camera.NearPlane = 0.1f;
            camera.FarPlane = 5000f;
            UpdateCameraPosition();

            if (_loadedModel is not null && _document is not null)
            {
                foreach (var geoModel in _document.Models)
                {
                    if (_componentModels.TryGetValue(geoModel.Id, out var compModel))
                        _view.AddNode(compModel);
                }

                FitCameraToContent();
                _loadedModel = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Aura3DRenderer] Scene init error: {ex.Message}");
        }
    }

    private void OnSceneUpdated(object? sender, UpdateRoutedEventArgs e)
    {
        Update((float)e.DeltaTime);
    }

    private void AddModelToScene(Model model)
    {
        if (_view.Scene is null) return;

        _view.AddNode(model);
        _sceneRoots.Add(model);
    }

    private void FitCameraToContent()
    {
        if (_view.Scene is null || _componentModels.Count == 0) return;

        float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

        foreach (var model in _componentModels.Values)
        {
            var bb = model.BoundingBox;
            if (bb.Min.X < minX) minX = bb.Min.X;
            if (bb.Min.Y < minY) minY = bb.Min.Y;
            if (bb.Min.Z < minZ) minZ = bb.Min.Z;
            if (bb.Max.X > maxX) maxX = bb.Max.X;
            if (bb.Max.Y > maxY) maxY = bb.Max.Y;
            if (bb.Max.Z > maxZ) maxZ = bb.Max.Z;
        }

        if (minX == float.MaxValue) return;

        var center = new Vector3(
            (minX + maxX) / 2f,
            (minY + maxY) / 2f,
            (minZ + maxZ) / 2f);
        var size = new Vector3(
            maxX - minX,
            maxY - minY,
            maxZ - minZ);

        _cameraOrbitTarget = center;
        _cameraDistance = MathF.Max(size.X, MathF.Max(size.Y, size.Z)) * 1.5f;
        if (_cameraDistance < 0.5f) _cameraDistance = 2f;
        _cameraYaw = 180f;
        _cameraPitch = -15f;
        UpdateCameraPosition();
    }

    private void UpdateCameraPosition()
    {
        if (_view.Scene is null) return;

        var camera = _view.MainCamera;
        float pitchRad = _cameraPitch * MathF.PI / 180f;
        float yawRad = _cameraYaw * MathF.PI / 180f;

        float x = _cameraDistance * MathF.Cos(pitchRad) * MathF.Sin(yawRad);
        float y = _cameraDistance * MathF.Sin(pitchRad);
        float z = _cameraDistance * MathF.Cos(pitchRad) * MathF.Cos(yawRad);

        camera.Position = _cameraOrbitTarget + new Vector3(x, -y, z);
        camera.LookAt(_cameraOrbitTarget);
    }
}
