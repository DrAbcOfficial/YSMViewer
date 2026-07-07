using Microsoft.Extensions.Logging;
using System.Numerics;
using System.Text.Json;
using YSMViewer.Models;
using YSMViewer.Models.Keyframes;
using YSMViewer.Services.Molang;

namespace YSMViewer.Services;

public sealed class AnimationService(
    Dictionary<string, IAnimatableBone> boneNodes,
    Dictionary<string, Vector3> baseEulers)
{
    private static readonly ILogger Logger = YsmLog.For<AnimationService>();
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
        [.. _allAnimations.Where(kv => IsValidLength(kv.Value.AnimationLength)).Select(kv => kv.Key)];

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
        Logger.LogInformation("Setting bone nodes: {Count} bones, {EulerCount} base eulers",
            boneNodes.Count, baseEulers?.Count ?? 0);

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
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to deserialize animation JSON");
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

        Logger.LogInformation("Loaded {Count} animations from JSON ({RawCount} raw entries)",
            _allAnimations.Count, file.Animations.Count);
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
            {
                Logger.LogWarning("Skipping invalid animation '{Name}' with length {Length}", name, anim.AnimationLength);
                return;
            }

            Logger.LogDebug("Playing animation '{Name}' ({Length}s, loop={LoopMode})", name, anim.AnimationLength, anim.LoopMode);
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
            if (_currentAnimation.LoopMode == AnimationLoopMode.HoldOnLastFrame)
                _currentTime = length;
            else if (_currentAnimation.LoopMode == AnimationLoopMode.Loop)
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
                var animDeltaBedrock = Sanitize(EvaluateKeyframeSet(boneAnim.Rotation, _currentTime));
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
                var animDeltaBedrock = Sanitize(EvaluateKeyframeSet(boneAnim.Position, _currentTime));
                var animDeltaGltf = new Vector3(-animDeltaBedrock.X, animDeltaBedrock.Y, animDeltaBedrock.Z) / 16f;
                node.Position = Sanitize(basePos + animDeltaGltf);
            }
            if (boneAnim.Scale is not null)
            {
                var animScale = Sanitize(EvaluateKeyframeSet(boneAnim.Scale, _currentTime));
                node.Scale = animScale;
            }
        }
    }

    public static Quaternion CreateBlockbenchQuaternion(Vector3 eulerDegrees)
    {
        if (float.IsNaN(eulerDegrees.X) || float.IsNaN(eulerDegrees.Y) || float.IsNaN(eulerDegrees.Z)
            || float.IsInfinity(eulerDegrees.X) || float.IsInfinity(eulerDegrees.Y) || float.IsInfinity(eulerDegrees.Z))
            return Quaternion.Identity;

        float rx = eulerDegrees.X * MathF.PI / 180f;
        float ry = eulerDegrees.Y * MathF.PI / 180f;
        float rz = eulerDegrees.Z * MathF.PI / 180f;
        var m = Matrix4x4.CreateRotationX(rx)
              * Matrix4x4.CreateRotationY(ry)
              * Matrix4x4.CreateRotationZ(rz);
        return Quaternion.CreateFromRotationMatrix(m);
    }

    private static Vector3 Sanitize(Vector3 v)
    {
        return new Vector3(
            float.IsNaN(v.X) || float.IsInfinity(v.X) ? 0f : v.X,
            float.IsNaN(v.Y) || float.IsInfinity(v.Y) ? 0f : v.Y,
            float.IsNaN(v.Z) || float.IsInfinity(v.Z) ? 0f : v.Z);
    }

    private Vector3 EvaluateKeyframeSet(MinecraftKeyframeSet kf, float time)
    {
        if (kf.IsConstant)
            return new Vector3(kf.ConstantValue);

        if (kf.Keyframes.Count == 0)
            return Vector3.Zero;

        if (kf.HasMolangExpressions || kf.HasAdvancedInterpolation)
        {
            return Sanitize(EvaluateKeyframeSetAdvanced(kf, time));
        }

        var sorted = kf.Keyframes.OrderBy(k => k.Key).ToList();

        if (time <= sorted[0].Key)
            return Sanitize(ToVector3(sorted[0].Value));

        if (time >= sorted[^1].Key)
            return Sanitize(ToVector3(sorted[^1].Value));

        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (time >= sorted[i].Key && time <= sorted[i + 1].Key)
            {
                float t = (time - sorted[i].Key) / (sorted[i + 1].Key - sorted[i].Key);
                var a = ToVector3(sorted[i].Value);
                var b = ToVector3(sorted[i + 1].Value);
                return Sanitize(Vector3.Lerp(a, b, t));
            }
        }

        return Sanitize(ToVector3(sorted[^1].Value));
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
        {
            Logger.LogDebug("MolangService not available, using simple evaluation fallback");
            return EvaluateKeyframeSetSimple(kf, time);
        }

        var molang = MolangService;

        if (frames.Length == 1)
            return Sanitize(frames[0].Evaluate(molang, 1f));

        if (time <= frames[0].StartTime)
            return Sanitize(frames[0].GetValue(molang, true));

        if (time >= frames[^1].EndTime)
            return Sanitize(frames[^1].GetValue(molang, false));

        for (int i = 0; i < frames.Length; i++)
        {
            if (time >= frames[i].StartTime && time <= frames[i].EndTime)
            {
                float progress = (time - frames[i].StartTime) / frames[i].Duration;
                return Sanitize(frames[i].Evaluate(molang, progress));
            }
        }

        return Sanitize(frames[^1].GetValue(molang, false));
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
            return Sanitize(ToVector3(sorted[0].Value));

        if (time >= sorted[^1].Key)
            return Sanitize(ToVector3(sorted[^1].Value));

        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (time >= sorted[i].Key && time <= sorted[i + 1].Key)
            {
                float t = (time - sorted[i].Key) / (sorted[i + 1].Key - sorted[i].Key);
                var a = ToVector3(sorted[i].Value);
                var b = ToVector3(sorted[i + 1].Value);
                return Sanitize(Vector3.Lerp(a, b, t));
            }
        }

        return Sanitize(ToVector3(sorted[^1].Value));
    }

    private static Vector3 ToVector3(float[] values)
    {
        if (values.Length == 0) return Vector3.Zero;
        if (values.Length == 1) return new Vector3(values[0]);
        if (values.Length == 2) return new Vector3(values[0], values[1], 0);
        return new Vector3(values[0], values[1], values[2]);
    }
}
