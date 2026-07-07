using Aura3D.Avalonia;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Avalonia.Controls;
using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Numerics;
using System.Text.Json;
using YSMViewer.Desktop.Services.Audio;
using YSMViewer.Models;
using YSMViewer.Models.AnimationController;
using YSMViewer.Models.Document;
using YSMViewer.Rendering;
using YSMViewer.Services;
using YSMViewer.Services.Animation;
using YSMViewer.Services.Audio;
using YSMViewer.Services.Molang;

namespace YSMViewer.Desktop.Rendering.Aura3D;

public sealed class Aura3DRenderer : IAnimationRenderer, IInteractiveRenderer, IDisposable
{
    private static readonly ILogger Logger = YsmLog.For<Aura3DRenderer>();
    private readonly Aura3DView _view;
    private readonly Aura3DView _gizmoView;
    private Model? _loadedModel;
    private YsmModelDocument? _document;
    private readonly Dictionary<string, Model> _componentModels = [];
    private readonly Dictionary<string, Node> _boneNodes = [];
    private readonly Dictionary<string, List<(string key, Node node)>> _boneNameToNodes = [];
    private readonly Dictionary<string, Vector3> _baseBoneEulers = [];
    private readonly Dictionary<string, Vector3> _basePositions = [];
    private readonly List<Model> _sceneRoots = [];
    private readonly AnimationService _animService = new();
    private Dictionary<string, IAnimatableBone>? _animBones;
    private bool _sceneInitialized;

    private MolangService? _molangService;
    private AnimationStateMachine? _stateMachine;
    private AnimationAudioService? _audioService;
    private float _animTime;
    private bool _useAnimationController;

    private readonly HashSet<string> _bonesAnimatedThisFrame = [];
    private const float ResetSpeed = 0.15f;

    public (float Pitch, float Yaw) GetCameraOrbit() => (_cameraPitch, _cameraYaw);

    private Vector3 _cameraOrbitTarget = Vector3.Zero;
    private float _cameraDistance = 30f;
    private float _cameraYaw = 180f;
    private float _cameraPitch = -15f;

    public Aura3DRenderer()
    {
        _view = new Aura3DView
        {
            MinWidth = 1,
            MinHeight = 1,
            CreateRenderPipeline = scene => new YSMPipeline(scene)
        };
        _view.SceneInitialized += OnSceneInitialized;
        _view.SceneUpdated += OnSceneUpdated;

        _gizmoView = new Aura3DView
        {
            MinWidth = 1,
            MinHeight = 1,
            IsHitTestVisible = false,
            CreateRenderPipeline = scene => new NoLightPipeline(scene)
        };
        _gizmoView.SceneInitialized += OnGizmoSceneInitialized;
    }

    public Control View => _view;
    public Control? GizmoControl => _gizmoView;
    public RendererCapabilities Capabilities => RendererCapabilities.Desktop;
    public IReadOnlyList<string> AnimationNames => _animService.AnimationNames;
    public float AnimationDuration => _animService.AnimationLength;
    public float AnimationCurrentTime => _animService.CurrentTime;
    public bool HasAnimationController { get; private set; }

    public bool UseAnimationController
    {
        get => _useAnimationController;
        set => _useAnimationController = value && HasAnimationController;
    }

    public MolangService? MolangService => _molangService;

    private void OnGizmoSceneInitialized(object? sender, InitializedRoutedEventArgs args)
    {
        var scene = args.Scene;
        if (scene is null) return;

        try
        {
            var rgba = ThemeService.Instance.GetViewportBackgroundColor();
            scene.Background = Texture.CreateFromColor(
                Color.FromArgb(rgba[0], rgba[1], rgba[2], rgba[3]));
            scene.RenderPipeline.EnableFrustumCulling = true;

            var camera = _gizmoView.MainCamera;
            camera.FieldOfView = 40f;
            camera.NearPlane = 0.01f;
            camera.FarPlane = 100f;

            var gizmo = new SphericalGizmo();
            _gizmoView.AddNode(gizmo);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to initialize gizmo scene");
        }
    }

