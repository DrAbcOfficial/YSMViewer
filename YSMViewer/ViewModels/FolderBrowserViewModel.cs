using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;

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

        var ysmFiles = new List<string>();
        CollectYsmFiles(path, depth: 0, maxDepth: 2, ysmFiles);
        ysmFiles.Sort(StringComparer.OrdinalIgnoreCase);

        var items = new List<YsmFileItemViewModel>(ysmFiles.Count);

        await Task.Run(async () =>
        {
            foreach (var file in ysmFiles)
            {
                var relPath = file.StartsWith(path)
                    ? file[path.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    : file;

                var (displayName, complexity) = await ParseYsmFileAsync(file);

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var vm = new YsmFileItemViewModel(file, relPath, displayName, complexity);
                    vm.UpdateComplexityColor(0, 0);
                    items.Add(vm);
                    Files.Add(vm);
                });
            }
        });

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            IsScanning = false;
        });
    }

    private static async Task<(string displayName, int complexity)> ParseYsmFileAsync(string filePath)
    {
        string displayName = Path.GetFileName(filePath);
        int complexity = 0;

        try
        {
            var data = await File.ReadAllBytesAsync(filePath);
            var parser = YSMParser.Core.Parsers.YSMParserFactory.CreateFromBytes(data);
            parser.Parse();
            var resources = parser.GetResources();

            // Parse metadata from ysm.json / info.json (same as MainViewModel info panel)
            displayName = ParseMetaName(resources.YsmJson)
                         ?? ParseMetaName(resources.InfoJson)
                         ?? displayName;

            if (resources.Models.Count > 0)
            {
                var jsonStr = Encoding.UTF8.GetString(resources.Models[0].Data);
                using var doc = JsonDocument.Parse(jsonStr);
                var root = doc.RootElement;

                if (root.TryGetProperty("minecraft:geometry", out var geoms) && geoms.GetArrayLength() > 0)
                {
                    var geom = geoms[0];

                    // Fallback: use description identifier if metadata didn't provide name
                    if (displayName == Path.GetFileName(filePath)
                        && geom.TryGetProperty("description", out var desc)
                        && desc.TryGetProperty("identifier", out var id))
                    {
                        var identifier = id.GetString();
                        if (!string.IsNullOrEmpty(identifier) && identifier != "geometry.unknown")
                            displayName = identifier;
                    }

                    complexity = CountGeometryStats(geom);
                }
            }
        }
        catch
        {
        }

        return (displayName, complexity);
    }

    private static string? ParseMetaName(byte[]? jsonData)
    {
        if (jsonData is null or { Length: 0 }) return null;
        try
        {
            using var doc = JsonDocument.Parse(jsonData);
            var root = doc.RootElement;
            if (root.TryGetProperty("metadata", out var meta)
                && meta.TryGetProperty("name", out var name))
                return name.GetString();
            if (root.TryGetProperty("name", out var rootName))
                return rootName.GetString();
        }
        catch { }
        return null;
    }

    private static int CountGeometryStats(JsonElement geom)
    {
        int total = 0;
        if (geom.TryGetProperty("bones", out var bones))
        {
            total += bones.GetArrayLength();
            foreach (var bone in bones.EnumerateArray())
            {
                if (bone.TryGetProperty("cubes", out var cubes))
                    total += cubes.GetArrayLength();
            }
        }
        return total;
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
    private int _complexity;

    [ObservableProperty]
    private string _complexityText = "";

    [ObservableProperty]
    private Avalonia.Media.IBrush _complexityColor =
        new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.Gray);

    public YsmFileItemViewModel(string fullPath, string relativePath, string displayName, int complexity)
    {
        FullPath = fullPath;
        RelativePath = relativePath;
        Name = displayName;
        Complexity = complexity;
        ComplexityText = complexity > 0 ? $"{complexity:N0} elems" : "";
    }

    public void UpdateComplexityColor(int min, int max)
    {
        if (Complexity <= 0)
        {
            ComplexityColor = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.Gray);
            return;
        }

        double clamped = Math.Min(Complexity, 3000);
        double t = clamped / 3000.0;
        double hue = 120.0 * (1.0 - t);
        double sat = 0.85;
        double val = 0.65;

        var (r, g, b) = HsvToRgb(hue, sat, val);
        ComplexityColor = new Avalonia.Media.SolidColorBrush(
            Avalonia.Media.Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255)));
    }

    private static (double r, double g, double b) HsvToRgb(double h, double s, double v)
    {
        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
        double m = v - c;
        double r, g, b;

        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return (r + m, g + m, b + m);
    }
}