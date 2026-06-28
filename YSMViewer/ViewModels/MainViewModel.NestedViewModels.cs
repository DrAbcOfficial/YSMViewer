using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace YSMViewer.ViewModels;

public sealed partial class ExtraAnimationGroupViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    public ObservableCollection<ExtraAnimationItemViewModel> Entries { get; } = [];
}

public sealed partial class ExtraAnimationItemViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string DisplayName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AnimationName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Category { get; set; } = string.Empty;

    public int OriginalIndex { get; set; }

    public Action<ExtraAnimationItemViewModel>? OnSelected { get; set; }

    [RelayCommand]
    private void Select()
    {
        OnSelected?.Invoke(this);
    }
}

public sealed partial class ComponentViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsVisible { get; set; } = false;

    public string ComponentId { get; set; } = string.Empty;

    public ComponentBoneGroupViewModel? BoneGroup { get; set; }

    public Action<string, bool>? OnVisibilityToggled { get; set; }

    partial void OnIsVisibleChanged(bool value)
    {
        OnVisibilityToggled?.Invoke(ComponentId, value);
        if (BoneGroup is not null)
            BoneGroup.IsVisible = value;
    }
}

public sealed partial class ComponentBoneGroupViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    public string ComponentId { get; set; } = string.Empty;
    public ObservableCollection<BoneTreeItemViewModel> BoneRoots { get; } = [];

    public void ExpandAll()
    {
        foreach (var root in BoneRoots)
            root.SetExpandedRecursive(true);
    }

    public void CollapseAll()
    {
        foreach (var root in BoneRoots)
            root.SetExpandedRecursive(false);
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

public sealed partial class SoundItemViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Format { get; set; } = string.Empty;

    [ObservableProperty]
    public partial long DataSize { get; set; }

    [ObservableProperty]
    public partial bool IsPlaying { get; set; }

    [ObservableProperty]
    public partial bool CanPlay { get; set; }

    public Action<SoundItemViewModel>? OnTogglePlayback { get; set; }

    public string PlayPauseText => IsPlaying ? "Pause" : "Play";

    public string SizeDisplay => DataSize < 1024
        ? $"{DataSize} B"
        : $"{DataSize / 1024.0:F1} KB";

    public Avalonia.Media.IBrush BadgeBrush => new Avalonia.Media.SolidColorBrush(
        Avalonia.Media.Color.FromArgb(255, 187, 154, 247));

    partial void OnIsPlayingChanged(bool value)
    {
        OnPropertyChanged(nameof(PlayPauseText));
    }

    [RelayCommand]
    private void TogglePlayback()
    {
        OnTogglePlayback?.Invoke(this);
    }
}
