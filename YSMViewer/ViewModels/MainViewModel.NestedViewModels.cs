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

    public ExtraAnimationSettingsGroupViewModel? SettingsGroup { get; set; }

    public bool HasSettings => SettingsGroup is not null && SettingsGroup.Forms.Count > 0;

    public Action<ExtraAnimationItemViewModel>? OnSelected { get; set; }

    [RelayCommand]
    private void Select()
    {
        OnSelected?.Invoke(this);
    }
}

public sealed partial class ExtraAnimationSettingsGroupViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    public string GroupId { get; set; } = string.Empty;
    public ObservableCollection<ExtraAnimationFormViewModel> Forms { get; } = [];
}

public abstract partial class ExtraAnimationFormViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;
}

public sealed partial class ExtraAnimationBooleanFormViewModel : ExtraAnimationFormViewModel
{
    private readonly Action<bool> _onValueChanged;

    [ObservableProperty]
    public partial bool Value { get; set; }

    public ExtraAnimationBooleanFormViewModel(Action<bool> onValueChanged)
    {
        _onValueChanged = onValueChanged;
    }

    partial void OnValueChanged(bool value)
    {
        _onValueChanged(value);
    }
}

public sealed partial class ExtraAnimationRangeFormViewModel : ExtraAnimationFormViewModel
{
    private readonly Action<double> _onValueChanged;

    [ObservableProperty]
    public partial double Value { get; set; }

    public double Min { get; set; }
    public double Max { get; set; }
    public double Step { get; set; }

    public ExtraAnimationRangeFormViewModel(Action<double> onValueChanged)
    {
        _onValueChanged = onValueChanged;
    }

    partial void OnValueChanged(double value)
    {
        _onValueChanged(value);
    }
}

public sealed partial class ExtraAnimationRadioFormViewModel : ExtraAnimationFormViewModel
{
    private readonly Action<ExtraAnimationRadioOptionViewModel?> _onValueChanged;

    [ObservableProperty]
    public partial ExtraAnimationRadioOptionViewModel? SelectedOption { get; set; }

    public ObservableCollection<ExtraAnimationRadioOptionViewModel> Options { get; } = [];

    public ExtraAnimationRadioFormViewModel(Action<ExtraAnimationRadioOptionViewModel?> onValueChanged)
    {
        _onValueChanged = onValueChanged;
    }

    partial void OnSelectedOptionChanged(ExtraAnimationRadioOptionViewModel? value)
    {
        _onValueChanged(value);
    }
}

public sealed class ExtraAnimationRadioOptionViewModel
{
    public string Label { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
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
