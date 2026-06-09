using System.Numerics;
using YSMViewer.Models;
using YSMViewer.Models.Keyframes;
using YSMViewer.Services.Molang;

namespace YSMViewer.Services.Animation;

public sealed class BoneAnimationQueue
{
    public string BoneName { get; }

    public float BlendWeight { get; private set; } = 1f;

    public bool AnimationActive { get; private set; }

    public enum PointType { None, KeyFrame, Transition, Constant }

    public PointType RotationType { get; private set; }
    public PointType PositionType { get; private set; }
    public PointType ScaleType { get; private set; }

    public Vector3 RotationValue { get; private set; }
    public Vector3 PositionValue { get; private set; }
    public Vector3 ScaleValue { get; private set; }

    public Vector3 TransitionOffset { get; private set; }
    public float TransitionLerpFactor { get; private set; }

    private BoneKeyFrame[] _rotationFrames = [];
    private BoneKeyFrame[] _positionFrames = [];
    private BoneKeyFrame[] _scaleFrames = [];

    private Vector3 _snapshotPos;
    private Vector3 _snapshotEulerBedrock;
    private Vector3 _snapshotScale;

    private Vector3 _cachedPosDelta;
    private Vector3 _cachedRotBedrock;
    private Vector3 _cachedScale;

    private Vector3 _basePos;
    private Vector3 _baseEulerGltf;

    public BoneAnimationQueue(string boneName, Vector3 basePos, Vector3 baseEulerGltf)
    {
        BoneName = boneName;
        _basePos = basePos;
        _baseEulerGltf = baseEulerGltf;
        _snapshotPos = basePos;
        _snapshotEulerBedrock = Vector3.Zero;
        _snapshotScale = Vector3.One;
    }

    public void CaptureSnapshot(Vector3 currentPos, Quaternion currentRot, Vector3 currentScale)
    {
        _snapshotPos = currentPos;
        _snapshotScale = currentScale;
        _snapshotEulerBedrock = QuaternionToBedrockDelta(currentRot, _baseEulerGltf);
    }

    private static Vector3 QuaternionToBedrockDelta(Quaternion q, Vector3 baseEulerGltf)
    {
        Vector3 eulerAngles = QuaternionToEuler(q);
        Vector3 deltaGltf = eulerAngles - baseEulerGltf;
        return new Vector3(-deltaGltf.X, -deltaGltf.Y, deltaGltf.Z);
    }

    private static Vector3 QuaternionToEuler(Quaternion q)
    {
        float sinrCossp = 2f * (q.W * q.X + q.Y * q.Z);
        float cosrCossp = 1f - 2f * (q.X * q.X + q.Y * q.Y);
        float roll = MathF.Atan2(sinrCossp, cosrCossp);

        float sinp = 2f * (q.W * q.Y - q.Z * q.X);
        float pitch = MathF.Abs(sinp) >= 1f ? MathF.CopySign(MathF.PI / 2f, sinp) : MathF.Asin(sinp);

        float sinyCosp = 2f * (q.W * q.Z + q.X * q.Y);
        float cosyCosp = 1f - 2f * (q.Y * q.Y + q.Z * q.Z);
        float yaw = MathF.Atan2(sinyCosp, cosyCosp);

        return new Vector3(
            roll * (180f / MathF.PI),
            pitch * (180f / MathF.PI),
            yaw * (180f / MathF.PI));
    }

    public void ApplyAnimation(MinecraftBoneAnimation boneAnim)
    {
        _rotationFrames = BuildKeyFrames(boneAnim.Rotation);
        _positionFrames = BuildKeyFrames(boneAnim.Position);
        _scaleFrames = BuildKeyFrames(boneAnim.Scale);
        AnimationActive = true;
        ResetQueues();
    }

    public void Clear()
    {
        _rotationFrames = [];
        _positionFrames = [];
        _scaleFrames = [];
        AnimationActive = false;
        ResetQueues();
    }

    public void SetBlendWeight(float weight)
    {
        BlendWeight = weight > 0f ? weight : 0f;
    }

    public void ResetQueues()
    {
        RotationType = PointType.None;
        PositionType = PointType.None;
        ScaleType = PointType.None;
    }

    public void CacheCurrentValues()
    {
        if (RotationType != PointType.None)
            _cachedRotBedrock = RotationValue;
        if (PositionType != PointType.None)
            _cachedPosDelta = PositionValue;
        if (ScaleType != PointType.None)
            _cachedScale = ScaleValue;
    }

    public void ProcessRunning(float tick, MolangService molang)
    {
        if (_rotationFrames.Length > 0)
        {
            RotationValue = EvaluateKeyFrames(_rotationFrames, tick, molang);
            RotationType = PointType.KeyFrame;
        }
        if (_positionFrames.Length > 0)
        {
            PositionValue = EvaluateKeyFrames(_positionFrames, tick, molang);
            PositionType = PointType.KeyFrame;
        }
        if (_scaleFrames.Length > 0)
        {
            ScaleValue = EvaluateKeyFrames(_scaleFrames, tick, molang);
            ScaleType = PointType.KeyFrame;
        }
    }

