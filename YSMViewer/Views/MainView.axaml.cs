using Aura3D.Avalonia;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Svg.Skia;
using Svg.Model;
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
    private float _cameraYaw = 180f;
    private float _cameraPitch = -15f;
    private bool _sceneInitialized;

    private static readonly string[] ThemeTooltips = ["Switch to Dark mode", "Switch to System theme", "Switch to Light mode"];
    private static readonly string[] ThemeSvgPaths =
    [
        "avares://YSMViewer/Assets/svg/mode-system.svg",
        "avares://YSMViewer/Assets/svg/mode-light.svg",
        "avares://YSMViewer/Assets/svg/mode-dark.svg",
    ];

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
        ApplyAllSvgColors();
        UpdateSceneAppearance();
    }

    private void UpdateThemeIcon()
    {
        var img = this.FindControl<Image>("ThemeSvgImage");
        if (img is null) return;

        var mode = ThemeService.Instance.CurrentMode;
        LoadSvgWithColor(img, ThemeSvgPaths[(int)mode]);

        var btn = this.FindControl<Button>("ThemeToggleButton");
        if (btn is not null)
        {
            ToolTip.SetTip(btn, ThemeTooltips[(int)mode]);
        }
    }

    private void ApplyAllSvgColors()
    {
        var paths = new (string Name, string Path)[]
        {
            ("LangSvgImage", "avares://YSMViewer/Assets/svg/lang.svg"),
            ("ThemeSvgImage", ThemeSvgPaths[(int)ThemeService.Instance.CurrentMode]),
            ("GitHubSvgImage", "avares://YSMViewer/Assets/svg/github.svg"),
            ("CameraFrontImg", "avares://YSMViewer/Assets/svg/up-junction.svg"),
            ("CameraLeftImg", "avares://YSMViewer/Assets/svg/left-junction.svg"),
            ("CameraTopImg", "avares://YSMViewer/Assets/svg/down-junction.svg"),
        };

        foreach (var (name, path) in paths)
        {
            var img = this.FindControl<Image>(name);
            if (img is not null)
                LoadSvgWithColor(img, path);
        }
    }

    private static void LoadSvgWithColor(Image image, string svgPath)
    {
        var color = ThemeService.Instance.IsDarkTheme() ? "#8b949e" : "#656d76";
        try
        {
            var source = SvgSource.Load(svgPath, new Uri("avares://YSMViewer/"));
            source.ReLoad(new SvgParameters(null, $":root {{ color: {color}; }}"));
            image.Source = new SvgImage { Source = source };
        }
        catch
        {
        }
    }

    private void UpdateSceneAppearance()
    {
        UpdateSceneBackgrounds();
    }

    private void UpdateSceneBackgrounds()
    {
        var rgba = ThemeService.Instance.GetViewportBackgroundColor();
        var color = System.Drawing.Color.FromArgb(rgba[0], rgba[1], rgba[2], rgba[3]);

        AuraView.Scene?.Background = Texture.CreateFromColor(color);
        GizmoView.Scene?.Background = Texture.CreateFromColor(color);
    }

    private void OnThemeToggleClick(object? sender, RoutedEventArgs e)
    {
        ThemeService.Instance.CycleTheme();
    }

    private async void OnGitHubClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;
        await topLevel.Launcher.LaunchUriAsync(new Uri("https://github.com/DrAbcOfficial/YSMViewer"));
    }

    private void OnLanguageButtonClick(object? sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();

        var enIcon = new Image { Width = 18, Height = 18 };
        LoadSvgWithColor(enIcon, "avares://YSMViewer/Assets/svg/lang-en.svg");
        var enItem = new MenuItem { Header = "English", Icon = enIcon };
        enItem.Click += (_, _) =>
        {
            LocalizationService.Instance.SetLanguage("en");
            menu.Close();
        };
        menu.Items.Add(enItem);

        var zhIcon = new Image { Width = 18, Height = 18 };
        LoadSvgWithColor(zhIcon, "avares://YSMViewer/Assets/svg/lang-cn.svg");
        var zhItem = new MenuItem { Header = "中文", Icon = zhIcon };
        zhItem.Click += (_, _) =>
        {
            LocalizationService.Instance.SetLanguage("zh");
            menu.Close();
        };
        menu.Items.Add(zhItem);

        menu.Open(sender as Control ?? this);
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var overlay = this.FindControl<Border>("DragOverlay");
        overlay?.IsVisible = false;
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
            overlay?.IsVisible = true;
        }
    }

    private void OnDragLeaveHandler(object? sender, DragEventArgs e)
    {
        var overlay = this.FindControl<Border>("DragOverlay");
        overlay?.IsVisible = false;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        ApplyAllSvgColors();

        if (DataContext is MainViewModel vm)
        {
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
        _sceneInitialized = true;
        var view = (Aura3DView)sender;
        var scene = args.Scene;

        try
        {
            var rgba = ThemeService.Instance.GetViewportBackgroundColor();
            scene.Background = Texture.CreateFromColor(System.Drawing.Color.FromArgb(rgba[0], rgba[1], rgba[2], rgba[3]));
            scene.RenderPipeline.EnableFrustumCulling = true;


            var camera = view.MainCamera;
            camera.FieldOfView = 50f;
            camera.NearPlane = 0.1f;
            camera.FarPlane = 5000f;
            UpdateCameraPosition(camera);
            SyncGizmoCamera();

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
            scene.RenderPipeline.EnableFrustumCulling = true;

            var pl = new PointLight()
            {
                LightColor = System.Drawing.Color.White,
                LuminousIntensity = 9999.0f,
                Position = new Vector3(0, 0, 0),
                AttenuationRadius = 9999.0f,
                CastShadow = false
            };
            scene.AddNode(pl);

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
        _cameraYaw = 180f;
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
                var dl = new DirectionalLight
                {
                    RotationDegrees = new Vector3(-45, 45, 0),
                    LightColor = System.Drawing.Color.White,
                    CastShadow = false
                };

                AuraView.AddNode(dl);

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
                icon?.Data = vm.IsAnimating
                        ? Avalonia.Media.Geometry.Parse("M6 4 L6 28 L12 28 L12 4 Z M18 4 L18 28 L24 28 L24 4 Z")
                        : Avalonia.Media.Geometry.Parse("M8 4 L8 28 L24 16 Z");
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

    private void OnStopAnimationClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.StopAnimation();
            PlayPauseBtn.Content = new PathIcon
            {
                Data = StreamGeometry.Parse("M8 4 L8 28 L24 16 Z"),
                Foreground = Brushes.White,
            };
        }
    }

    private void SetCameraView(float yaw, float pitch)
    {
        if (!_sceneInitialized || AuraView.Scene is null) return;
        _cameraYaw = yaw;
        _cameraPitch = pitch;
        UpdateCameraPosition(AuraView.MainCamera);
        SyncGizmoCamera();
    }

    private void OnCameraFrontClick(object? sender, RoutedEventArgs e)
    {
        SetCameraView(180f, 0f);
    }

    private void OnCameraLeftClick(object? sender, RoutedEventArgs e)
    {
        SetCameraView(-90f, 0f);
    }

    private void OnCameraTopClick(object? sender, RoutedEventArgs e)
    {
        SetCameraView(180f, -89f);
    }
}