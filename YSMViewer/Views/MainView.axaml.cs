using Aura3D.Avalonia;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using System.Numerics;
using YSMViewer.Rendering;
using YSMViewer.Services;
using YSMViewer.ViewModels;

namespace YSMViewer.Views;

public partial class MainView : UserControl
{
    private SphericalGizmo? _gizmo;
    private bool _isDragging;
    private bool _isPanning;
    private bool _gizmoIsDragging;
    private Point _lastMousePos;
    private Point _gizmoLastPos;

    public MainView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerMoved += OnPointerMoved;
        PointerWheelChanged += OnPointerWheelChanged;

        DragDrop.AddDropHandler(this, OnDrop);
        DragDrop.AddDragOverHandler(this, OnDragOverHandler);
        DragDrop.AddDragEnterHandler(this, OnDragEnterHandler);
        DragDrop.AddDragLeaveHandler(this, OnDragLeaveHandler);
    }

    private async void OnOpenButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open YSM Model",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("YSM/ZIP Models") { Patterns = ["*.ysm", "*.zip"] }],
        });
        if (files is not { Count: > 0 }) return;
        await using var stream = await files[0].OpenReadAsync();
        using var ms = new System.IO.MemoryStream();
        await stream.CopyToAsync(ms);
        await vm.LoadFromBytesAsync(ms.ToArray());
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        UpdateSceneAppearance();

        if (DataContext is MainViewModel vm)
            _ = vm.LoadStartupFileIfNeeded();
    }

    private void UpdateSceneAppearance()
    {
        var rgba = ThemeService.Instance.GetViewportBackgroundColor();
        if (DataContext is MainViewModel vm)
            vm.Renderer.SetTheme(new RenderTheme(rgba[1], rgba[2], rgba[3], rgba[0],
                ThemeService.Instance.IsDarkTheme()));
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

            if (!path.EndsWith(".ysm", StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;

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

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;

        if (props.IsRightButtonPressed)
        {
            _isDragging = true;
            _lastMousePos = e.GetPosition(this);
            e.Handled = true;
        }
        else if (props.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _lastMousePos = e.GetPosition(this);
            e.Handled = true;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
        _isPanning = false;
        _gizmoIsDragging = false;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging)
        {
            if (DataContext is not MainViewModel vm) return;
            if (vm.Renderer is not IInteractiveRenderer interactive) return;

            var pos = e.GetPosition(this);
            float dx = (float)(pos.X - _lastMousePos.X);
            float dy = (float)(pos.Y - _lastMousePos.Y);
            _lastMousePos = pos;

            interactive.OrbitCamera(dx * 0.3f, dy * 0.3f);
            SyncGizmoCamera();
        }
        else if (_isPanning)
        {
            if (DataContext is not MainViewModel vm) return;
            if (vm.Renderer is not IInteractiveRenderer interactive) return;

            var pos = e.GetPosition(this);
            float dx = (float)(pos.X - _lastMousePos.X);
            float dy = (float)(pos.Y - _lastMousePos.Y);
            _lastMousePos = pos;

            interactive.PanCamera(dx, dy);
        }
        else if (_gizmoIsDragging)
        {
            if (DataContext is not MainViewModel vm) return;
            if (vm.Renderer is not IInteractiveRenderer interactive) return;

            var pos = e.GetPosition(this);
            float dx = (float)(pos.X - _gizmoLastPos.X);
            float dy = (float)(pos.Y - _gizmoLastPos.Y);
            _gizmoLastPos = pos;

            interactive.OrbitCamera(dx * 0.3f, dy * 0.3f);
            SyncGizmoCamera();
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.Renderer is IInteractiveRenderer interactive)
            interactive.ZoomCamera((float)e.Delta.Y);
    }

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
                var data = new DataTransfer();
                data.Add(DataTransferItem.CreateText(vm.ErrorDetail));
                await topLevel.Clipboard.SetDataAsync(data);
                vm.Notifications.Show("Copied to clipboard", NotificationType.Info, 2000);
            }
        }
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

    private void OnAnimationSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm && e.AddedItems.Count > 0 && e.AddedItems[0] is string name)
            vm.SelectAnimation(name);
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
        if (vm.Renderer is not IInteractiveRenderer interactive) return;

        const float gizmoCamDist = 2.5f;
        var (pitch, yaw) = interactive.GetCameraOrbit();
        float pitchRad = pitch * MathF.PI / 180f;
        float yawRad = yaw * MathF.PI / 180f;

        float x = gizmoCamDist * MathF.Cos(pitchRad) * MathF.Sin(yawRad);
        float y = gizmoCamDist * MathF.Sin(pitchRad);
        float z = gizmoCamDist * MathF.Cos(pitchRad) * MathF.Cos(yawRad);

        var cam = GizmoView.MainCamera;
        cam.Position = new Vector3(x, -y, z);
        cam.LookAt(Vector3.Zero);
    }

    private void OnGizmoPointerEntered(object? sender, PointerEventArgs e)
    {
        GizmoBorder.Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0));
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