    private void SyncGizmoCamera()
    {
        if (_gizmoView.Scene is null) return;

        const float gizmoCamDist = 2.5f;
        float pitchRad = _cameraPitch * MathF.PI / 180f;
        float yawRad = _cameraYaw * MathF.PI / 180f;

        float x = gizmoCamDist * MathF.Cos(pitchRad) * MathF.Sin(yawRad);
        float y = gizmoCamDist * MathF.Sin(pitchRad);
        float z = gizmoCamDist * MathF.Cos(pitchRad) * MathF.Cos(yawRad);

        var cam = _gizmoView.MainCamera;
        cam.Position = new Vector3(x, -y, z);
        cam.LookAt(Vector3.Zero);
    }

    public void SyncGizmo()
    {
        SyncGizmoCamera();
    }

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
            {
                foreach (var t in document.Textures)
                {
                    if (t.Id == geoModel.TextureId) { tex = t; break; }
                }
                tex ??= document.Textures.Count > 0 ? document.Textures[0] : null;
            }

            var result = Aura3DModelBuilder.BuildFromDocument(geoModel, tex);
            result.RootModel.Enable = geoModel.DefaultVisible;
            _componentModels[geoModel.Id] = result.RootModel;

            // Use compound key to distinguish bones across components
            foreach (var kv in result.BoneNodes)
            {
                var compoundKey = $"{geoModel.Id}:{kv.Key}";
                _boneNodes[compoundKey] = kv.Value;

                if (!_boneNameToNodes.TryGetValue(kv.Key, out var list))
                {
                    list = [];
                    _boneNameToNodes[kv.Key] = list;
                }
                list.Add((compoundKey, kv.Value));
            }

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
        _basePositions.Clear();
        foreach (var geoModel in document.Models)
        {
            foreach (var bone in geoModel.Bones)
            {
                var compoundKey = $"{geoModel.Id}:{bone.Id}";
                if (_boneNodes.TryGetValue(compoundKey, out var node) && !_animBones.ContainsKey(bone.Id))
                {
                    _animBones[bone.Id] = new Aura3DBoneNode(node);
                    _basePositions[bone.Id] = node.Position;
                }
            }
        }

        foreach (var geoModel in document.Models)
        {
            foreach (var bone in geoModel.Bones)
            {
                if (_animBones.TryGetValue(bone.Id, out var animBone))
                    animBone.PivotPosition = bone.Pivot;
            }
        }

        _animService.SetBoneNodes(_animBones, _baseBoneEulers);

        _molangService = new MolangService
        {
            BoneNodes = _animBones,
            BasePositions = _basePositions
        };
        _animService.MolangService = _molangService;

        if (document.Sounds.Count > 0)
        {
            _audioService = new AnimationAudioService(new DesktopAudioPlayer(), document.Sounds);
            _molangService.AudioHost = _audioService;
        }

        foreach (var fn in document.Functions)
            _molangService.RegisterFunction(fn.Name, fn.Data);

        foreach (var anim in document.Animations)
            _animService.LoadAnimations(anim.Data);

        HasAnimationController = false;
        _stateMachine = null;

