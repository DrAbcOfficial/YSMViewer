using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
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
    public bool IsBrowserReadOnly => !SupportsComponentVisibility && !SupportsBoneVisibility;

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
        LocSelectFolder = L.GetString("SelectFolder", culture)!;
        LocOpenYSMTitle = L.GetString("OpenYSMTitle", culture)!;
        LocAutoRotate = L.GetString("AutoRotate", culture)!;
        LocSideView = L.GetString("LeftView", culture)!;
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
    public partial string LocSideView { get; set; } = "";

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
    public partial string LocSelectFolder { get; set; } = "";

    [ObservableProperty]
    public partial string LocOpenYSMTitle { get; set; } = "";

    [ObservableProperty]
    public partial string LocAutoRotate { get; set; } = "";

    private async Task OnFileSelectedFromBrowser(string filePath)
    {
        await LoadFileAsync(filePath);
    }

    private void OnScanError(string message)
    {
        Notifications.Show(message, NotificationType.Warning);
    }

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
    public partial bool HasModelAuthors { get; set; }

    [ObservableProperty]
    public partial bool HasModelLicense { get; set; }

    [ObservableProperty]
    public partial bool HasModelTips { get; set; }

    [ObservableProperty]
    public partial TextureItemViewModel? SelectedTexture { get; set; }

    public ObservableCollection<ComponentViewModel> Components { get; } = [];
    public ObservableCollection<string> AnimationNames { get; } = [];
    public ObservableCollection<BoneTreeItemViewModel> BoneTreeRoots { get; } = [];
    public ObservableCollection<TextureItemViewModel> TextureItems { get; } = [];

    private YsmModelDocument? _currentDocument;

    public string? StartupFilePath { get; set; }

    public string? StartupFileUrl { get; set; }

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
        HasAnimations = false;
        AnimationNames.Clear();
        CurrentAnimationName = string.Empty;
        IsAnimating = false;

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
        HasAnimations = false;
        AnimationNames.Clear();
        CurrentAnimationName = string.Empty;
        IsAnimating = false;

        try
        {
            IsLoading = true;
            StatusText = "Parsing YSM...";

            YsmModelDocument? document = null;
            await Task.Run(() =>
            {
                document = YsmLoaderService.LoadDocumentFromBytes(data);
            });

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

        Components.Clear();
        BoneTreeRoots.Clear();
        TextureItems.Clear();
        AnimationNames.Clear();

        foreach (var tex in document.Textures)
            AddTextureEntry(tex.Name, tex.Data, tex.Width, tex.Height, "Texture");
        foreach (var img in document.Images)
            AddTextureEntry(img.Name, img.Data, img.Width, img.Height, img.Category);

        HasTextures = TextureItems.Count > 0;

        foreach (var modelInfo in document.Models)
        {
            var displayName = modelInfo.Category switch
            {
                YsmModelCategory.Main => modelInfo.Name,
                YsmModelCategory.Arm => $"{modelInfo.Name} (Arm)",
                _ => $"{modelInfo.Name} (Sub)",
            };

            Components.Add(new ComponentViewModel
            {
                Name = displayName,
                ComponentId = modelInfo.Id,
                IsVisible = modelInfo.DefaultVisible,
                OnVisibilityToggled = (id, vis) => SetComponentVisible(id, vis),
            });
        }

        if (SupportsAnimation && Renderer is IAnimationRenderer animRenderer)
        {
            if (document.Animations.Count > 0)
            {
                HasAnimations = animRenderer.AnimationNames.Count > 0;

                foreach (var name in animRenderer.AnimationNames)
                    AnimationNames.Add(name);

                CanPreviousAnimation = animRenderer.AnimationNames.Count > 0;
                CanNextAnimation = animRenderer.AnimationNames.Count > 0;
            }
        }

        BuildBoneTree();

        Renderer.LoadModel(document);

        StatusText = $"Loaded: {ModelName} (V{ModelVersion})";
        Notifications.Show($"Loaded {ModelDisplayName}", NotificationType.Success);
    }

    public ComponentViewModel? GetComponent(string id)
    {
        return Components.FirstOrDefault(c => c.ComponentId == id);
    }

    partial void OnIsAnimatingChanged(bool value)
    {
        if (Renderer is IAnimationRenderer animRenderer)
        {
            if (value && CurrentAnimationName is { Length: > 0 })
                animRenderer.PlayAnimation(CurrentAnimationName);
        }
    }

    public void SelectAnimation(string name)
    {
        if (Renderer is not IAnimationRenderer animRenderer) return;
        if (!animRenderer.AnimationNames.Contains(name)) return;

        CurrentAnimationName = name;
        animRenderer.PlayAnimation(name);
        AnimationProgress = 0f;
        IsAnimating = true;
        UpdateAnimationNavigationState();
    }

    public void NextAnimation()
    {
        if (Renderer is not IAnimationRenderer animRenderer) return;
        if (animRenderer.AnimationNames.Count == 0) return;

        var names = AnimationNames;
        int currentIndex = names.IndexOf(CurrentAnimationName);
        if (currentIndex < 0) currentIndex = 0;
        var nextIndex = (currentIndex + 1) % names.Count;
        SelectAnimation(names[nextIndex]);
    }

    public void StopAnimation()
    {
        IsAnimating = false;
        CurrentAnimationName = string.Empty;
        AnimationProgress = 0f;
        AnimationTimeText = string.Empty;

        if (Renderer is IAnimationRenderer animRenderer)
            animRenderer.StopAnimation();
    }

    public void PreviousAnimation()
    {
        if (Renderer is not IAnimationRenderer animRenderer) return;
        if (animRenderer.AnimationNames.Count == 0) return;

        var names = AnimationNames;
        int currentIndex = names.IndexOf(CurrentAnimationName);
        if (currentIndex < 0) currentIndex = 0;
        var prevIndex = (currentIndex - 1 + names.Count) % names.Count;
        SelectAnimation(names[prevIndex]);
    }

    private void UpdateAnimationNavigationState()
    {
        if (Renderer is IAnimationRenderer animRenderer)
        {
            var names = animRenderer.AnimationNames;
            CanPreviousAnimation = names.Count > 0;
            CanNextAnimation = names.Count > 0;
        }
    }

    public void UpdateAnimation(float deltaTime)
    {
        if (!IsAnimating || Renderer is not IAnimationRenderer animRenderer) return;

        animRenderer.Update(deltaTime);
        AnimationProgress = 0f;
    }

    public void UpdateAutoRotation(float deltaTime)
    {
        if (Renderer is IAutoRotateRenderer rotRenderer)
            rotRenderer.Update(deltaTime);
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

    private void BuildBoneTree()
    {
        BoneTreeRoots.Clear();
        if (_currentDocument is null) return;

        var boneParentMap = new Dictionary<string, string?>();
        foreach (var model in _currentDocument.Models)
        {
            foreach (var bone in model.Bones)
                boneParentMap[bone.Id] = bone.ParentId;
        }

        var rootBones = new List<YsmBoneInfo>();
        foreach (var model in _currentDocument.Models)
        {
            foreach (var bone in model.Bones)
            {
                if (bone.ParentId is null || !boneParentMap.ContainsKey(bone.ParentId))
                    rootBones.Add(bone);
            }
        }

        foreach (var bone in rootBones)
        {
            var item = BuildBoneTreeItem(bone, _currentDocument);
            if (item is not null)
                BoneTreeRoots.Add(item);
        }
    }

    private BoneTreeItemViewModel? BuildBoneTreeItem(YsmBoneInfo bone, YsmModelDocument document)
    {
        var item = new BoneTreeItemViewModel
        {
            Name = bone.Name,
            BoneId = bone.Id,
            IsVisible = true,
            OnVisibilityToggled = (id, vis) => SetBoneVisible(id, vis),
        };

        var childBones = new List<YsmBoneInfo>();
        foreach (var model in document.Models)
        {
            foreach (var child in model.Bones)
            {
                if (child.ParentId == bone.Id)
                    childBones.Add(child);
            }
        }

        foreach (var child in childBones)
        {
            var childItem = BuildBoneTreeItem(child, document);
            if (childItem is not null)
                item.Children.Add(childItem);
        }

        item.HasChildren = item.Children.Count > 0;
        item.Icon = item.HasChildren ? "📁" : "🧊";
        return item;
    }

    public void ExpandAllBones()
    {
        foreach (var root in BoneTreeRoots)
            root.SetExpandedRecursive(true);
    }

    public void CollapseAllBones()
    {
        foreach (var root in BoneTreeRoots)
            root.SetExpandedRecursive(false);
    }

    private void AddTextureEntry(string name, byte[] data, int width, int height, string category)
    {
        TextureItems.Add(new TextureItemViewModel
        {
            Name = name,
            Category = category,
            DataSize = data.Length,
            Width = width,
            Height = height,
            Thumbnail = null,
        });
    }
}

