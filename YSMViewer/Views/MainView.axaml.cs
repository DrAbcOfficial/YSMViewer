using Aura3D.Avalonia;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Svg.Skia;
using Svg.Model;
using System.Numerics;
using YSMViewer.Rendering;
using YSMViewer.Rendering.Aura3D;
using YSMViewer.Services;
using YSMViewer.ViewModels;

namespace YSMViewer.Views;

public partial class MainView : UserControl
{
    private SphericalGizmo? _gizmo;
    private bool _isDragging;
    private bool _gizmoIsDragging;
    private Avalonia.Point _lastMousePos;
    private Avalonia.Point _gizmoLastPos;

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
            ToolTip.SetTip(btn, mode switch
            {
                AppThemeMode.Dark => "Switch to System theme",
                AppThemeMode.System => "Switch to Light mode",
                _ => "Switch to Dark mode",
            });
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
        catch { }
    }

    private void UpdateSceneAppearance()
    {
        var rgba = ThemeService.Instance.GetViewportBackgroundColor();
        if (DataContext is MainViewModel vm)
            vm.Renderer.SetTheme(new RenderTheme(rgba[1], rgba[2], rgba[3], rgba[0],
                ThemeService.Instance.IsDarkTheme()));
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

            try { await vm.LoadFileAsync(path); }
            catch (Exception ex) { vm.SetError(ex); }
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
            catch (Exception ex) { vm.SetError(ex); }
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

        e.DragEffects = e.DataTransfer.Formats.Contains(DataFormat.File)
            ? DragDropEffects.Copy : DragDropEffects.None;
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
        UpdateSceneAppearance();

        if (DataContext is MainViewModel vm)
            _ = vm.LoadStartupFileIfNeeded();
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
        if (_isDragging)
        {
            if (DataContext is not MainViewModel vm) return;
            if (vm.Renderer is not Aura3DRenderer aura) return;

            var pos = e.GetPosition(this);
            float dx = (float)(pos.X - _lastMousePos.X);
            float dy = (float)(pos.Y - _lastMousePos.Y);
            _lastMousePos = pos;

            aura.OrbitCamera(dx * 0.3f, dy * 0.3f);
            SyncGizmoCamera();
        }
        else if (_gizmoIsDragging)
        {
            if (DataContext is not MainViewModel vm) return;
            if (vm.Renderer is not Aura3DRenderer aura) return;

            var pos = e.GetPosition(this);
            float dx = (float)(pos.X - _gizmoLastPos.X);
            float dy = (float)(pos.Y - _gizmoLastPos.Y);
            _gizmoLastPos = pos;

            aura.OrbitCamera(dx * 0.3f, dy * 0.3f);
            SyncGizmoCamera();
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.Renderer is Aura3DRenderer aura)
            aura.ZoomCamera((float)e.Delta.Y);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e) { }

    private void OnDismissErrorClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.HasError = false;
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
            vm.SelectAnimation(name);
    }

    private void OnShowAllComponentsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            foreach (var comp in vm.Components)
                vm.SetComponentVisible(comp.ComponentId, true);
        }
    }

    private void OnHideAllComponentsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            foreach (var comp in vm.Components)
                vm.SetComponentVisible(comp.ComponentId, false);
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

    private void OnCameraFrontClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.Renderer.SetCameraView(RenderCameraView.Front);
    }

    private void OnCameraLeftClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.Renderer.SetCameraView(RenderCameraView.Side);
    }

    private void OnCameraTopClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.Renderer.SetCameraView(RenderCameraView.Top);
    }

    private void OnGizmoSetupPipeline(object? sender, RoutedEventArgs args)
    {
        if (sender is Aura3DView view)
            view.CreateRenderPipeline = s => new NoLightPipeline(s);
    }

    private void OnGizmoSceneInitialized(object sender, InitializedRoutedEventArgs args)
    {
        var view = (Aura3DView)sender;
        var scene = args.Scene;

        try
        {
            var rgba = ThemeService.Instance.GetViewportBackgroundColor();
            scene.Background = Texture.CreateFromColor(
                System.Drawing.Color.FromArgb(rgba[0], rgba[1], rgba[2], rgba[3]));
            scene.RenderPipeline.EnableFrustumCulling = true;

            var camera = view.MainCamera;
            camera.FieldOfView = 40f;
            camera.NearPlane = 0.01f;
            camera.FarPlane = 100f;

            _gizmo = new SphericalGizmo();
            view.AddNode(_gizmo);

            SyncGizmoCamera();
        }
        catch { }
    }

    private void SyncGizmoCamera()
    {
        if (GizmoView.Scene is null) return;
        if (DataContext is not MainViewModel vm) return;
        if (vm.Renderer is not Aura3DRenderer aura) return;

        const float gizmoCamDist = 2.5f;
        float pitchRad = aura.CameraPitch * MathF.PI / 180f;
        float yawRad = aura.CameraYaw * MathF.PI / 180f;

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
}
