using Aura3D.Core.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using YSMViewer.Services;

namespace YSMViewer.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    private readonly YsmLoaderService _loaderService = new();
    private readonly AnimationService _animationService = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = "Ready. Open a .ysm file to begin.";

    [ObservableProperty]
    private bool _showToolbar;

    [ObservableProperty]
    private string _modelName = string.Empty;

    [ObservableProperty]
    private int _modelVersion;

    [ObservableProperty]
    private bool _hasModel;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorDetail = string.Empty;

    [ObservableProperty]
    private bool _hasAnimations;

    [ObservableProperty]
    private string _currentAnimationName = string.Empty;

    [ObservableProperty]
    private float _animationProgress;

    [ObservableProperty]
    private bool _isAnimating;

    [ObservableProperty]
    private string _animationTimeText = string.Empty;

    [ObservableProperty]
    private bool _canPreviousAnimation;

    [ObservableProperty]
    private bool _canNextAnimation;

    [ObservableProperty]
    private string _modelDisplayName = string.Empty;

    [ObservableProperty]
    private string _modelAuthors = string.Empty;

    [ObservableProperty]
    private string _modelLicense = string.Empty;

    [ObservableProperty]
    private string _modelTips = string.Empty;

    [ObservableProperty]
    private bool _isFreeModel;

    public ObservableCollection<ComponentViewModel> Components { get; } = [];
    public ObservableCollection<string> AnimationNames { get; } = [];
    public ObservableCollection<BoneTreeItemViewModel> BoneTreeRoots { get; } = [];

    private YsmLoaderService.LoadedModel? _currentModel;
    private Action<Model>? _onSceneReady;
    private Action<float>? _onAnimationUpdate;
    private float _animTime;

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

    public void SetSceneCallback(Action<Model> onSceneReady)
    {
        _onSceneReady = onSceneReady;
    }

    public void SetAnimationCallback(Action<float> onAnimationUpdate)
    {
        _onAnimationUpdate = onAnimationUpdate;
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

            await Task.Run(() =>
            {
                _currentModel = YsmLoaderService.Load(filePath);
            });

            if (_currentModel is null)
            {
                StatusText = "Error: failed to load model";
                return;
            }

            PopulateModelData(_currentModel);
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

            YsmLoaderService.LoadedModel? loadedModel = null;
            await Task.Run(() =>
            {
                loadedModel = YsmLoaderService.LoadFromBytes(data);
            });

            if (loadedModel is null)
            {
                StatusText = "Error: failed to load model";
                return;
            }

            PopulateModelData(loadedModel);
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

            using var client = new System.Net.Http.HttpClient();
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

    private void PopulateModelData(YsmLoaderService.LoadedModel loadedModel)
    {
        StatusText = "Building scene...";
        ModelName = loadedModel.ModelName;
        ModelVersion = loadedModel.Version;
        HasModel = true;
        _currentModel = loadedModel;

        if (loadedModel.Metadata is not null)
        {
            var meta = loadedModel.Metadata;
            ModelDisplayName = meta.Name ?? loadedModel.ModelName;
            ModelAuthors = meta.Authors is { Length: > 0 }
                ? string.Join(", ", meta.Authors) : "Unknown";
            ModelLicense = meta.LicenseType ?? "Unknown";
            IsFreeModel = meta.IsFree;
            ModelTips = meta.Tips ?? string.Empty;
        }
        else
        {
            ModelDisplayName = loadedModel.ModelName;
            ModelAuthors = string.Empty;
            ModelLicense = string.Empty;
            IsFreeModel = false;
            ModelTips = string.Empty;
        }

        Components.Clear();
        BoneTreeRoots.Clear();

        foreach (var modelInfo in loadedModel.ModelNodes)
        {
            var displayName = modelInfo.Category switch
            {
                YsmLoaderService.ModelCategory.Main => modelInfo.Name,
                YsmLoaderService.ModelCategory.Arm => $"{modelInfo.Name} (Arm)",
                _ => $"{modelInfo.Name} (Sub)",
            };
            if (modelInfo.GeometryCount > 1)
                displayName += $" [{modelInfo.GeometryCount} UV]";
            Components.Add(new ComponentViewModel
            {
                Name = displayName,
                ModelNode = modelInfo.Node,
                IsVisible = modelInfo.DefaultVisible,
            });
        }

        _animationService.SetBoneNodes(loadedModel.BoneNodes, loadedModel.BaseBoneEulers);

        if (loadedModel.Animations.Count > 0)
        {
            _animationService.LoadAnimations(loadedModel.Animations[0].Data);
            HasAnimations = _animationService.AnimationNames.Count > 0;

            AnimationNames.Clear();
            foreach (var name in _animationService.AnimationNames)
                AnimationNames.Add(name);

            IsAnimating = false;
            CanPreviousAnimation = _animationService.AnimationNames.Count > 0;
            CanNextAnimation = _animationService.AnimationNames.Count > 0;
        }

        BuildBoneTree();

        _onSceneReady?.Invoke(loadedModel.ContainerNode);

        StatusText = $"Loaded: {ModelName} (V{ModelVersion})";
    }

    partial void OnIsAnimatingChanged(bool value)
    {
        _animationService.IsPlaying = value;
        if (value && _currentAnimationName is { Length: > 0 })
            _animationService.PlayAnimation(_currentAnimationName);
    }

    public void SelectAnimation(string name)
    {
        if (_animationService.AnimationNames.Contains(name))
        {
            CurrentAnimationName = name;
            _animationService.PlayAnimation(name);
            _animTime = 0f;
            AnimationProgress = 0f;
            IsAnimating = true;
            UpdateAnimationNavigationState();
        }
    }

    public void NextAnimation()
    {
        if (_animationService.AnimationNames.Count == 0) return;
        var names = AnimationNames;
        int currentIndex = names.IndexOf(CurrentAnimationName);
        if (currentIndex < 0) currentIndex = 0;
        var nextIndex = (currentIndex + 1) % names.Count;
        SelectAnimation(names[nextIndex]);
    }

    public void PreviousAnimation()
    {
        if (_animationService.AnimationNames.Count == 0) return;
        var names = AnimationNames;
        int currentIndex = names.IndexOf(CurrentAnimationName);
        if (currentIndex < 0) currentIndex = 0;
        var prevIndex = (currentIndex - 1 + names.Count) % names.Count;
        SelectAnimation(names[prevIndex]);
    }

    private void UpdateAnimationNavigationState()
    {
        var names = _animationService.AnimationNames;
        CanPreviousAnimation = names.Count > 0;
        CanNextAnimation = names.Count > 0;
    }

    public void UpdateAnimation(float deltaTime)
    {
        if (!IsAnimating) return;

        _animationService.Update(deltaTime);
        _animTime = _animationService.CurrentTime;
        float len = _animationService.AnimationLength;
        AnimationProgress = len > 0 ? _animTime / len : 0f;

        if (len > 0)
        {
            var timeSpan = TimeSpan.FromSeconds(_animTime);
            var lenSpan = TimeSpan.FromSeconds(len);
            AnimationTimeText = $"{timeSpan.Minutes}:{timeSpan.Seconds:D2}.{timeSpan.Milliseconds / 10:D2} / {lenSpan.Minutes}:{lenSpan.Seconds:D2}.{lenSpan.Milliseconds / 10:D2}";
        }

        _onAnimationUpdate?.Invoke(deltaTime);
    }

    private void BuildBoneTree()
    {
        BoneTreeRoots.Clear();
        if (_currentModel is null) return;

        foreach (var modelInfo in _currentModel.ModelNodes)
        {
            foreach (var child in modelInfo.Node.Children)
            {
                var item = BuildBoneTreeItem(child);
                if (item is not null)
                    BoneTreeRoots.Add(item);
            }
        }
    }

    private static BoneTreeItemViewModel? BuildBoneTreeItem(Node node)
    {
        if (node is Mesh) return null;

        var item = new BoneTreeItemViewModel
        {
            Name = node.Name,
            SceneNode = node,
            IsVisible = node.Enable
        };

        foreach (var child in node.Children)
        {
            var childItem = BuildBoneTreeItem(child);
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
}

public sealed partial class ComponentViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isVisible = false;

    public Model? ModelNode { get; set; }

    partial void OnIsVisibleChanged(bool value)
    {
        if (ModelNode is not null)
            ModelNode.Enable = value;
    }
}

public sealed partial class BoneTreeItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _hasChildren;

    [ObservableProperty]
    private string _icon = "🧊";

    public Node? SceneNode { get; set; }
    public ObservableCollection<BoneTreeItemViewModel> Children { get; } = [];

    partial void OnIsVisibleChanged(bool value)
    {
        if (SceneNode is not null)
            SceneNode.Enable = value;
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