    public void ProcessBeginningTransition(float progress, float tick, MolangService molang)
    {
        if (_rotationFrames.Length > 0)
        {
            Vector3 firstFrame = EvaluateKeyFrames(_rotationFrames, 0f, molang);
            Vector3 blendTo;
            if (progress >= 1f)
            {
                blendTo = firstFrame;
            }
            else
            {
                Vector3 currentFrame;
                if (tick <= 0f)
                    currentFrame = _snapshotEulerBedrock;
                else
                    currentFrame = EvaluateKeyFrames(_rotationFrames, tick, molang);

                blendTo = Vector3.Lerp(_snapshotEulerBedrock, currentFrame, progress);
            }
            RotationValue = blendTo;
            TransitionOffset = _snapshotEulerBedrock;
            TransitionLerpFactor = progress;
            RotationType = PointType.Transition;
        }
        if (_positionFrames.Length > 0)
        {
            Vector3 firstFrame = EvaluateKeyFrames(_positionFrames, 0f, molang);
            Vector3 currentFrame;
            if (progress >= 1f)
            {
                currentFrame = firstFrame;
            }
            else
            {
                currentFrame = tick <= 0f ? _snapshotPos : EvaluateKeyFrames(_positionFrames, tick, molang);
                currentFrame = Vector3.Lerp(_snapshotPos, currentFrame, progress);
            }
            PositionValue = currentFrame;
            PositionType = PointType.Transition;
        }
        if (_scaleFrames.Length > 0)
        {
            Vector3 firstFrame = EvaluateKeyFrames(_scaleFrames, 0f, molang);
            Vector3 currentFrame;
            if (progress >= 1f)
            {
                currentFrame = firstFrame;
            }
            else
            {
                Vector3 dest = tick <= 0f ? _snapshotScale : EvaluateKeyFrames(_scaleFrames, tick, molang);
                currentFrame = Vector3.Lerp(_snapshotScale, dest, progress);
            }
            ScaleValue = currentFrame;
            ScaleType = PointType.Transition;
        }
    }

    public void ProcessEndingTransition(float progress)
    {
        if (RotationType != PointType.None || _rotationFrames.Length > 0)
        {
            Vector3 target = Vector3.Zero;
            RotationValue = Vector3.Lerp(_cachedRotBedrock, target, progress);
            TransitionLerpFactor = 1f - progress;
            RotationType = PointType.Constant;
        }
        if (PositionType != PointType.None || _positionFrames.Length > 0)
        {
            Vector3 target = Vector3.Zero;
            PositionValue = Vector3.Lerp(_cachedPosDelta, target, progress);
            PositionType = PointType.Constant;
        }
        if (ScaleType != PointType.None || _scaleFrames.Length > 0)
        {
            Vector3 target = Vector3.One;
            ScaleValue = Vector3.Lerp(_cachedScale, target, progress);
            ScaleType = PointType.Constant;
        }
    }

    private static Vector3 EvaluateKeyFrames(BoneKeyFrame[] frames, float tick, MolangService molang)
    {
        if (frames.Length == 0) return Vector3.Zero;
        if (frames.Length == 1) return frames[0].Evaluate(molang, 1f);

        if (tick <= frames[0].StartTime)
            return frames[0].GetValue(molang, true);
        if (tick >= frames[^1].EndTime)
            return frames[^1].GetValue(molang, false);

        for (int i = 0; i < frames.Length - 1; i++)
        {
            if (tick >= frames[i].StartTime && tick <= frames[i + 1].StartTime)
            {
                float duration = frames[i + 1].StartTime - frames[i].StartTime;
                float progress = duration > 0f ? (tick - frames[i].StartTime) / duration : 1f;
                return frames[i].Evaluate(molang, progress);
            }
        }

        return frames[^1].GetValue(molang, false);
    }

    private static BoneKeyFrame[] BuildKeyFrames(MinecraftKeyframeSet? kf)
    {
        if (kf is null) return [];
        if (kf.IsConstant) return [];
        if (kf.Keyframes.Count == 0) return [];

        var sorted = kf.Keyframes.OrderBy(k => k.Key).ToList();

        var rawFrames = new List<RawBoneKeyFrame>(sorted.Count);
        foreach (var (time, values) in sorted.Select(kv => (kv.Key, kv.Value)))
        {
            kf.RawEntries.TryGetValue(time, out var rawEntry);
            string? lerpMode = rawEntry?.LerpMode;
            string? easing = null;

            object?[]? preVals = rawEntry?.Pre;
            object?[] postVals;

            if (rawEntry is not null && rawEntry.Post.Length > 0)
            {
                postVals = rawEntry.Post;
                if (preVals is null && rawEntry.Pre is not null)
                    preVals = rawEntry.Pre;
            }
            else
            {
                postVals = new object?[values.Length];
                for (int i = 0; i < values.Length; i++)
                    postVals[i] = values[i];
            }

            object? preX = null, preY = null, preZ = null;
            if (preVals is not null && preVals.Length >= 3)
            {
                preX = preVals[0];
                preY = preVals[1];
                preZ = preVals[2];
            }

            object? postX = postVals.Length > 0 ? postVals[0] : 0f;
            object? postY = postVals.Length > 1 ? postVals[1] : 0f;
            object? postZ = postVals.Length > 2 ? postVals[2] : 0f;

            rawFrames.Add(new RawBoneKeyFrame(time, preX, preY, preZ, postX, postY, postZ, lerpMode, easing));
        }

        return BoneKeyFrameProcessor.Process([.. rawFrames]);
    }
}