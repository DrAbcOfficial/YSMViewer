using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using YSMViewer.ViewModels;

namespace YSMViewer.Views;

public partial class FolderBrowserView : UserControl
{
    public FolderBrowserView()
    {
        InitializeComponent();
    }

    private async void OnFileDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not FolderBrowserViewModel vm) return;
        if (vm.SelectedFile is not null)
        {
            await vm.SelectFileAsync(vm.SelectedFile);
        }
    }
}