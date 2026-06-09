using System.Text.Json;
using System.Text.Json.Serialization;

namespace YSMViewer.Models;

public sealed class MinecraftAnimationFile
{
    [JsonPropertyName("format_version")]
    public string FormatVersion { get; set; } = "";

    [JsonPropertyName("animations")]
    public Dictionary<string, MinecraftAnimation> Animations { get; set; } = [];
}

public sealed class MinecraftAnimation
{
    [JsonPropertyName("animation_length")]
    public float AnimationLength { get; set; }

    [JsonPropertyName("loop")]
    public JsonElement? LoopRaw { get; set; }

    [JsonIgnore]
    public bool Loop
    {
        get
        {
            if (LoopRaw is not { } raw) return true;
            if (raw.ValueKind == JsonValueKind.True) return true;
            if (raw.ValueKind == JsonValueKind.False) return false;
            if (raw.ValueKind == JsonValueKind.String)
            {
                var s = raw.GetString();
                return s == "true" || s == "True";
            }
            return true;
        }
    }

    [JsonPropertyName("anim_time_update")]
    public string? AnimTimeUpdate { get; set; }

    [JsonPropertyName("blend_weight")]
    public float BlendWeight { get; set; } = 1.0f;

    [JsonPropertyName("bones")]
    public JsonElement? BonesRaw { get; set; }

    [JsonIgnore]
    public Dictionary<string, MinecraftBoneAnimation>? Bones { get; set; }
}

public sealed class MinecraftBoneAnimation
{
    public MinecraftKeyframeSet? Rotation { get; set; }
    public MinecraftKeyframeSet? Position { get; set; }
    public MinecraftKeyframeSet? Scale { get; set; }
}

public sealed class MinecraftKeyframeSet
{
    public Dictionary<float, float[]> Keyframes { get; set; } = [];

    public bool IsConstant { get; set; }
    public float ConstantValue { get; set; }

    public Dictionary<float, KeyframeRawEntry> RawEntries { get; set; } = [];
    public bool HasMolangExpressions { get; set; }
    public bool HasAdvancedInterpolation { get; set; }
}

public sealed class KeyframeRawEntry
{
    public object?[]? Pre { get; set; }
    public object?[] Post { get; set; } = [];
    public string? LerpMode { get; set; }
}
