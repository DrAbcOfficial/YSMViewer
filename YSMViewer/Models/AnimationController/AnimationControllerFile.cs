using System.Text.Json.Serialization;

namespace YSMViewer.Models.AnimationController;

public sealed class AnimationControllerFile
{
    [JsonPropertyName("format_version")]
    public string FormatVersion { get; set; } = "1.8.0";

    [JsonPropertyName("animation_controllers")]
    public Dictionary<string, AnimationControllerEntry> Controllers { get; set; } = [];
}

public sealed class AnimationControllerEntry
{
    [JsonPropertyName("initial_state")]
    public string? InitialState { get; set; }

    [JsonPropertyName("states")]
    public Dictionary<string, AnimationControllerStateModel> States { get; set; } = [];
}

public sealed class AnimationControllerStateModel
{
    [JsonPropertyName("animations")]
    public List<string>? Animations { get; set; }

    [JsonPropertyName("transitions")]
    public List<Dictionary<string, string>>? Transitions { get; set; }

    [JsonPropertyName("on_entry")]
    public List<string>? OnEntry { get; set; }

    [JsonPropertyName("on_exit")]
    public List<string>? OnExit { get; set; }

    [JsonPropertyName("blend_transition")]
    public float BlendTransition { get; set; } = 0f;

    [JsonPropertyName("blend_via_shortest_path")]
    public bool BlendViaShortestPath { get; set; }

    [JsonPropertyName("sound_effects")]
    public List<string>? SoundEffects { get; set; }
}

public sealed class AnimationSlotReference
{
    public string AnimationName { get; }
    public string? ConditionExpression { get; }

    public AnimationSlotReference(string animationName, string? conditionExpression)
    {
        AnimationName = animationName;
        ConditionExpression = conditionExpression;
    }

    public static AnimationSlotReference Parse(string entry)
    {
        var idx = entry.IndexOf('>');
        if (idx > 0)
        {
            var name = entry[..idx].Trim();
            var condition = entry[(idx + 1)..].Trim();
            return new AnimationSlotReference(name, condition);
        }
        return new AnimationSlotReference(entry.Trim(), null);
    }
}