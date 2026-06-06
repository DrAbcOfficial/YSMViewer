using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;

namespace YSMViewer.ViewModels;

public sealed partial class FolderBrowserViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _folderPath = string.Empty;

    [ObservableProperty]
    private bool _hasFolder;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _folderName = string.Empty;

    [ObservableProperty]
    private YsmFileItemViewModel? _selectedFile;

    public ObservableCollection<YsmFileItemViewModel> Files { get; } = [];

    public event Func<string, Task>? FileSelected;
    public event Action<string>? ScanError;

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(
            Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow : null);
        if (topLevel?.StorageProvider is not { } sp) return;

        var folders = await sp.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "Select folder with YSM models",
            AllowMultiple = false,
        });

        if (folders is not { Count: > 0 }) return;

        var folder = folders[0];
        var path = folder.Path.LocalPath;
        if (string.IsNullOrEmpty(path)) return;

        await ScanFolderAsync(path);
    }

    public async Task ScanFolderAsync(string path)
    {
        FolderPath = path;
        FolderName = Path.GetFileName(path) ?? path;
        HasFolder = true;
        IsScanning = true;
        Files.Clear();

        await Task.Run(() =>
        {
            var ysmFiles = new List<string>();
            CollectYsmFiles(path, depth: 0, maxDepth: 2, ysmFiles);
            foreach (var file in ysmFiles.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var relPath = file.StartsWith(path) ? file[path.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : file;
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    Files.Add(new YsmFileItemViewModel(file, relPath));
                });
            }
        });

        IsScanning = false;
    }

    private void CollectYsmFiles(string dir, int depth, int maxDepth, List<string> results)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.ysm"))
                results.Add(file);

            if (depth < maxDepth)
            {
                foreach (var sub in Directory.EnumerateDirectories(dir))
                    CollectYsmFiles(sub, depth + 1, maxDepth, results);
            }
        }
        catch (UnauthorizedAccessException)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                ScanError?.Invoke($"Access denied: {dir}"));
        }
        catch (DirectoryNotFoundException)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                ScanError?.Invoke($"Directory not found: {dir}"));
        }
    }

    public async Task SelectFileAsync(YsmFileItemViewModel item)
    {
        SelectedFile = item;
        if (item is not null && FileSelected is not null)
            await FileSelected.Invoke(item.FullPath);
    }
}

public sealed partial class YsmFileItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _relativePath;

    [ObservableProperty]
    private string _fullPath;

    [ObservableProperty]
    private long _fileSize;

    public YsmFileItemViewModel(string fullPath, string relativePath)
    {
        FullPath = fullPath;
        RelativePath = relativePath;
        Name = Path.GetFileName(fullPath);

        try
        {
            FileSize = new FileInfo(fullPath).Length;
        }
        catch
        {
            FileSize = 0;
        }
    }

    public string SizeDisplay => FileSize < 1024
        ? $"{FileSize} B"
        : $"{FileSize / 1024.0:F1} KB";
}