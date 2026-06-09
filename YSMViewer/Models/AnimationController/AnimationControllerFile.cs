using System.Text.Json;
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
    [JsonConverter(typeof(SoundEffectListConverter))]
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

public sealed class SoundEffectListConverter : JsonConverter<List<string>>
{
    public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected start of array for sound_effects");

        var list = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                list.Add(reader.GetString()!);
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                string? effect = null;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        var propName = reader.GetString();
                        reader.Read();
                        if (string.Equals(propName, "effect", StringComparison.OrdinalIgnoreCase)
                            && reader.TokenType == JsonTokenType.String)
                        {
                            effect = reader.GetString();
                        }
                        else
                        {
                            reader.Skip();
                        }
                    }
                }
                if (effect is not null)
                    list.Add(effect);
            }
            else
            {
                reader.Skip();
            }
        }

        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
            writer.WriteStringValue(item);
        writer.WriteEndArray();
    }
}