using Aura3D.Avalonia;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using System.Numerics;
using YSMViewer.Services;
using YSMViewer.ViewModels;

namespace YSMViewer.Views;

public partial class MainView : UserControl
{
    private Model? _pendingModel;
    private Model? _loadedModel;
    private SphericalGizmo? _gizmo;
    private bool _isDragging;
    private bool _gizmoIsDragging;
    private Avalonia.Point _lastMousePos;
    private Avalonia.Point _gizmoLastPos;
    private Vector3 _cameraOrbitTarget = Vector3.Zero;
    private float _cameraDistance = 30f;
    private float _cameraYaw;
    private float _cameraPitch = -15f;
    private bool _sceneInitialized;

    private static readonly StreamGeometry DarkIconData =
        StreamGeometry.Parse("M12 3a9 9 0 1 0 9 9c0-.46-.04-.92-.1-1.36a5.389 5.389 0 0 1-4.4 2.26 5.403 5.403 0 0 1-3.14-9.8c-.44-.06-.9-.1-1.36-.1Z");
    private static readonly StreamGeometry LightIconData =
        StreamGeometry.Parse("M12 7a5 5 0 1 0 0 10a5 5 0 0 0 0-10Zm0-4a1 1 0 0 1 1 1v1a1 1 0 1 1-2 0V4a1 1 0 0 1 1-1Zm0 17a1 1 0 0 1 1 1v1a1 1 0 1 1-2 0v-1a1 1 0 0 1 1-1Zm9-8a1 1 0 0 1 1 1h1a1 1 0 1 1 0 2h-1a1 1 0 0 1 0-2ZM4 12a1 1 0 0 1-1 1H2a1 1 0 1 1 0-2h1a1 1 0 0 1 1 1ZM18.36 5.64a1 1 0 0 1 1.41 0l.71.7a1 1 0 0 1-1.42 1.42l-.7-.71a1 1 0 0 1 0-1.41ZM5.64 18.36a1 1 0 0 1 0 1.41l-.7.71a1 1 0 0 1-1.42-1.42l.71-.7a1 1 0 0 1 1.41 0Zm12.72 0a1 1 0 0 1 1.41 0l.71.7a1 1 0 1 1-1.42 1.42l-.7-.71a1 1 0 0 1 0-1.41ZM5.64 5.64a1 1 0 0 1-1.41 0l-.71-.7a1 1 0 0 1 1.42-1.42l.7.71a1 1 0 0 1 0 1.41Z");
    private static readonly StreamGeometry SystemIconData =
        StreamGeometry.Parse("M12 2a1 1 0 0 1 1 1v3a1 1 0 1 1-2 0V3a1 1 0 0 1 1-1Zm6.5 3.5a1 1 0 0 1 1.41 0l.71.71a1 1 0 0 1-1.42 1.41l-.7-.7a1 1 0 0 1 0-1.42ZM6.5 5.5a1 1 0 0 1 0 1.42l-.7.7a1 1 0 0 1-1.42-1.41l.71-.71a1 1 0 0 1 1.41 0ZM12 8a4 4 0 1 0 0 8a4 4 0 0 0 0-8Zm7 3a1 1 0 1 1 0 2h-3a1 1 0 1 1 0-2ZM8 12a1 1 0 0 1 0 2H5a1 1 0 1 1 0-2Zm3.5 6.5a1 1 0 0 1 1 0v3a1 1 0 1 1-2 0v-3a1 1 0 0 1 1 0Z");

    private static readonly string[] ThemeTooltips = ["Dark mode", "Light mode", "System theme"];

    public MainView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerMoved += OnPointerMoved;
        PointerWheelChanged += OnPointerWheelChanged;
        KeyDown += OnKeyDown;

