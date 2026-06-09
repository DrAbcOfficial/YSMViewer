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

    public string DisplayName => StripDomain(Name);

    public string Domain => GetDomain(Name);

    public bool IsQuery => Domain == "query";

    public bool IsVariable => Domain == "variable";

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

    private static string StripDomain(string name)
    {
        var dotIdx = name.IndexOf('.');
        return dotIdx > 0 ? name[(dotIdx + 1)..] : name;
    }

    private static string GetDomain(string name)
    {
        var dotIdx = name.IndexOf('.');
        if (dotIdx <= 0) return "query";
        var prefix = name[..dotIdx].ToLowerInvariant();
        return prefix is "v" ? "variable" : prefix;
    }
}