using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using YSMViewer.Models;
using YSMViewer.Models.Keyframes;
using YSMViewer.Services.Molang;

namespace YSMViewer.Services;

public sealed class AnimationService(
    Dictionary<string, IAnimatableBone> boneNodes,
    Dictionary<string, Vector3> baseEulers)
{
    private readonly Dictionary<string, IAnimatableBone> _boneNodes = boneNodes;
    private readonly Dictionary<string, Vector3> _basePositions = [];
    private readonly Dictionary<string, Vector3> _baseEulers = baseEulers;
    private readonly Dictionary<string, MinecraftAnimation> _allAnimations = [];
    private readonly Dictionary<MinecraftKeyframeSet, BoneKeyFrame[]> _processedKeyframes = [];
    private MinecraftAnimation? _currentAnimation;
    private float _currentTime;
    private bool _isPlaying = true;

    public MolangService? MolangService { get; set; }

    public IReadOnlyDictionary<string, MinecraftAnimation> GetAllAnimations() => _allAnimations;

    public IReadOnlyList<string> AnimationNames =>
        _allAnimations.Where(kv => IsValidLength(kv.Value.AnimationLength))
                      .Select(kv => kv.Key)
                      .ToList();

    public float AnimationLength => _currentAnimation?.AnimationLength ?? 0f;
    public float CurrentTime => _currentTime;
    public bool IsPlaying
    {
        get => _isPlaying;
        set => _isPlaying = value;
    }

    public AnimationService() : this([], []) { }


    public void SetBoneNodes(
        Dictionary<string, IAnimatableBone> boneNodes,
        IReadOnlyDictionary<string, Vector3>? baseEulers = null)
    {
        _boneNodes.Clear();
        _basePositions.Clear();
        _baseEulers.Clear();
        _allAnimations.Clear();
        _currentAnimation = null;

        foreach (var kv in boneNodes)
        {
            _boneNodes[kv.Key] = kv.Value;
            _basePositions[kv.Key] = kv.Value.Position;
        }
        if (baseEulers is not null)
        {
            foreach (var kv in baseEulers)
                _baseEulers[kv.Key] = kv.Value;
        }
    }

    public void LoadAnimations(byte[] animationJsonData)
    {
        MinecraftAnimationFile? file;
        try
        {
            file = JsonSerializer.Deserialize(animationJsonData, YsmJsonContext.Default.MinecraftAnimationFile);
        }
        catch
        {
            return;
        }
        if (file is null) return;

        foreach (var (name, anim) in file.Animations)
        {
            if (!_allAnimations.ContainsKey(name))
            {
                if (!IsValidLength(anim.AnimationLength))
                    continue;

                if (anim.BonesRaw is { ValueKind: JsonValueKind.Object } raw)
                    anim.Bones = ParseBones(raw);
                _allAnimations[name] = anim;
            }
        }
    }

    private static Dictionary<string, MinecraftBoneAnimation> ParseBones(JsonElement bonesElement)
    {
        var converter = new MinecraftBoneAnimationConverter();
        var result = new Dictionary<string, MinecraftBoneAnimation>();
        var options = new JsonSerializerOptions();

        foreach (var prop in bonesElement.EnumerateObject())
        {
            var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(prop.Value.GetRawText()));
            reader.Read();
            var boneAnim = converter.Read(ref reader, typeof(MinecraftBoneAnimation), options);
            if (boneAnim is not null)
                result[prop.Name] = boneAnim;
        }

        return result;
    }

    public void ResetBones()
    {
        _currentTime = 0f;
        _isPlaying = false;

        foreach (var (name, node) in _boneNodes)
        {
            if (_basePositions.TryGetValue(name, out var basePos))
                node.Position = basePos;
            if (_baseEulers.TryGetValue(name, out var baseEuler))
                node.RotationQuaternion = CreateBlockbenchQuaternion(baseEuler);
            node.Scale = Vector3.One;
        }
    }

    public void PlayAnimation(string name)
    {
        if (_allAnimations.TryGetValue(name, out var anim))
        {
            if (!IsValidLength(anim.AnimationLength))
                return;

            _currentAnimation = anim;
            _currentTime = 0f;
            _isPlaying = true;
        }
    }

    private static bool IsValidLength(float length) =>
        length > 0f && !float.IsInfinity(length) && !float.IsNaN(length);

    public void Update(float deltaTime)
    {
        if (!_isPlaying || _currentAnimation is null) return;

        _currentTime += deltaTime;
        float length = _currentAnimation.AnimationLength;
        if (length <= 0f) return;

        if (_currentTime >= length)
        {
            if (_currentAnimation.Loop)
                _currentTime %= length;
            else
                _currentTime = length;
        }

        if (_currentAnimation.Bones is null) return;

        foreach (var (boneName, boneAnim) in _currentAnimation.Bones)
        {
            if (!_boneNodes.TryGetValue(boneName, out var node)) continue;

            var basePos = _basePositions.GetValueOrDefault(boneName);
            var baseEulerGltf = _baseEulers.GetValueOrDefault(boneName);

            if (boneAnim.Rotation is not null)
            {
                var animDeltaBedrock = EvaluateKeyframeSet(boneAnim.Rotation, _currentTime);
                var animDeltaGltf = new Vector3(-animDeltaBedrock.X, -animDeltaBedrock.Y, animDeltaBedrock.Z);

                Vector3 combinedGltf;
                if (boneAnim.Position is not null || boneAnim.Scale is not null)
                {
                    combinedGltf = animDeltaGltf;
                }
                else
                {
                    combinedGltf = baseEulerGltf + animDeltaGltf;
                }
                node.RotationQuaternion = CreateBlockbenchQuaternion(combinedGltf);
            }
            if (boneAnim.Position is not null)
            {
                var animDeltaBedrock = EvaluateKeyframeSet(boneAnim.Position, _currentTime);
                var animDeltaGltf = new Vector3(-animDeltaBedrock.X, animDeltaBedrock.Y, animDeltaBedrock.Z) / 16f;
                node.Position = basePos + animDeltaGltf;
            }
            if (boneAnim.Scale is not null)
            {
                var animScale = EvaluateKeyframeSet(boneAnim.Scale, _currentTime);
                node.Scale = animScale;
            }
        }
    }

    internal static Quaternion CreateBlockbenchQuaternion(Vector3 eulerDegrees)
    {
        float rx = eulerDegrees.X * MathF.PI / 180f;
        float ry = eulerDegrees.Y * MathF.PI / 180f;
        float rz = eulerDegrees.Z * MathF.PI / 180f;
        var m = Matrix4x4.CreateRotationX(rx)
              * Matrix4x4.CreateRotationY(ry)
              * Matrix4x4.CreateRotationZ(rz);
        return Quaternion.CreateFromRotationMatrix(m);
    }

    private Vector3 EvaluateKeyframeSet(MinecraftKeyframeSet kf, float time)
    {
        if (kf.IsConstant)
            return new Vector3(kf.ConstantValue);

        if (kf.Keyframes.Count == 0)
            return Vector3.Zero;

        if (kf.HasMolangExpressions || kf.HasAdvancedInterpolation)
        {
            return EvaluateKeyframeSetAdvanced(kf, time);
        }

        var sorted = kf.Keyframes.OrderBy(k => k.Key).ToList();

        if (time <= sorted[0].Key)
            return ToVector3(sorted[0].Value);

        if (time >= sorted[^1].Key)
            return ToVector3(sorted[^1].Value);

        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (time >= sorted[i].Key && time <= sorted[i + 1].Key)
            {
                float t = (time - sorted[i].Key) / (sorted[i + 1].Key - sorted[i].Key);
                var a = ToVector3(sorted[i].Value);
                var b = ToVector3(sorted[i + 1].Value);
                return Vector3.Lerp(a, b, t);
            }
        }

        return ToVector3(sorted[^1].Value);
    }

    private Vector3 EvaluateKeyframeSetAdvanced(MinecraftKeyframeSet kf, float time)
    {
        if (!_processedKeyframes.TryGetValue(kf, out var frames))
        {
            frames = BuildKeyFrames(kf);
            _processedKeyframes[kf] = frames;
        }

        if (frames.Length == 0)
            return Vector3.Zero;

        if (MolangService is null)
            return EvaluateKeyframeSetSimple(kf, time);

        var molang = MolangService;

        if (frames.Length == 1)
            return frames[0].Evaluate(molang, 1f);

        if (time <= frames[0].StartTime)
            return frames[0].GetValue(molang, true);

        if (time >= frames[^1].EndTime)
            return frames[^1].GetValue(molang, false);

        for (int i = 0; i < frames.Length; i++)
        {
            if (time >= frames[i].StartTime && time <= frames[i].EndTime)
            {
                float progress = (time - frames[i].StartTime) / frames[i].Duration;
                return frames[i].Evaluate(molang, progress);
            }
        }

        return frames[^1].GetValue(molang, false);
    }

    private static BoneKeyFrame[] BuildKeyFrames(MinecraftKeyframeSet kf)
    {
        return BoneKeyFrameProcessor.FromKeyframeSet(kf);
    }

    private static Vector3 EvaluateKeyframeSetSimple(MinecraftKeyframeSet kf, float time)
    {
        if (kf.IsConstant)
            return new Vector3(kf.ConstantValue);

        if (kf.Keyframes.Count == 0)
            return Vector3.Zero;

        var sorted = kf.Keyframes.OrderBy(k => k.Key).ToList();

        if (time <= sorted[0].Key)
            return ToVector3(sorted[0].Value);

        if (time >= sorted[^1].Key)
            return ToVector3(sorted[^1].Value);

        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (time >= sorted[i].Key && time <= sorted[i + 1].Key)
            {
                float t = (time - sorted[i].Key) / (sorted[i + 1].Key - sorted[i].Key);
                var a = ToVector3(sorted[i].Value);
                var b = ToVector3(sorted[i + 1].Value);
                return Vector3.Lerp(a, b, t);
            }
        }

        return ToVector3(sorted[^1].Value);
    }

    private static Vector3 ToVector3(float[] values)
    {
        if (values.Length == 0) return Vector3.Zero;
        if (values.Length == 1) return new Vector3(values[0]);
        if (values.Length == 2) return new Vector3(values[0], values[1], 0);
        return new Vector3(values[0], values[1], values[2]);
    }
}

public sealed class MinecraftBoneAnimationConverter : System.Text.Json.Serialization.JsonConverter<MinecraftBoneAnimation>
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
                kf.IsConstant = true;
                kf.ConstantValue = 0f;
                kf.HasMolangExpressions = true;
                kf.RawEntries[0f] = new KeyframeRawEntry
                {
                    Post = [node.GetValue<string>()]
                };
            }
        }
        else if (node.GetValueKind() == JsonValueKind.Array)
        {
            var arr = node.AsArray();
            if (arr.Count > 0)
            {
                kf.IsConstant = true;
                kf.ConstantValue = 0f;
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
                arr = new object?[] { valNode.GetValue<double>() };
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
