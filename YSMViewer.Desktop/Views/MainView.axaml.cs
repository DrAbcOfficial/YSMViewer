using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using YSMViewer.Rendering;
using YSMViewer.Services;
using YSMViewer.ViewModels;

namespace YSMViewer.Desktop.Views;

public partial class MainView : UserControl
{
    private static ThemeService ThemeSvc => App.Services.GetRequiredService<ThemeService>();
    private bool _isDragging;
    private bool _isPanning;
    private bool _isZooming;
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
            FileTypeFilter = [new FilePickerFileType("YSM/ZIP Models")
            {
                Patterns = ["*.ysm", "*.zip"],
                MimeTypes = ["application/vnd.ysm.model+encrypted", "application/zip", "application/x-zip-compressed"],
            }],
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
        {
            SetupGizmo(vm);
            _ = vm.LoadStartupFileIfNeeded();
        }
    }

    private void SetupGizmo(MainViewModel vm)
    {
        if (vm.Renderer is IInteractiveRenderer interactive && interactive.GizmoControl is { } gizmoControl)
        {
            var gizmoHost = this.FindControl<ContentControl>("GizmoHost");
            if (gizmoHost is not null)
            {
                gizmoHost.Content = gizmoControl;
                gizmoHost.IsVisible = true;
            }
        }
    }

    private void UpdateSceneAppearance()
    {
        var rgba = ThemeSvc.GetViewportBackgroundColor();
        if (DataContext is MainViewModel vm)
            vm.Renderer.SetTheme(new RenderTheme(rgba[1], rgba[2], rgba[3], rgba[0],
                ThemeSvc.IsDarkTheme()));
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
            overlay?.SetCurrentValue(Border.IsVisibleProperty, true);
        }
    }

    private void OnDragLeaveHandler(object? sender, DragEventArgs e)
    {
        var overlay = this.FindControl<Border>("DragOverlay");
        overlay?.SetCurrentValue(Border.IsVisibleProperty, false);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;

        if (props.IsLeftButtonPressed)
        {
            _isDragging = true;
            _lastMousePos = e.GetPosition(this);
            e.Handled = true;
        }
        else if (props.IsRightButtonPressed)
        {
            _isPanning = true;
            _lastMousePos = e.GetPosition(this);
            e.Handled = true;
        }
        else if (props.IsMiddleButtonPressed)
        {
            _isZooming = true;
            _lastMousePos = e.GetPosition(this);
            e.Handled = true;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
        _isPanning = false;
        _isZooming = false;
        _gizmoIsDragging = false;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (vm.Renderer is not IInteractiveRenderer interactive) return;

        if (_isDragging)
        {
            var pos = e.GetPosition(this);
            float dx = (float)(pos.X - _lastMousePos.X);
            float dy = (float)(pos.Y - _lastMousePos.Y);
            _lastMousePos = pos;

            interactive.OrbitCamera(dx * 0.3f, dy * 0.3f);
            SyncGizmoCamera(interactive);
        }
        else if (_isPanning)
        {
            var pos = e.GetPosition(this);
            float dx = (float)(pos.X - _lastMousePos.X);
            float dy = (float)(pos.Y - _lastMousePos.Y);
            _lastMousePos = pos;

            interactive.PanCamera(dx, dy);
        }
        else if (_isZooming)
        {
            var pos = e.GetPosition(this);
            float dy = (float)(pos.Y - _lastMousePos.Y);
            _lastMousePos = pos;

            interactive.ZoomCamera(dy * 0.05f);
        }
        else if (_gizmoIsDragging)
        {
            var pos = e.GetPosition(this);
            float dx = (float)(pos.X - _gizmoLastPos.X);
            float dy = (float)(pos.Y - _gizmoLastPos.Y);
            _gizmoLastPos = pos;

            interactive.OrbitCamera(dx * 0.3f, dy * 0.3f);
            SyncGizmoCamera(interactive);
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
        {
            vm.ExpandAllBones();
            SetGeneratedTreeItemsExpanded(true);
        }
    }

    private void OnCollapseAllBonesClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.CollapseAllBones();
            SetGeneratedTreeItemsExpanded(false);
        }
    }

    private void SetGeneratedTreeItemsExpanded(bool expanded)
    {
        foreach (var item in this.GetVisualDescendants().OfType<TreeViewItem>())
            item.IsExpanded = expanded;
    }

    private void OnAnimationSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm && e.AddedItems.Count > 0 && e.AddedItems[0] is string name)
            vm.SelectAnimation(name);
    }

    private static void SyncGizmoCamera(IInteractiveRenderer interactive)
    {
        interactive.SyncGizmo();
    }

    private void OnGizmoPointerEntered(object? sender, PointerEventArgs e)
    {
        var gizmoBorder = this.FindControl<Border>("GizmoBorder");
        gizmoBorder?.Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0));
    }

    private void OnGizmoPointerExited(object? sender, PointerEventArgs e)
    {
        var gizmoBorder = this.FindControl<Border>("GizmoBorder");
        gizmoBorder?.Background = Brushes.Transparent;
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

    private double _leftPanelSavedWidth = 280;
    private double _rightPanelSavedWidth = 300;

    private void OnToggleLeftPanelClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var col = MainContentGrid.ColumnDefinitions[0];
        if (vm.IsLeftPanelVisible)
        {
            if (col.Width.IsAbsolute)
                _leftPanelSavedWidth = col.Width.Value;
            col.Width = new GridLength(0);
            vm.IsLeftPanelVisible = false;
        }
        else
        {
            col.Width = new GridLength(_leftPanelSavedWidth);
            vm.IsLeftPanelVisible = true;
        }
    }

    private void OnToggleRightPanelClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var col = MainContentGrid.ColumnDefinitions[4];
        if (vm.IsRightPanelVisible)
        {
            if (col.Width.IsAbsolute)
                _rightPanelSavedWidth = col.Width.Value;
            col.Width = new GridLength(0);
            vm.IsRightPanelVisible = false;
        }
        else
        {
            col.Width = new GridLength(_rightPanelSavedWidth);
            vm.IsRightPanelVisible = true;
        }
    }
}