        ThemeService.Instance.ModeChanged += OnThemeChanged;
        UpdateThemeIcon();
    }

    private void OnThemeChanged(AppThemeMode mode)
    {
        UpdateThemeIcon();
        UpdateSceneBackgrounds();
    }

    private void UpdateThemeIcon()
    {
        var icon = this.FindControl<PathIcon>("ThemeIcon");
        if (icon is null) return;

        var mode = ThemeService.Instance.CurrentMode;
        icon.Data = mode switch
        {
            AppThemeMode.Light => LightIconData,
            AppThemeMode.System => SystemIconData,
            _ => DarkIconData,
        };

        var btn = this.FindControl<Button>("ThemeToggleButton");
        if (btn is not null)
        {
            ToolTip.SetTip(btn, ThemeTooltips[(int)mode]);
        }
    }

    private void UpdateSceneBackgrounds()
    {
        var rgba = ThemeService.Instance.GetViewportBackgroundColor();
        var color = System.Drawing.Color.FromArgb(rgba[0], rgba[1], rgba[2], rgba[3]);

        if (AuraView.Scene is not null)
            AuraView.Scene.Background = Texture.CreateFromColor(color);
        if (GizmoView.Scene is not null)
            GizmoView.Scene.Background = Texture.CreateFromColor(color);
    }

    private void OnThemeToggleClick(object? sender, RoutedEventArgs e)
    {
        ThemeService.Instance.CycleTheme();
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.SetSceneCallback(AddModelToScene);
            _ = vm.LoadStartupFileIfNeeded();
        }

        _ = DetectRenderingFailureAsync();
    }

    private async Task DetectRenderingFailureAsync()
    {
        await Task.Delay(5000);

        if (!_sceneInitialized && DataContext is MainViewModel vm)
        {
            vm.SetError(new InvalidOperationException(
                "3D rendering failed to initialize. " +
                "This may be caused by a WebGL2-incompatible browser or a rendering pipeline error. " +
                "Please try using a modern browser with WebGL2 support (Chrome, Edge, Firefox)."));
        }
    }

    private async void OnOpenButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storage) return;

        var files = await storage.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open YSM Model",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("YSM Models")
                    {
                        Patterns = ["*.ysm"],
                    },
                ],
            });

        if (files is not { Count: > 0 }) return;

        await using var stream = await files[0].OpenReadAsync();
        using var ms = new System.IO.MemoryStream();
        await stream.CopyToAsync(ms);
        await vm.LoadFromBytesAsync(ms.ToArray());
    }

    private void OnSceneInitialized(object sender, InitializedRoutedEventArgs args)
    {
        _sceneInitialized = true;
        var view = (Aura3DView)sender;
        var scene = args.Scene;

        try
        {
            var rgba = ThemeService.Instance.GetViewportBackgroundColor();
            scene.Background = Texture.CreateFromColor(System.Drawing.Color.FromArgb(rgba[0], rgba[1], rgba[2], rgba[3]));

            var camera = view.MainCamera;
            camera.FieldOfView = 50f;
            camera.NearPlane = 0.1f;
            camera.FarPlane = 5000f;
            UpdateCameraPosition(camera);
            SyncGizmoCamera();

            var ambientLight = new DirectionalLight
            {
                LightColor = System.Drawing.Color.FromArgb(255, 80, 80, 100),
                RotationDegrees = new Vector3(-30, 45, 0),
            };
            view.AddNode(ambientLight);

            var keyLight = new DirectionalLight
            {
                LightColor = System.Drawing.Color.FromArgb(255, 220, 210, 190),
                RotationDegrees = new Vector3(-45, -30, 0),
            };
            view.AddNode(keyLight);

            var fillLight = new DirectionalLight
            {
                LightColor = System.Drawing.Color.FromArgb(255, 100, 120, 150),
                RotationDegrees = new Vector3(-10, 150, 0),
            };
            view.AddNode(fillLight);

            if (_pendingModel is not null)
            {
                try
                {
                    view.AddNode(_pendingModel);
                    FitCameraToModel(view.MainCamera, _pendingModel);
                    _loadedModel = _pendingModel;

                    if (DataContext is MainViewModel vm)
                    {
                        foreach (var comp in vm.Components)
                        {
                            if (comp.ModelNode is not null)
                                comp.ModelNode.Enable = comp.IsVisible;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (DataContext is MainViewModel vm)
                        vm.SetError(ex);
                }
                finally
                {
                    _pendingModel = null;
                }
            }
        }
        catch (Exception ex)
        {
            if (DataContext is MainViewModel vm)
                vm.SetError(ex);
        }
    }

    private void OnGizmoSceneInitialized(object sender, InitializedRoutedEventArgs args)
    {
        var view = (Aura3DView)sender;
        var scene = args.Scene;

        try
        {
            var rgba = ThemeService.Instance.GetViewportBackgroundColor();
            scene.Background = Texture.CreateFromColor(System.Drawing.Color.FromArgb(rgba[0], rgba[1], rgba[2], rgba[3]));

            var camera = view.MainCamera;
            camera.FieldOfView = 40f;
            camera.NearPlane = 0.01f;
            camera.FarPlane = 100f;

            _gizmo = new SphericalGizmo();
            view.AddNode(_gizmo);

            SyncGizmoCamera();
        }
        catch (Exception ex)
        {
            if (DataContext is MainViewModel vm)
                vm.SetError(ex);
        }
    }

    private void SyncGizmoCamera()
    {
        if (GizmoView.Scene is null) return;

        const float gizmoCamDist = 2.5f;
        float pitchRad = _cameraPitch * MathF.PI / 180f;
        float yawRad = _cameraYaw * MathF.PI / 180f;

        float x = gizmoCamDist * MathF.Cos(pitchRad) * MathF.Sin(yawRad);
        float y = gizmoCamDist * MathF.Sin(pitchRad);
        float z = gizmoCamDist * MathF.Cos(pitchRad) * MathF.Cos(yawRad);

        var cam = GizmoView.MainCamera;
        cam.Position = new Vector3(x, -y, z);
        cam.LookAt(Vector3.Zero);
    }

    private void OnGizmoPointerEntered(object? sender, PointerEventArgs e)
    {
        GizmoBorder.Background = new SolidColorBrush(Avalonia.Media.Color.FromArgb(128, 0, 0, 0));
    }

    private void OnGizmoPointerExited(object? sender, PointerEventArgs e)
    {
        GizmoBorder.Background = Brushes.Transparent;
    }

    private void OnGizmoPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        if (props.IsLeftButtonPressed)
        {
            _gizmoIsDragging = true;
            _gizmoLastPos = e.GetPosition(this);
            e.Handled = true;
        }
    }

    private void OnGizmoPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _gizmoIsDragging = false;
    }

    private void FitCameraToModel(Camera camera, Model model)
    {
        var bb = model.BoundingBox;
        var center = new Vector3(
            (bb.Min.X + bb.Max.X) / 2f,
            (bb.Min.Y + bb.Max.Y) / 2f,
            (bb.Min.Z + bb.Max.Z) / 2f);
        var size = new Vector3(
            bb.Max.X - bb.Min.X,
            bb.Max.Y - bb.Min.Y,
            bb.Max.Z - bb.Min.Z);
        _cameraOrbitTarget = center;
        _cameraDistance = MathF.Max(size.X, MathF.Max(size.Y, size.Z)) * 1.5f;
        _cameraYaw = 0f;
        _cameraPitch = -15f;
        UpdateCameraPosition(camera);
        SyncGizmoCamera();
    }

    private void UpdateCameraPosition(Camera camera)
    {
        float pitchRad = _cameraPitch * MathF.PI / 180f;
        float yawRad = _cameraYaw * MathF.PI / 180f;

        float x = _cameraDistance * MathF.Cos(pitchRad) * MathF.Sin(yawRad);
        float y = _cameraDistance * MathF.Sin(pitchRad);
        float z = _cameraDistance * MathF.Cos(pitchRad) * MathF.Cos(yawRad);

        camera.Position = _cameraOrbitTarget + new Vector3(x, -y, z);
        camera.LookAt(_cameraOrbitTarget);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;

        if (props.IsRightButtonPressed)
        {
            _isDragging = true;
            _lastMousePos = e.GetPosition(this);
            e.Handled = true;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
        _gizmoIsDragging = false;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (AuraView.Scene is null) return;

        if (_isDragging)
        {
            var pos = e.GetPosition(this);
            float dx = (float)(pos.X - _lastMousePos.X);
            float dy = (float)(pos.Y - _lastMousePos.Y);
            _lastMousePos = pos;

            _cameraYaw -= dx * 0.3f;
            _cameraPitch += dy * 0.3f;
            _cameraPitch = Math.Clamp(_cameraPitch, -89f, 89f);

            UpdateCameraPosition(AuraView.MainCamera);
            SyncGizmoCamera();
        }
        else if (_gizmoIsDragging)
        {
            var pos = e.GetPosition(this);
            float dx = (float)(pos.X - _gizmoLastPos.X);
            float dy = (float)(pos.Y - _gizmoLastPos.Y);
            _gizmoLastPos = pos;

            _cameraYaw -= dx * 0.3f;
            _cameraPitch += dy * 0.3f;
            _cameraPitch = Math.Clamp(_cameraPitch, -89f, 89f);

            UpdateCameraPosition(AuraView.MainCamera);
            SyncGizmoCamera();
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (AuraView.Scene is null) return;

        _cameraDistance *= 1f - (float)e.Delta.Y * 0.1f;
        _cameraDistance = MathF.Max(_cameraDistance, 0.5f);
        UpdateCameraPosition(AuraView.MainCamera);
        SyncGizmoCamera();
    }

    private void OnSceneUpdated(object sender, UpdateRoutedEventArgs e)
    {
        if (_sceneInitialized && DataContext is MainViewModel vm)
        {
            vm.UpdateAnimation((float)e.DeltaTime);
        }
    }

    private void AddModelToScene(Model modelNode)
    {
        try
        {
            if (_loadedModel is not null && AuraView.Scene is not null)
            {
                AuraView.Scene.RemoveNode(_loadedModel);
                _loadedModel = null;
            }

            if (AuraView.Scene is not null)
            {
                AuraView.AddNode(modelNode);
                FitCameraToModel(AuraView.MainCamera, modelNode);
                _loadedModel = modelNode;

                if (DataContext is MainViewModel vm)
                {
                    foreach (var comp in vm.Components)
                    {
                        if (comp.ModelNode is not null)
                            comp.ModelNode.Enable = comp.IsVisible;
                    }
                }
            }
            else
            {
                _pendingModel = modelNode;
            }
        }
        catch (Exception ex)
        {
            if (DataContext is MainViewModel vm)
                vm.SetError(ex);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
    }

    private void OnDismissErrorClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.HasError = false;
        }
    }

    private void OnPlayPauseClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.IsAnimating = !vm.IsAnimating;
            if (sender is Button btn)
            {
                var icon = btn.FindControl<PathIcon>("PlayPauseIcon");
                if (icon is not null)
                {
                    icon.Data = vm.IsAnimating
                        ? Avalonia.Media.Geometry.Parse("M6 4 L6 28 L12 28 L12 4 Z M18 4 L18 28 L24 28 L24 4 Z")
                        : Avalonia.Media.Geometry.Parse("M8 4 L8 28 L24 16 Z");
                }
            }
        }
    }

    private void OnPreviousAnimationClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.PreviousAnimation();
    }

    private void OnNextAnimationClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.NextAnimation();
    }

    private void OnAnimationSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm && e.AddedItems.Count > 0 && e.AddedItems[0] is string name)
        {
            vm.SelectAnimation(name);
        }
    }

    private void OnShowAllComponentsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            foreach (var comp in vm.Components)
                comp.IsVisible = true;
        }
    }

    private void OnHideAllComponentsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            foreach (var comp in vm.Components)
                comp.IsVisible = false;
        }
    }

    private void OnExpandAllBonesClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.ExpandAllBones();
    }

    private void OnCollapseAllBonesClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.CollapseAllBones();
    }
}