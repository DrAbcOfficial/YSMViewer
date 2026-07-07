using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Text.Json;
using YSMViewer.Services;

namespace YSMViewer.ViewModels;

public enum FileSortColumn { None, Name, Complexity }

public sealed partial class FolderBrowserViewModel : ViewModelBase
{
    private static readonly ILogger Logger = YsmLog.For<FolderBrowserViewModel>();
    private readonly LocalizationService _localization;
    [ObservableProperty]
    public partial string FolderPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasFolder { get; set; }

    [ObservableProperty]
    public partial bool IsScanning { get; set; }

    [ObservableProperty]
    public partial string FolderName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial YsmFileItemViewModel? SelectedFile { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial FileSortColumn SortColumn { get; set; } = FileSortColumn.None;

    [ObservableProperty]
    public partial bool SortAscending { get; set; } = true;

    [ObservableProperty]
    public partial double ScanProgress { get; set; }

    [ObservableProperty]
    public partial string ScanProgressText { get; set; } = string.Empty;

    private readonly List<YsmFileItemViewModel> _allFiles = [];

    public ObservableCollection<YsmFileItemViewModel> FilteredFiles { get; } = [];

    public event Func<string, Task>? FileSelected;
    public event Action<string>? ScanError;

    public FolderBrowserViewModel(LocalizationService localizationService)
    {
        _localization = localizationService;
        RefreshLocStrings();
        _localization.CultureChanged += RefreshLocStrings;
    }

    private void RefreshLocStrings()
    {
        var r = Resources.Strings.ResourceManager;
        var c = _localization.CurrentCulture;
        LocOpenFolder = r.GetString("OpenFolder", c)!;
        LocSearchPrompt = r.GetString("SearchPrompt", c)!;
        LocName = r.GetString("Name", c)!;
        LocComplexityCol = r.GetString("ComplexityCol", c)!;
        LocEmptyFolder = r.GetString("EmptyFolder", c)!;
    }

    [ObservableProperty]
    public partial string LocOpenFolder { get; set; } = "";

    [ObservableProperty]
    public partial string LocSearchPrompt { get; set; } = "";

    [ObservableProperty]
    public partial string LocName { get; set; } = "";

    [ObservableProperty]
    public partial string LocComplexityCol { get; set; } = "";

    [ObservableProperty]
    public partial string LocEmptyFolder { get; set; } = "";



    partial void OnSearchTextChanged(string value)
    {
        RefreshFiltered();
    }

    partial void OnSortColumnChanged(FileSortColumn value)
    {
        RefreshFiltered();
    }

    partial void OnSortAscendingChanged(bool value)
    {
        RefreshFiltered();
    }

    [RelayCommand]
    private void ToggleSort(string columnName)
    {
        var col = columnName switch
        {
            "Name" => FileSortColumn.Name,
            "Complexity" => FileSortColumn.Complexity,
            _ => FileSortColumn.None,
        };

        if (SortColumn == col)
            SortAscending = !SortAscending;
        else
        {
            SortColumn = col;
            SortAscending = true;
        }
    }

    private void RefreshFiltered()
    {
        var query = _allFiles.AsEnumerable();
        var search = SearchText?.Trim() ?? "";

        if (search.Length > 0)
        {
            query = query.Where(f =>
                f.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                f.RelativePath.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        query = SortColumn switch
        {
            FileSortColumn.Name => SortAscending
                ? query.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                : query.OrderByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase),
            FileSortColumn.Complexity => SortAscending
                ? query.OrderBy(f => f.Complexity)
                : query.OrderByDescending(f => f.Complexity),
            _ => query,
        };

        FilteredFiles.Clear();
        foreach (var f in query)
            FilteredFiles.Add(f);
    }

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
        ScanProgress = 0;
        ScanProgressText = "";
        SearchText = "";
        SortColumn = FileSortColumn.None;
        _allFiles.Clear();
        FilteredFiles.Clear();

        var ysmFiles = new List<string>();
        CollectYsmFiles(path, depth: 0, maxDepth: 2, ysmFiles);
        ysmFiles.Sort(StringComparer.OrdinalIgnoreCase);

        int total = ysmFiles.Count;
        int processed = 0;
        var maxParallelism = Math.Max(2, Environment.ProcessorCount);
        var semaphore = new SemaphoreSlim(maxParallelism);
        var tasks = new List<Task>();

        foreach (var file in ysmFiles)
        {
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var relPath = file.StartsWith(path)
                        ? file[path.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        : file;

                    var (displayName, complexity) = await ParseYsmFileAsync(file);
                    var current = Interlocked.Increment(ref processed);

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        var vm = new YsmFileItemViewModel(file, relPath, displayName, complexity);
                        vm.UpdateComplexityColor();
                        _allFiles.Add(vm);
                        FilteredFiles.Add(vm);
                        ScanProgress = (double)current / total;
                        ScanProgressText = $"{current}/{total}";
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogDebug(ex, "Failed to parse YSM file in scan '{FilePath}'", file);
                    Interlocked.Increment(ref processed);
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);

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
            using var parser = YsmLoaderService.IsZipData(data)
                ? new ZipYsmParser(data)
                : YSMParser.Core.Parsers.YSMParserFactory.CreateFromBytes(data);

            var peekResult = parser.Peek();

            displayName = MinecraftFormatHelper.StripFormatting(
                ParseMetaName(peekResult.YsmJson)
                ?? ParseMetaName(peekResult.InfoJson)
                ?? peekResult.HeaderName
                ?? displayName);

            if (peekResult.Models is { Count: > 0 })
            {
                if (displayName == Path.GetFileName(filePath))
                {
                    var ident = peekResult.Models[0].Identifier;
                    if (!string.IsNullOrEmpty(ident) && ident != "geometry.unknown")
                        displayName = ident;
                }

                complexity = peekResult.Models.Sum(m => m.BoneCount + m.TotalCubeCount);
            }
            else
            {
                complexity = ComputeComplexityV1V2(parser, peekResult);
            }
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to parse YSM file metadata '{FilePath}'", filePath);
        }

        return (displayName, complexity);
    }

    private static int ComputeComplexityV1V2(
        YSMParser.Core.Parsers.YSMParser parser,
        YSMParser.Core.Parsers.YsmPeekResult peekResult)
    {
        if (peekResult.ResourceNames is not { Count: > 0 })
            return 0;

        parser.Parse();
        var resources = parser.GetResources();
        int complexity = 0;

        foreach (var model in resources.Models)
        {
            try
            {
                var geoData = System.Text.Json.JsonSerializer.Deserialize(
                    model.Data, Models.YsmJsonContext.Default.MinecraftGeometryFile);
                if (geoData?.Geometries is not null)
                {
                    foreach (var geo in geoData.Geometries)
                    {
                        complexity += geo.Bones?.Count ?? 0;
                        complexity += geo.Bones?.Sum(b => b.Cubes?.Count ?? 0) ?? 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to parse geometry for complexity calculation");
            }
        }

        return complexity;
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
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to parse meta name from JSON");
        }
        return null;
    }

    private void CollectYsmFiles(string dir, int depth, int maxDepth, List<string> results)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.ysm"))
                results.Add(file);
            foreach (var file in Directory.EnumerateFiles(dir, "*.zip"))
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

public sealed partial class YsmFileItemViewModel(string fullPath, string relativePath, string displayName, int complexity) : ViewModelBase
{
    [ObservableProperty]
    public partial string Name { get; set; } = displayName;
    [ObservableProperty]
    public partial string RelativePath { get; set; } = relativePath;
    [ObservableProperty]
    public partial string FullPath { get; set; } = fullPath;

    [ObservableProperty]
    public partial int Complexity { get; set; } = complexity;

    [ObservableProperty]
    public partial string ComplexityText { get; set; } = complexity > 0 ? $"{complexity:N0} elems" : "";

    [ObservableProperty]
    public partial Avalonia.Media.IBrush ComplexityColor { get; set; } =
        new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.Gray);

    public void UpdateComplexityColor()
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