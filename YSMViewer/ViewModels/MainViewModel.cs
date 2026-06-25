using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Text.Json;
using YSMViewer.Models.Document;
using YSMViewer.Rendering;
using YSMViewer.Services;

namespace YSMViewer.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    public IRenderer Renderer { get; }

    public FolderBrowserViewModel FolderBrowser { get; }

    public NotificationService Notifications { get; } = new();

    public bool IsDesktop { get; } = Avalonia.Application.Current?.ApplicationLifetime
        is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;

    public bool SupportsAnimation => Renderer.Capabilities.SupportsAnimation;
    public bool SupportsComponentVisibility => Renderer.Capabilities.SupportsComponentVisibility;
    public bool SupportsBoneVisibility => Renderer.Capabilities.SupportsBoneVisibility;
    public bool SupportsAutoRotation => Renderer.Capabilities.SupportsAutoRotation;
    public bool SupportsAudio => Renderer.Capabilities.SupportsAudio;
    public bool IsBrowserReadOnly => !SupportsComponentVisibility && !SupportsBoneVisibility;

    public ObservableCollection<string> AnimationNames { get; } = [];
    public ObservableCollection<ComponentViewModel> Components { get; } = [];
    public ObservableCollection<ComponentBoneGroupViewModel> BoneGroups { get; } = [];
    public ObservableCollection<TextureItemViewModel> TextureItems { get; } = [];
    public ObservableCollection<SoundItemViewModel> SoundItems { get; } = [];

    private YsmModelDocument? _currentDocument;

    public string? StartupFilePath { get; set; }
    public string? StartupFileUrl { get; set; }

    [ObservableProperty]
    public partial bool HasAnimationController { get; set; }

    [ObservableProperty]
    public partial bool UseAnimationController { get; set; }

    partial void OnUseAnimationControllerChanged(bool value)
    {
        if (Renderer is IAnimationRenderer animRenderer)
            animRenderer.UseAnimationController = value;
    }

    public MainViewModel(IRenderer renderer)
    {
        Renderer = renderer;
        FolderBrowser = new FolderBrowserViewModel();
        FolderBrowser.FileSelected += OnFileSelectedFromBrowser;
        FolderBrowser.ScanError += OnScanError;
        RefreshLocalizedStrings();
        LocalizationService.Instance.CultureChanged += RefreshLocalizedStrings;
    }

    private void RefreshLocalizedStrings()
    {
        var L = Resources.Strings.ResourceManager;
        var culture = LocalizationService.Instance.CurrentCulture;
        LocOpenFile = L.GetString("OpenFile", culture)!;
        LocStatusReady = L.GetString("ReadyStatus", culture)!;
        LocToggleTheme = L.GetString("ToggleTheme", culture)!;
        LocSwitchLang = L.GetString("SwitchLanguage", culture)!;
        LocViewGitHub = L.GetString("ViewOnGitHub", culture)!;
        LocInfo = L.GetString("Info", culture)!;
        LocComponents = L.GetString("Components", culture)!;
        LocBones = L.GetString("Bones", culture)!;
        LocTextures = L.GetString("Textures", culture)!;
        LocSounds = L.GetString("Sounds", culture)!;
        LocAnimations = L.GetString("Animations", culture)!;
        LocShowAll = L.GetString("ShowAll", culture)!;
        LocHideAll = L.GetString("HideAll", culture)!;
        LocExpandAll = L.GetString("ExpandAll", culture)!;
        LocCollapseAll = L.GetString("CollapseAll", culture)!;
        LocEmptyState = L.GetString("EmptyState", culture)!;
        LocErrorTitle = L.GetString("Error", culture)!;
        LocDismiss = L.GetString("Dismiss", culture)!;
        LocCopy = L.GetString("Copy", culture)!;
        LocDropHint = L.GetString("DropHint", culture)!;
        LocDropHintExt = L.GetString("DropHintExt", culture)!;
        LocFrontView = L.GetString("FrontView", culture)!;
        LocLeftView = L.GetString("LeftView", culture)!;
        LocTopView = L.GetString("TopView", culture)!;
        LocStopAnim = L.GetString("StopAnim", culture)!;
        LocOpenFolder = L.GetString("OpenFolder", culture)!;
        LocSearchPrompt = L.GetString("SearchPrompt", culture)!;
        LocEmptyFolder = L.GetString("EmptyFolder", culture)!;
        StatusText = LocStatusReady;
    }

    [ObservableProperty]
    public partial string LocOpenFile { get; set; } = "";

    [ObservableProperty]
    public partial string LocStatusReady { get; set; } = "";

    [ObservableProperty]
    public partial string LocToggleTheme { get; set; } = "";

    [ObservableProperty]
    public partial string LocSwitchLang { get; set; } = "";

    [ObservableProperty]
    public partial string LocViewGitHub { get; set; } = "";

    [ObservableProperty]
    public partial string LocInfo { get; set; } = "";

    [ObservableProperty]
    public partial string LocComponents { get; set; } = "";

    [ObservableProperty]
    public partial string LocBones { get; set; } = "";

    [ObservableProperty]
    public partial string LocTextures { get; set; } = "";

    [ObservableProperty]
    public partial string LocSounds { get; set; } = "";

    [ObservableProperty]
    public partial string LocAnimations { get; set; } = "";

    [ObservableProperty]
    public partial string LocShowAll { get; set; } = "";

    [ObservableProperty]
    public partial string LocHideAll { get; set; } = "";

    [ObservableProperty]
    public partial string LocExpandAll { get; set; } = "";

    [ObservableProperty]
    public partial string LocCollapseAll { get; set; } = "";

    [ObservableProperty]
    public partial string LocEmptyState { get; set; } = "";

    [ObservableProperty]
    public partial string LocErrorTitle { get; set; } = "";

    [ObservableProperty]
    public partial string LocDismiss { get; set; } = "";

    [ObservableProperty]
    public partial string LocCopy { get; set; } = "";

    [ObservableProperty]
    public partial string LocDropHint { get; set; } = "";

    [ObservableProperty]
    public partial string LocDropHintExt { get; set; } = "";

    [ObservableProperty]
    public partial string LocFrontView { get; set; } = "";

    [ObservableProperty]
    public partial string LocLeftView { get; set; } = "";

    [ObservableProperty]
    public partial string LocTopView { get; set; } = "";

    [ObservableProperty]
    public partial string LocStopAnim { get; set; } = "";

    [ObservableProperty]
    public partial string LocOpenFolder { get; set; } = "";

    [ObservableProperty]
    public partial string LocSearchPrompt { get; set; } = "";

    [ObservableProperty]
    public partial string LocEmptyFolder { get; set; } = "";

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Ready. Open a .ysm file to begin.";

    [ObservableProperty]
    public partial string ModelName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int ModelVersion { get; set; }

    [ObservableProperty]
    public partial bool HasModel { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorDetail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasAnimations { get; set; }

    [ObservableProperty]
    public partial string CurrentAnimationName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial float AnimationProgress { get; set; }

    [ObservableProperty]
    public partial bool IsAnimating { get; set; }

    [ObservableProperty]
    public partial string AnimationTimeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CanPreviousAnimation { get; set; }

    [ObservableProperty]
    public partial bool CanNextAnimation { get; set; }

    [ObservableProperty]
    public partial string ModelDisplayName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ModelAuthors { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ModelLicense { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ModelTips { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsFreeModel { get; set; }

    [ObservableProperty]
    public partial bool HasTextures { get; set; }

    [ObservableProperty]
    public partial bool HasSounds { get; set; }

    [ObservableProperty]
    public partial bool HasModelAuthors { get; set; }

    [ObservableProperty]
    public partial bool HasModelLicense { get; set; }

    [ObservableProperty]
    public partial bool HasModelTips { get; set; }

    [ObservableProperty]
    public partial TextureItemViewModel? SelectedTexture { get; set; }

    [ObservableProperty]
    public partial MolangPanelViewModel? MolangPanel { get; set; }

    [ObservableProperty]
    public partial bool HasMolangVariables { get; set; }

    // Panel layout
    [ObservableProperty]
    public partial bool IsLeftPanelVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsRightPanelVisible { get; set; } = true;

    [ObservableProperty]
    public partial double LeftPanelWidth { get; set; } = 280;

    [ObservableProperty]
    public partial double RightPanelWidth { get; set; } = 300;

    [ObservableProperty]
    public partial double RightPanelPreviousWidth { get; set; } = 300;

    [ObservableProperty]
    public partial bool IsMobileView { get; set; }

    [RelayCommand]
    private void ToggleLeftPanel()
    {
        IsLeftPanelVisible = !IsLeftPanelVisible;
    }

    [RelayCommand]
    private void ToggleRightPanel()
    {
        if (IsRightPanelVisible)
            RightPanelPreviousWidth = RightPanelWidth;
        IsRightPanelVisible = !IsRightPanelVisible;
    }

    private async Task OnFileSelectedFromBrowser(string filePath)
    {
        await LoadFileAsync(filePath);
    }

    private void OnScanError(string message)
    {
        Notifications.Show(message, NotificationType.Warning);
    }

    public async Task LoadStartupFileIfNeeded()
    {
        if (StartupFileUrl is { Length: > 0 } && !HasModel && !IsLoading)
        {
            await LoadFromUrlAsync(StartupFileUrl);
        }
        else if (StartupFilePath is { Length: > 0 } && !HasModel && !IsLoading)
        {
            await LoadFileAsync(StartupFilePath);
        }
    }

    public void SetError(Exception ex)
    {
        HasError = true;
        var detail = new System.Text.StringBuilder();
        detail.AppendLine($"Type: {ex.GetType().FullName}");
        detail.AppendLine($"Message: {ex.Message}");
        detail.AppendLine($"Stack:");
        detail.AppendLine(ex.StackTrace ?? "(null)");

        var inner = ex.InnerException;
        while (inner is not null)
        {
            detail.AppendLine();
            detail.AppendLine($"Inner: {inner.GetType().FullName}");
            detail.AppendLine($"Message: {inner.Message}");
            detail.AppendLine($"Stack:");
            detail.AppendLine(inner.StackTrace ?? "(null)");
            inner = inner.InnerException;
        }

        ErrorDetail = detail.ToString();
        StatusText = $"Error: {ex.Message}";
    }

    public async Task LoadFileAsync(string filePath)
    {
        HasError = false;
        ErrorDetail = string.Empty;
        ResetAnimationState();

        try
        {
            IsLoading = true;
            StatusText = "Parsing YSM...";

            var document = await Task.Run(() => YsmLoaderService.LoadDocumentFromFile(filePath));
            PopulateModelData(document);
        }
        catch (Exception ex)
        {
            SetError(ex);
            HasModel = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadFromBytesAsync(byte[] data)
    {
        HasError = false;
        ErrorDetail = string.Empty;
        ResetAnimationState();

        try
        {
            IsLoading = true;
            StatusText = "Parsing YSM...";

            YsmModelDocument? document = null;
            await Task.Run(() => { document = YsmLoaderService.LoadDocumentFromBytes(data); });
            PopulateModelData(document!);
        }
        catch (Exception ex)
        {
            SetError(ex);
            HasModel = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadFromUrlAsync(string url)
    {
        HasError = false;
        ErrorDetail = string.Empty;

        try
        {
            IsLoading = true;
            StatusText = "Downloading model...";

            using var client = new HttpClient();
            var bytes = await client.GetByteArrayAsync(url);
            await LoadFromBytesAsync(bytes);
        }
        catch (Exception ex)
        {
            SetError(ex);
            HasModel = false;
            IsLoading = false;
        }
    }

    private void PopulateModelData(YsmModelDocument document)
    {
        StatusText = "Building scene...";
        ModelName = document.Info.Name;
        ModelVersion = document.Info.Version;
        HasModel = true;
        _currentDocument = document;

        ModelDisplayName = MinecraftFormatHelper.StripFormatting(
            !string.IsNullOrEmpty(document.Info.DisplayName) && document.Info.DisplayName != "Unknown"
                ? document.Info.DisplayName : document.Info.Name);
        ModelAuthors = document.Info.Authors ?? string.Empty;
        ModelLicense = document.Info.License;
        IsFreeModel = document.Info.IsFree;
        ModelTips = document.Info.Tips;

        HasModelAuthors = !string.IsNullOrEmpty(ModelAuthors);
        HasModelLicense = !string.IsNullOrEmpty(ModelLicense);
        HasModelTips = !string.IsNullOrEmpty(ModelTips);

        foreach (var item in TextureItems)
            item.Thumbnail?.Dispose();

        Components.Clear();
        BoneGroups.Clear();
        TextureItems.Clear();
        SoundItems.Clear();
        AnimationNames.Clear();

        foreach (var tex in document.Textures)
            AddTextureEntry(tex.Name, tex.Data, tex.Width, tex.Height, "Texture");
        foreach (var img in document.Images)
            AddTextureEntry(img.Name, img.Data, img.Width, img.Height, img.Category);

        HasTextures = TextureItems.Count > 0;

        foreach (var sound in document.Sounds)
            AddSoundEntry(sound.Name, sound.Data);

        HasSounds = SoundItems.Count > 0;

        var nameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var modelInfo in document.Models)
        {
            var key = modelInfo.Name;
            nameCounts[key] = nameCounts.GetValueOrDefault(key, 0) + 1;
        }

        foreach (var modelInfo in document.Models)
        {
            var displayName = modelInfo.Name;

            if (nameCounts[modelInfo.Name] > 1)
            {
                var disambig = modelInfo.Id
                    .Replace("models/", "")
                    .Replace(".json", "");
                var lastSlash = disambig.LastIndexOf('/');
                if (lastSlash >= 0)
                    disambig = disambig[..lastSlash];
                if (!string.IsNullOrEmpty(disambig))
                    displayName = $"{displayName} ({disambig})";
            }

            displayName = modelInfo.Category switch
            {
                YsmModelCategory.Main => displayName,
                YsmModelCategory.Arm => $"{displayName} (Arm)",
                _ => $"{displayName} (Sub)",
            };

            Components.Add(new ComponentViewModel
            {
                Name = displayName,
                ComponentId = modelInfo.Id,
                IsVisible = modelInfo.DefaultVisible,
                OnVisibilityToggled = (id, vis) => SetComponentVisible(id, vis),
            });
        }

        BuildBoneTree();

        Renderer.LoadModel(document);

        PopulateAnimationData(document);

        HasAnimationController = Renderer is IAnimationRenderer animR && animR.HasAnimationController;
        UseAnimationController = HasAnimationController;

        if (Renderer is IAnimationRenderer rendererWithMolang && rendererWithMolang.MolangService is not null)
        {
            var rendererMolang = rendererWithMolang.MolangService;
            MolangPanel = new MolangPanelViewModel(rendererMolang);
            var expressions = CollectAllMolangExpressions(document);
            MolangPanel.DiscoverVariables(expressions);
        }
        else if (MolangPanel is not null)
        {
            MolangPanel = null;
        }

        HasMolangVariables = MolangPanel?.Variables.Count > 0;

        StatusText = $"Loaded: {ModelName} (V{ModelVersion})";
        Notifications.Show($"Loaded {ModelDisplayName}", NotificationType.Success);
    }

    public ComponentViewModel? GetComponent(string id)
    {
        return Components.FirstOrDefault(c => c.ComponentId == id);
    }

    public void SetComponentVisible(string componentId, bool visible)
    {
        if (Renderer is IInteractiveRenderer interactive)
            interactive.SetComponentVisible(componentId, visible);
    }

    public void SetBoneVisible(string boneId, bool visible)
    {
        if (Renderer is IInteractiveRenderer interactive)
            interactive.SetBoneVisible(boneId, visible);
    }

    private void AddTextureEntry(string name, byte[] data, int width, int height, string category)
    {
        Bitmap? thumbnail = null;
        if (data is { Length: > 0 })
        {
            try { thumbnail = new Bitmap(new MemoryStream(data)); }
            catch { }
        }

        TextureItems.Add(new TextureItemViewModel
        {
            Name = name,
            Category = category,
            DataSize = data.Length,
            Width = width,
            Height = height,
            Thumbnail = thumbnail,
        });
    }

    private void AddSoundEntry(string name, byte[] data)
    {
        var extension = System.IO.Path.GetExtension(name).TrimStart('.');
        SoundItems.Add(new SoundItemViewModel
        {
            Name = name,
            Format = string.IsNullOrEmpty(extension) ? "Audio" : extension.ToUpperInvariant(),
            DataSize = data.Length,
        });
    }

    private static IEnumerable<string> CollectAllMolangExpressions(YsmModelDocument document)
    {
        var expressions = new List<string>();

        foreach (var anim in document.Animations)
        {
            try
            {
                var json = JsonDocument.Parse(anim.Data);
                CollectStringsRecursive(json.RootElement, expressions);
            }
            catch { }
        }

        foreach (var ac in document.AnimControllers)
        {
            try
            {
                var json = JsonDocument.Parse(ac.Data);
                CollectStringsRecursive(json.RootElement, expressions);
            }
            catch { }
        }

        return expressions;
    }

    private static void CollectStringsRecursive(JsonElement element, List<string> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var s = element.GetString();
                if (!string.IsNullOrEmpty(s) &&
                    (s.Contains("query.", StringComparison.OrdinalIgnoreCase) ||
                     s.Contains("q.", StringComparison.OrdinalIgnoreCase) ||
                     s.Contains("variable.", StringComparison.OrdinalIgnoreCase) ||
                     s.Contains("v.", StringComparison.OrdinalIgnoreCase)))
                    result.Add(s);
                break;
            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray())
                    CollectStringsRecursive(child, result);
                break;
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                    CollectStringsRecursive(prop.Value, result);
                break;
        }
    }
}