        if (document.AnimControllers.Count > 0)
        {
            var (controllerKey, controllerEntry, allControllers) = ParseFirstController(document.AnimControllers[0].Data);
            if (controllerEntry is not null)
            {
                var context = CreateAnimationContext(controllerEntry, controllerKey, allControllers);
                _stateMachine = new AnimationStateMachine(controllerEntry, context);
                _stateMachine.Initialize();
                _molangService.StateMachineHost = _stateMachine;
                HasAnimationController = true;
                _useAnimationController = true;
            }
        }
    }

    private static (string? Key, AnimationControllerEntry? Entry, Dictionary<string, AnimationControllerEntry>? AllControllers) ParseFirstController(byte[] data)
    {
        try
        {
            var text = System.Text.Encoding.UTF8.GetString(data);
            var file = JsonSerializer.Deserialize(text, YsmJsonContext.Default.AnimationControllerFile);
            if (file?.Controllers is null || file.Controllers.Count == 0)
                return (null, null, null);
            var first = file.Controllers.First();
            return (first.Key, first.Value, file.Controllers);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to parse animation controller data");
            return (null, null, null);
        }
    }

    private AnimationContext CreateAnimationContext(AnimationControllerEntry controller, string? controllerKey, Dictionary<string, AnimationControllerEntry>? allControllers)
    {
        var anims = new Dictionary<string, MinecraftAnimation>(StringComparer.OrdinalIgnoreCase);
        foreach (var anim in _animService.GetAllAnimations())
            anims[anim.Key] = anim.Value;

        return new AnimationContext
        {
            Molang = _molangService!,
            Animations = anims,
            BoneNodes = _animBones!,
            BasePositions = _basePositions,
            BaseEulers = _baseBoneEulers,
            AllControllers = allControllers,
            ControllerNameHint = controllerKey ?? "",
        };
    }

    public void Dispose()
    {
        _animService.IsPlaying = false;
        _animService.ResetBones();
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
        _animTime = 0f;
        _audioService?.Dispose();
        _audioService = null;
        _stateMachine = null;
        _molangService = null;
        HasAnimationController = false;
        _useAnimationController = false;
        _componentModels.Clear();
        _boneNodes.Clear();
        _boneNameToNodes.Clear();
        _baseBoneEulers.Clear();
        _basePositions.Clear();
    }

    public void Clear()
    {
        Dispose();
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
        _molangService?.ResetPhysics();
        if (_stateMachine is not null && _useAnimationController)
        {
            _animTime = 0f;
            ((IAnimationStateMachineHost)_stateMachine).Reset();
            _stateMachine.SetAnimation(name, 0);
            _animService.IsPlaying = true;
        }
        else
        {
            _animService.ResetBones();
            _animService.PlayAnimation(name);
            _animService.IsPlaying = true;
        }
    }

    public void StopAnimation()
    {
        _animService.IsPlaying = false;
        _molangService?.ResetPhysics();
        if (_stateMachine is not null)
            ((IAnimationStateMachineHost)_stateMachine).Reset();
        _animService.ResetBones();
        _animTime = 0f;
    }

    public void Update(float deltaTime)
    {
        if (_stateMachine is not null && _useAnimationController)
        {
            _molangService?.ResetFrame(deltaTime);
            _animTime += deltaTime;
            bool isMoving = _molangService?.SafeGetUserVar("is_moving") > 0.5;
            _stateMachine.Process(_animTime, deltaTime, isMoving);

            _bonesAnimatedThisFrame.Clear();

            _stateMachine.ForEachTransform((boneName, pos, rot, scale) =>
            {
                if (_animBones!.TryGetValue(boneName, out var bone))
                {
                    bone.Position = pos;
                    bone.RotationQuaternion = rot;
                    bone.Scale = scale;
                    _bonesAnimatedThisFrame.Add(boneName);
                }
            });

            foreach (var (boneName, entries) in _boneNameToNodes)
            {
                var vis = _stateMachine.GetBoneVisibility(boneName);
                foreach (var (_, node) in entries)
                    node.Enable = vis;
            }

            if (_bonesAnimatedThisFrame.Count > 0 && _animBones is not null)
            {
                foreach (var (boneName, bone) in _animBones)
                {
                    if (_bonesAnimatedThisFrame.Contains(boneName)) continue;

                    float t = Math.Clamp(deltaTime / ResetSpeed, 0f, 1f);

                    if (_basePositions.TryGetValue(boneName, out var basePos))
                        bone.Position = Vector3.Lerp(bone.Position, basePos, t);

                    if (_baseBoneEulers.TryGetValue(boneName, out var baseEuler))
                    {
                        var targetQuat = AnimationService.CreateBlockbenchQuaternion(baseEuler);
                        bone.RotationQuaternion = Quaternion.Normalize(
                            Quaternion.Slerp(bone.RotationQuaternion, targetQuat, t));
                    }

                    bone.Scale = Vector3.Lerp(bone.Scale, Vector3.One, t);
                }
            }
        }
        else
        {
            _molangService?.ResetFrame(deltaTime);
            _animService.Update(deltaTime);
        }
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
                        AddModelToScene(compModel);
                }

                FitCameraToContent();
                _loadedModel = null;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Scene init error");
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
