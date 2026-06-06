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

    private DirectionalLight? _ambientLight;
    private DirectionalLight? _keyLight;
    private DirectionalLight? _fillLight;

    private static readonly StreamGeometry MoonIconData =
        StreamGeometry.Parse("M20.996 11.712 L22.245 11.672 A1.25 1.25 0 0 0 20.64 10.513 L12.289 3.005 L13.487 3.36 A1.25 1.25 0 0 0 12.327 1.755 L21.639 12.712 A5.8 5.8 0 0 1 19 10.75 L19 13.25 A8.3 8.3 0 0 0 21.351 12.91 L19 10.75 A5.75 5.75 0 0 1 13.25 5 L10.75 5 A8.25 8.25 0 0 0 19 13.25 L13.25 5 C13.25 4.428 13.333 3.878 13.487 3.36 L11.09 2.65 A8.3 8.3 0 0 0 10.75 5 L12 4.25 Q12.124 4.25 12.25 4.254 L12.328 1.755 A10 10 0 0 0 12 1.75 L4.25 12 A7.75 7.75 0 0 1 12 4.25 L12 1.75 C6.34 1.75 1.75 6.34 1.75 12 L12 19.75 A7.75 7.75 0 0 1 4.25 12 L1.75 12 C1.75 17.66 6.34 22.25 12 22.25 L19.75 12 A7.75 7.75 0 0 1 12 19.75 L12 22.25 C17.66 22.25 22.25 17.66 22.25 12 L19.746 11.75 Q19.75 11.876 19.75 12 L22.25 12 Q22.25 11.835 22.245 11.672 Z");

    private static readonly StreamGeometry SunIconData =
        StreamGeometry.Parse("M11 2 L13 2 L13 7 L11 7 Z M11 17 L13 17 L13 22 L11 22 Z M2 11 L7 11 L7 13 L2 13 Z M17 11 L22 11 L22 13 L17 13 Z M5.64 4.22 L7.76 6.34 L6.34 7.76 L4.22 5.64 Z M16.24 16.24 L18.36 18.36 L16.95 19.78 L14.83 17.66 Z M4.22 19.78 L6.34 17.66 L7.76 16.24 L5.64 18.36 Z M17.66 7.76 L16.24 6.34 L18.36 4.22 L19.78 5.64 Z M12 8 A4 4 0 0 1 12 16 A4 4 0 0 1 12 8 Z");

    private static readonly StreamGeometry SystemIconData =
        StreamGeometry.Parse("M4 2 L20 2 Q22 2 22 4 L22 14 Q22 16 20 16 L4 16 Q2 16 2 14 L2 4 Q2 2 4 2 Z M3 9 L21 9 L21 11 L3 11 Z M9 16 L15 16 L15 19 L9 19 Z M7 20 L17 20 L17 22 L7 22 Z");

    private static readonly string[] ThemeTooltips = ["Switch to Light mode", "Switch to System theme", "Switch to Dark mode"];

    public MainView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerMoved += OnPointerMoved;
        PointerWheelChanged += OnPointerWheelChanged;
        KeyDown += OnKeyDown;

        DragDrop.AddDropHandler(this, OnDrop);
        DragDrop.AddDragOverHandler(this, OnDragOverHandler);
        DragDrop.AddDragEnterHandler(this, OnDragEnterHandler);
        DragDrop.AddDragLeaveHandler(this, OnDragLeaveHandler);

        ThemeService.Instance.ModeChanged += OnThemeChanged;
        UpdateThemeIcon();
    }

    private void OnThemeChanged(AppThemeMode mode)
    {
        UpdateThemeIcon();
        UpdateSceneAppearance();
    }

    private void UpdateThemeIcon()
    {
        var icon = this.FindControl<PathIcon>("ThemeIcon");
        if (icon is null) return;

        var mode = ThemeService.Instance.CurrentMode;
        icon.Data = mode switch
        {
            AppThemeMode.Light => SunIconData,
            AppThemeMode.System => SystemIconData,
            _ => MoonIconData,
        };

        var btn = this.FindControl<Button>("ThemeToggleButton");
        if (btn is not null)
        {
            ToolTip.SetTip(btn, ThemeTooltips[(int)mode]);
        }
    }

    private void UpdateSceneAppearance()
    {
        UpdateSceneBackgrounds();
        UpdateSceneLights();
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

    private void UpdateSceneLights()
    {
        if (!_sceneInitialized) return;

        var ambient = ThemeService.Instance.GetAmbientLightColor();
        var key = ThemeService.Instance.GetKeyLightColor();
        var fill = ThemeService.Instance.GetFillLightColor();

        if (_ambientLight is not null)
            _ambientLight.LightColor = System.Drawing.Color.FromArgb(ambient.A, ambient.R, ambient.G, ambient.B);
        if (_keyLight is not null)
            _keyLight.LightColor = System.Drawing.Color.FromArgb(key.A, key.R, key.G, key.B);
        if (_fillLight is not null)
            _fillLight.LightColor = System.Drawing.Color.FromArgb(fill.A, fill.R, fill.G, fill.B);
    }

    private void OnThemeToggleClick(object? sender, RoutedEventArgs e)
    {
        ThemeService.Instance.CycleTheme();
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var overlay = this.FindControl<Border>("DragOverlay");
        if (overlay is not null) overlay.IsVisible = false;
        e.Handled = true;

        if (DataContext is not MainViewModel vm) return;

        if (!e.DataTransfer.Formats.Contains(DataFormat.File)) return;

        var files = e.DataTransfer.TryGetFiles();
        if (files is null) return;

        foreach (var file in files)
        {
            var path = file.TryGetLocalPath();
            if (path is null) continue;

            if (!path.EndsWith(".ysm", StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                await vm.LoadFileAsync(path);
            }
            catch (Exception ex)
            {
                vm.SetError(ex);
            }
            return;
        }

        foreach (var file in files)
        {
            if (file is not IStorageFile storageFile) continue;
            try
            {
                await using var stream = await storageFile.OpenReadAsync();
                using var ms = new System.IO.MemoryStream();
                await stream.CopyToAsync(ms);
                await vm.LoadFromBytesAsync(ms.ToArray());
                return;
            }
            catch (Exception ex)
            {
                vm.SetError(ex);
            }
        }
    }

    private void OnDragOverHandler(object? sender, DragEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.IsLoading)
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (e.DataTransfer.Formats.Contains(DataFormat.File))
            e.DragEffects = DragDropEffects.Copy;
        else
            e.DragEffects = DragDropEffects.None;

        e.Handled = true;
    }

    private void OnDragEnterHandler(object? sender, DragEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.IsLoading) return;

        if (e.DataTransfer.Formats.Contains(DataFormat.File))
        {
            var overlay = this.FindControl<Border>("DragOverlay");
            if (overlay is not null) overlay.IsVisible = true;
        }
    }

    private void OnDragLeaveHandler(object? sender, DragEventArgs e)
    {
        var overlay = this.FindControl<Border>("DragOverlay");
        if (overlay is not null) overlay.IsVisible = false;
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

            var ambient = ThemeService.Instance.GetAmbientLightColor();
            _ambientLight = new DirectionalLight
            {
                LightColor = System.Drawing.Color.FromArgb(ambient.A, ambient.R, ambient.G, ambient.B),
                RotationDegrees = new Vector3(-30, 45, 0),
            };
            view.AddNode(_ambientLight);

            var key = ThemeService.Instance.GetKeyLightColor();
            _keyLight = new DirectionalLight
            {
                LightColor = System.Drawing.Color.FromArgb(key.A, key.R, key.G, key.B),
                RotationDegrees = new Vector3(-45, -30, 0),
            };
            view.AddNode(_keyLight);

            var fill = ThemeService.Instance.GetFillLightColor();
            _fillLight = new DirectionalLight
            {
                LightColor = System.Drawing.Color.FromArgb(fill.A, fill.R, fill.G, fill.B),
                RotationDegrees = new Vector3(-10, 150, 0),
            };
            view.AddNode(_fillLight);

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

    private async void OnCopyErrorClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard is not null)
            {
                var data = new Avalonia.Input.DataTransfer();
                data.Add(Avalonia.Input.DataTransferItem.CreateText(vm.ErrorDetail));
                await topLevel.Clipboard.SetDataAsync(data);
                vm.Notifications.Show("Copied to clipboard", NotificationType.Info, 2000);
            }
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