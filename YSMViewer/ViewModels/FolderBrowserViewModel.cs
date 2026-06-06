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

        var results = new List<YsmFileItemViewModel>(ysmFiles.Count);
        var complexityValues = new List<int>();

        await Task.Run(async () =>
        {
            foreach (var file in ysmFiles)
            {
                var relPath = file.StartsWith(path)
                    ? file[path.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    : file;

                var item = await ParseYsmFileAsync(file, relPath);
                results.Add(item);
                complexityValues.Add(item.Complexity);

                Avalonia.Threading.Dispatcher.UIThread.Post(() => Files.Add(item));
            }
        });

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (complexityValues.Count > 0)
            {
                int min = complexityValues.Min();
                int max = complexityValues.Max();
                if (min == max) max = min + 1;

                foreach (var item in results)
                    item.UpdateComplexityColor(min, max);
            }
            IsScanning = false;
        });
    }

    private static async Task<YsmFileItemViewModel> ParseYsmFileAsync(string filePath, string relativePath)
    {
        string displayName = Path.GetFileName(filePath);
        int complexity = 0;

        try
        {
            var data = await File.ReadAllBytesAsync(filePath);
            var parser = YSMParser.Core.Parsers.YSMParserFactory.CreateFromBytes(data);
            parser.Parse();
            var resources = parser.GetResources();

            if (resources.Models.Count > 0)
            {
                var jsonData = resources.Models[0].Data;
                var jsonStr = Encoding.UTF8.GetString(jsonData);

                using var doc = JsonDocument.Parse(jsonStr);
                var root = doc.RootElement;

                if (root.TryGetProperty("minecraft:geometry", out var geoms) && geoms.GetArrayLength() > 0)
                {
                    var geom = geoms[0];
                    if (geom.TryGetProperty("description", out var desc))
                    {
                        if (desc.TryGetProperty("ysm_extra_info", out var extra)
                            && extra.TryGetProperty("name", out var metaName))
                        {
                            displayName = metaName.GetString() ?? displayName;
                        }
                        else if (desc.TryGetProperty("identifier", out var id))
                        {
                            displayName = id.GetString() ?? displayName;
                        }
                    }

                    complexity = CountGeometryStats(geom);
                }
            }
        }
        catch
        {
            // keep default displayName (filename) and complexity 0
        }

        return new YsmFileItemViewModel(filePath, relativePath, displayName, complexity);
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
        if (Complexity <= 0 || max <= min)
        {
            ComplexityColor = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.Gray);
            return;
        }

        double t = (double)(Complexity - min) / (max - min);
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