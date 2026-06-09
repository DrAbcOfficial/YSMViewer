using CommunityToolkit.Mvvm.ComponentModel;

namespace YSMViewer.ViewModels;

public enum MolangControlType
{
    Slider,
    Toggle,
    TextBox,
}

public sealed partial class MolangVariableItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    public string DisplayName => Name.StartsWith("query.", StringComparison.OrdinalIgnoreCase)
        ? Name["query.".Length..]
        : Name;

    [ObservableProperty]
    private float _value;

    public float DefaultValue { get; set; }

    public float MinValue { get; set; } = 0f;

    public float MaxValue { get; set; } = 1f;

    public float Step { get; set; } = 0.1f;

    public MolangControlType ControlType { get; set; } = MolangControlType.Slider;

    public bool IsBoolean { get; set; }

    public event Action<MolangVariableItem, float>? ValueChanged;

    partial void OnValueChanged(float value)
    {
        ValueChanged?.Invoke(this, value);
    }
}