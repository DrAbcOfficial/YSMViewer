using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using System.Runtime.Versioning;
using YSMViewer.Rendering;
using YSMViewer.Rendering.ThreeJs;
using YSMViewer.Services;
using YSMViewer.ViewModels;

namespace YSMViewer.Browser.Views;

[SupportedOSPlatform("browser")]
public partial class BrowserMainView : UserControl
{
    private double _rightPanelSavedWidth = 300;
    private const double MobileBreakpoint = 768;

    public BrowserMainView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;

        DragDrop.AddDropHandler(this, OnDrop);
        DragDrop.AddDragOverHandler(this, OnDragOverHandler);
        DragDrop.AddDragEnterHandler(this, OnDragEnterHandler);
        DragDrop.AddDragLeaveHandler(this, OnDragLeaveHandler);

        ThreeJsInterop.RestoreButtonClicked += OnRestoreButtonFromHtml;

        ThemeService.Instance.ModeChanged += _ => UpdateSceneAppearance();
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
            UpdateMobileState();
            SyncButtonVisibility();
            _ = vm.LoadStartupFileIfNeeded();
        }
    }

    private void UpdateSceneAppearance()
    {
        var rgba = ThemeService.Instance.GetViewportBackgroundColor();
        if (DataContext is MainViewModel vm)
            vm.Renderer.SetTheme(new RenderTheme(rgba[1], rgba[2], rgba[3], rgba[0],
                ThemeService.Instance.IsDarkTheme()));
    }

    private void SyncButtonVisibility()
    {
        if (DataContext is not MainViewModel vm) return;
        try
        {
            if (vm.IsRightPanelVisible)
                ThreeJsInterop.HideRestoreButton();
            else
                ThreeJsInterop.ShowRestoreButton();
        }
        catch { }
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateMobileState();
    }

    private void UpdateMobileState()
    {
        if (DataContext is not MainViewModel vm) return;

        var width = Bounds.Width;
        var isMobile = width > 0 && width < MobileBreakpoint;
        if (isMobile != vm.IsMobileView)
        {
            vm.IsMobileView = isMobile;
            if (isMobile)
            {
                vm.IsRightPanelVisible = false;
                if (BrowserMainContentGrid.ColumnDefinitions.Count > 2)
                    BrowserMainContentGrid.ColumnDefinitions[2].Width = new GridLength(0);
            }
        }
        SyncButtonVisibility();
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;

        if (DataContext is not MainViewModel vm) return;
        if (!e.DataTransfer.Formats.Contains(DataFormat.File)) return;

        var files = e.DataTransfer.TryGetFiles();
        if (files is null) return;

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

    private void OnCloseBannerClick(object? sender, RoutedEventArgs e)
    {
        var banner = this.FindControl<Border>("BrowserBanner");
        banner?.IsVisible = false;
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

    private void OnBrowserToggleRightPanelClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var col = BrowserMainContentGrid.ColumnDefinitions[2];
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

    private void OnBrowserShowAllComponentsClick(object? sender, RoutedEventArgs e)
    {
        OnShowAllComponentsClick(sender, e);
    }

    private void OnBrowserHideAllComponentsClick(object? sender, RoutedEventArgs e)
    {
        OnHideAllComponentsClick(sender, e);
    }

    private void OnRestoreButtonFromHtml()
    {
        OnBrowserToggleRightPanelClick(null, null!);
    }

}
