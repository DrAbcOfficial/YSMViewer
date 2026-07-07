using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using YSMViewer.ViewModels;

namespace YSMViewer.Views.Shared;

public partial class SidePanelContent : UserControl
{
    public static readonly StyledProperty<object?> PlatformAnimationContentProperty =
        AvaloniaProperty.Register<SidePanelContent, object?>(nameof(PlatformAnimationContent));

    public object? PlatformAnimationContent
    {
        get => GetValue(PlatformAnimationContentProperty);
        set => SetValue(PlatformAnimationContentProperty, value);
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public SidePanelContent()
    {
        InitializeComponent();
        PlatformAnimationContentProperty.Changed.AddClassHandler<SidePanelContent>((x, e) => x.OnPlatformAnimationContentChanged(e));
    }

    private void OnPlatformAnimationContentChanged(AvaloniaPropertyChangedEventArgs e)
    {
        PlatformAnimationContentHost.Content = e.NewValue;
    }

    private void OnToggleRightPanelClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        vm.IsRightPanelVisible = false;
    }

    private void OnShowAllComponentsClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ShowAllComponentsCommand.Execute(null);
    }

    private void OnHideAllComponentsClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.HideAllComponentsCommand.Execute(null);
    }

    private void OnExpandAllBonesClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ExpandAllBonesCommand.Execute(null);
        SetGeneratedTreeItemsExpanded(true);
    }

    private void OnCollapseAllBonesClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.CollapseAllBonesCommand.Execute(null);
        SetGeneratedTreeItemsExpanded(false);
    }

    private void SetGeneratedTreeItemsExpanded(bool expanded)
    {
        foreach (var item in this.GetVisualDescendants().OfType<TreeViewItem>())
            item.IsExpanded = expanded;
    }

    private void OnAnimationSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is string name)
            vm.SelectAnimation(name);
    }
}