public sealed partial class ComponentViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsVisible { get; set; } = false;

    public string ComponentId { get; set; } = string.Empty;

    public Action<string, bool>? OnVisibilityToggled { get; set; }

    partial void OnIsVisibleChanged(bool value)
    {
        OnVisibilityToggled?.Invoke(ComponentId, value);
    }
}

public sealed partial class BoneTreeItemViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    [ObservableProperty]
    public partial bool HasChildren { get; set; }

    [ObservableProperty]
    public partial string Icon { get; set; } = "🧊";

    public string BoneId { get; set; } = string.Empty;
    public ObservableCollection<BoneTreeItemViewModel> Children { get; } = [];

    public Action<string, bool>? OnVisibilityToggled { get; set; }

    partial void OnIsVisibleChanged(bool value)
    {
        OnVisibilityToggled?.Invoke(BoneId, value);
        foreach (var child in Children)
            child.IsVisible = value;
    }

    public void SetExpandedRecursive(bool expanded)
    {
        IsExpanded = expanded;
        foreach (var child in Children)
            child.SetExpandedRecursive(expanded);
    }
}

public sealed partial class TextureItemViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Category { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Avalonia.Media.Imaging.Bitmap? Thumbnail { get; set; }

    [ObservableProperty]
    public partial int Width { get; set; }

    [ObservableProperty]
    public partial int Height { get; set; }

    [ObservableProperty]
    public partial long DataSize { get; set; }

    public string SizeDisplay => DataSize < 1024
        ? $"{DataSize} B"
        : $"{DataSize / 1024.0:F1} KB";

    public string DimensionsDisplay => Width > 0 && Height > 0
        ? $"{Width} x {Height}"
        : "Unknown";

    public Avalonia.Media.IBrush BadgeBrush => Category switch
    {
        "Avatar" => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(255, 158, 206, 106)),
        "Background" => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(255, 224, 175, 104)),
        "Special" => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(255, 247, 118, 142)),
        _ => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(255, 122, 162, 247)),
    };
}
