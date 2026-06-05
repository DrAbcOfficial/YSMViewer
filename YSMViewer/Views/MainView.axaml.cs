using Aura3D.Avalonia;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using System.Numerics;
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

    public MainView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        OpenButton.Click += OnOpenButtonClick;
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerMoved += OnPointerMoved;
        PointerWheelChanged += OnPointerWheelChanged;
        KeyDown += OnKeyDown;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.SetSceneCallback(AddModelToScene);
            _ = vm.LoadStartupFileIfNeeded();
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
        var view = (Aura3DView)sender;
        var scene = args.Scene;

        scene.Background = Texture.CreateFromColor(System.Drawing.Color.FromArgb(255, 26, 27, 38));

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

    private void OnGizmoSceneInitialized(object sender, InitializedRoutedEventArgs args)
    {
        var view = (Aura3DView)sender;
        var scene = args.Scene;

        scene.Background = Texture.CreateFromColor(System.Drawing.Color.FromArgb(255, 26, 27, 38));

        var camera = view.MainCamera;
        camera.FieldOfView = 40f;
        camera.NearPlane = 0.01f;
        camera.FarPlane = 100f;

        _gizmo = new SphericalGizmo();
        view.AddNode(_gizmo);

        SyncGizmoCamera();
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

        if (props.IsMiddleButtonPressed)
        {
            var overlay = this.FindControl<Border>("RadialOverlay");
            if (overlay is not null && overlay.IsVisible)
            {
                CloseRadialMenu();
                e.Handled = true;
                return;
            }

            if (DataContext is MainViewModel vm && vm.Components.Count > 0)
            {
                var pos = e.GetPosition(this);
                ShowRadialMenuAt(pos.X, pos.Y);
            }
            e.Handled = true;
            return;
        }

        if (props.IsRightButtonPressed)
        {
            var overlay = this.FindControl<Border>("RadialOverlay");
            if (overlay is not null && overlay.IsVisible)
            {
                CloseRadialMenu();
                e.Handled = true;
                return;
            }

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
        if (DataContext is MainViewModel vm)
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
        if (e.Key == Key.Escape)
        {
            CloseRadialMenu();
        }
    }

    private void ShowRadialMenuAt(double x, double y)
    {
        if (DataContext is not MainViewModel vm || vm.Components.Count == 0) return;

        var overlay = this.FindControl<Border>("RadialOverlay");
        var menu = this.FindControl<RadialMenu>("RadialMenuControl");
        if (overlay is null || menu is null) return;

        double menuSize = 200;
        var panel = this.FindControl<Panel>("ViewportPanel");
        double areaW = panel?.Bounds.Width ?? Bounds.Width;
        double areaH = panel?.Bounds.Height ?? Bounds.Height;

        double marginLeft = Math.Max(0, Math.Min(x - menuSize / 2, areaW - menuSize));
        double marginTop = Math.Max(0, Math.Min(y - menuSize / 2, areaH - menuSize));

        overlay.Margin = new Avalonia.Thickness(marginLeft, marginTop, 0, 0);

        var items = vm.Components.Select(c => new RadialMenuItem
        {
            Label = c.Name.Length > 8 ? c.Name[..8] : c.Name,
            IsOn = c.IsVisible,
            ToggleAction = () => c.IsVisible = !c.IsVisible,
        }).ToList();

        menu.SetItems(items);
        menu.IsOpen = true;
        menu.ItemClicked += OnRadialItemClicked;
        overlay.IsVisible = true;
    }

    private void CloseRadialMenu()
    {
        var overlay = this.FindControl<Border>("RadialOverlay");
        var menu = this.FindControl<RadialMenu>("RadialMenuControl");
        menu?.ItemClicked -= OnRadialItemClicked;
        overlay?.IsVisible = false;
    }

    private void OnCopyErrorClick(object? sender, RoutedEventArgs e)
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
                btn.Content = vm.IsAnimating ? "\u23F8" : "\u25B6";
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

    private void OnAnimationDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.HasAnimations)
            vm.IsAnimating = true;
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

    private void OnRadialToggleClick(object? sender, RoutedEventArgs e)
    {
        ShowRadialMenuAt(Bounds.Width / 2, Bounds.Height / 2);
    }

    private void OnRadialItemClicked(object? sender, int index)
    {
        CloseRadialMenu();
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

    private void OnTextureItemPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Border border)
            border.Background = new SolidColorBrush(Avalonia.Media.Color.FromArgb(255, 36, 40, 59));
    }

    private void OnTextureItemPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Border border)
            border.Background = new SolidColorBrush(Avalonia.Media.Color.FromArgb(255, 31, 35, 53));
    }
}
