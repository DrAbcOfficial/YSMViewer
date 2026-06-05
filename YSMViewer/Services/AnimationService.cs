using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aura3D.Core.Nodes;
using YSMViewer.Models;

namespace YSMViewer.Services;

public sealed class AnimationService
{
    private readonly Dictionary<string, Node> _boneNodes;
    private readonly Dictionary<string, Vector3> _basePositions = [];
    private readonly Dictionary<string, Vector3> _baseEulers = [];
    private MinecraftAnimationFile? _currentFile;
    private MinecraftAnimation? _currentAnimation;
    private float _currentTime;
    private bool _isPlaying = true;

    public string? CurrentAnimationName => _currentFile?.Animations
        .FirstOrDefault(kv => kv.Value == _currentAnimation).Key;

    public IReadOnlyList<string> AnimationNames { get; private set; } = [];

    public float AnimationLength => _currentAnimation?.AnimationLength ?? 0f;
    public float CurrentTime => _currentTime;
    public bool IsPlaying
    {
        get => _isPlaying;
        set => _isPlaying = value;
    }

    public AnimationService() : this(new Dictionary<string, Node>(),
        new Dictionary<string, Vector3>()) { }

    public AnimationService(
        Dictionary<string, Node> boneNodes,
        Dictionary<string, Vector3> baseEulers)
    {
        _boneNodes = boneNodes;
    }

    public void SetBoneNodes(
        Dictionary<string, Node> boneNodes,
        IReadOnlyDictionary<string, Vector3>? baseEulers = null)
    {
        _boneNodes.Clear();
        _basePositions.Clear();
        _baseEulers.Clear();

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
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        _currentFile = JsonSerializer.Deserialize<MinecraftAnimationFile>(animationJsonData, options);
        if (_currentFile is null) return;

        var names = new List<string>();
        foreach (var (name, anim) in _currentFile.Animations)
        {
            names.Add(name);
            if (anim.BonesRaw is { ValueKind: JsonValueKind.Object } raw)
            {
                anim.Bones = ParseBones(raw);
            }
        }
        AnimationNames = names;
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

    public void PlayAnimation(string name)
    {
        if (_currentFile?.Animations.TryGetValue(name, out var anim) == true)
        {
            _currentAnimation = anim;
            _currentTime = 0f;
            _isPlaying = true;
        }
    }

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
                if (boneAnim.Position is not null)
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

    private static Vector3 EvaluateKeyframeSet(MinecraftKeyframeSet kf, float time)
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
        }
        else if (node.GetValueKind() == JsonValueKind.Array)
        {
            var arr = node.AsArray();
            if (arr.Count > 0)
            {
                kf.IsConstant = true;
                kf.ConstantValue = 0f;
                var vals = new float[arr.Count];
                for (int i = 0; i < arr.Count; i++)
                    vals[i] = JsonNodeToFloat(arr[i]);
                kf.Keyframes[0f] = vals;
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
                else if (valNode.GetValueKind() == JsonValueKind.Array)
                {
                    var arr = valNode.AsArray();
                    var vals = new float[arr.Count];
                    for (int i = 0; i < arr.Count; i++)
                        vals[i] = JsonNodeToFloat(arr[i]);
                    kf.Keyframes[time] = vals;
                }
                else if (valNode.GetValueKind() == JsonValueKind.String)
                {
                    if (float.TryParse(valNode.GetValue<string>(),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out float f))
                        kf.Keyframes[time] = [f];
                }
            }
        }

        return kf;
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
