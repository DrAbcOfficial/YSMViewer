using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace YSMViewer.Models;

public sealed class MinecraftBoneAnimationConverter : JsonConverter<MinecraftBoneAnimation>
{
    public override MinecraftBoneAnimation? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            return null;

        var result = new MinecraftBoneAnimation();
        var json = JsonNode.Parse(ref reader)?.AsObject();
        if (json is null) return result;

        result.Rotation = ParseChannel(json, "rotation");
        result.Position = ParseChannel(json, "position");
        result.Scale = ParseChannel(json, "scale");
        result.Visibility = ParseChannel(json, "visibility");

        return result;
    }

    private static MinecraftKeyframeSet? ParseChannel(JsonObject json, string key)
    {
        if (!json.TryGetPropertyValue(key, out var node) || node is null)
            return null;

        var kf = new MinecraftKeyframeSet();

        if (node.GetValueKind() == JsonValueKind.Number)
        {
            kf.IsConstant = true;
            kf.ConstantValue = JsonNodeToFloat(node);
        }
        else if (node.GetValueKind() == JsonValueKind.String)
        {
            if (float.TryParse(node.GetValue<string>(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float f))
            {
                kf.IsConstant = true;
                kf.ConstantValue = f;
            }
            else
            {
                kf.HasMolangExpressions = true;
                kf.Keyframes[0f] = [0f, 0f, 0f];
                var expression = node.GetValue<string>();
                kf.RawEntries[0f] = new KeyframeRawEntry { Post = [expression, expression, expression] };
            }
        }
        else if (node.GetValueKind() == JsonValueKind.Array)
        {
            var arr = node.AsArray();
            if (arr.Count > 0)
            {
                var (vals, rawVals, hasExpr) = ParseArrayComponents(arr);
                kf.Keyframes[0f] = vals;
                if (hasExpr)
                {
                    kf.HasMolangExpressions = true;
                    kf.RawEntries[0f] = new KeyframeRawEntry { Post = rawVals };
                }
            }
        }
        else if (node.GetValueKind() == JsonValueKind.Object)
        {
            foreach (var prop in node.AsObject())
            {
                if (!float.TryParse(prop.Key,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float time))
                    continue;

                var valNode = prop.Value;
                if (valNode is null) continue;

                if (valNode.GetValueKind() == JsonValueKind.Number)
                {
                    kf.Keyframes[time] = [JsonNodeToFloat(valNode)];
                }
                else if (valNode.GetValueKind() == JsonValueKind.String)
                {
                    var s = valNode.GetValue<string>();
                    if (float.TryParse(s,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out float f))
                    {
                        kf.Keyframes[time] = [f];
                    }
                    else
                    {
                        kf.HasMolangExpressions = true;
                        kf.Keyframes[time] = [0f];
                        kf.RawEntries[time] = new KeyframeRawEntry { Post = [s] };
                    }
                }
                else if (valNode.GetValueKind() == JsonValueKind.Array)
                {
                    var arr = valNode.AsArray();
                    var (vals, rawVals, hasExpr) = ParseArrayComponents(arr);
                    kf.Keyframes[time] = vals;
                    if (hasExpr)
                    {
                        kf.HasMolangExpressions = true;
                        kf.RawEntries[time] = new KeyframeRawEntry { Post = rawVals };
                    }
                }
                else if (valNode.GetValueKind() == JsonValueKind.Object)
                {
                    ParseKeyframeObject(kf, time, valNode.AsObject());
                }
            }
        }

        return kf;
    }

    private static void ParseKeyframeObject(MinecraftKeyframeSet kf, float time, JsonObject obj)
    {
        object?[]? preVals = null;
        object?[]? postVals = null;
        string? lerpMode = null;
        bool hasExpr = false;

        foreach (var prop in obj)
        {
            if (prop.Key == "lerp_mode" || prop.Key == "lerpmode")
            {
                if (prop.Value?.GetValueKind() == JsonValueKind.String)
                {
                    lerpMode = prop.Value.GetValue<string>();
                    kf.HasAdvancedInterpolation = true;
                }
                continue;
            }

            if (prop.Key != "pre" && prop.Key != "post")
                continue;

            var targetArray = prop.Key == "pre";
            var valNode = prop.Value;
            if (valNode is null) continue;

            if (valNode.GetValueKind() == JsonValueKind.Number)
            {
                var arr = targetArray ? preVals : postVals;
                arr = [valNode.GetValue<double>()];
                if (targetArray) preVals = arr; else postVals = arr;
            }
            else if (valNode.GetValueKind() == JsonValueKind.String)
            {
                var s = valNode.GetValue<string>();
                if (float.TryParse(s, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float f))
                {
                    var arr = new object?[] { f };
                    if (targetArray) preVals = arr; else postVals = arr;
                }
                else
                {
                    hasExpr = true;
                    var arr = new object?[] { s };
                    if (targetArray) preVals = arr; else postVals = arr;
                }
            }
            else if (valNode.GetValueKind() == JsonValueKind.Array)
            {
                var jsonArr = valNode.AsArray();
                var (floats, raws, expr) = ParseArrayComponents(jsonArr);
                var arr = new object?[raws.Length];
                for (int i = 0; i < raws.Length; i++)
                    arr[i] = raws[i] ?? floats[i];
                if (targetArray) preVals = arr; else postVals = arr;
                if (expr) hasExpr = true;
            }
        }

        if (postVals is null && preVals is null)
            return;

        postVals ??= preVals;

        if (postVals is not null)
        {
            var floatVals = new float[postVals.Length];
            for (int i = 0; i < postVals.Length; i++)
            {
                floatVals[i] = postVals[i] switch
                {
                    float f => f,
                    double d => (float)d,
                    string s when float.TryParse(s, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float f) => f,
                    _ => 0f
                };
            }
            kf.Keyframes[time] = floatVals;
        }

        if (hasExpr || preVals is not null || lerpMode is not null)
        {
            if (hasExpr) kf.HasMolangExpressions = true;
            kf.RawEntries[time] = new KeyframeRawEntry
            {
                Pre = preVals,
                Post = postVals ?? [],
                LerpMode = lerpMode
            };
        }
    }

    private static (float[] vals, object?[] rawVals, bool hasExpr) ParseArrayComponents(JsonArray arr)
    {
        var vals = new float[arr.Count];
        var rawVals = new object?[arr.Count];
        bool hasExpr = false;

        for (int i = 0; i < arr.Count; i++)
        {
            var elem = arr[i];
            if (elem is null)
            {
                vals[i] = 0f;
                rawVals[i] = null;
                continue;
            }

            if (elem.GetValueKind() == JsonValueKind.Number)
            {
                vals[i] = (float)elem.GetValue<double>();
                rawVals[i] = null;
            }
            else if (elem.GetValueKind() == JsonValueKind.String)
            {
                var s = elem.GetValue<string>();
                if (float.TryParse(s, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float f))
                {
                    vals[i] = f;
                    rawVals[i] = null;
                }
                else
                {
                    vals[i] = 0f;
                    rawVals[i] = s;
                    hasExpr = true;
                }
            }
            else
            {
                vals[i] = 0f;
                rawVals[i] = null;
            }
        }

        return (vals, rawVals, hasExpr);
    }

    public override void Write(Utf8JsonWriter writer, MinecraftBoneAnimation value, JsonSerializerOptions options)
        => throw new NotSupportedException();

    private static float JsonNodeToFloat(JsonNode? node)
    {
        if (node is null) return 0f;
        return node.GetValueKind() switch
        {
            JsonValueKind.Number => (float)node.GetValue<double>(),
            JsonValueKind.String => float.TryParse(node.GetValue<string>(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 0f,
            _ => 0f,
        };
    }
}
