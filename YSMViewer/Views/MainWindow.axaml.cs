using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using YSMViewer.ViewModels;

namespace YSMViewer.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        OpenButton.Click += OnOpenButtonClick;
    }

    private async void OnOpenButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var files = await StorageProvider.OpenFilePickerAsync(
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

        await vm.LoadFileAsync(files[0].Path.LocalPath);
    }
}
