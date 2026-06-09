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

    public Vector3 RotationTransitionOffset { get; private set; }
    public float RotationTransitionLerp { get; private set; }
    public Vector3 PositionTransitionOffset { get; private set; }
    public float PositionTransitionLerp { get; private set; }
    public Vector3 ScaleTransitionOffset { get; private set; }
    public float ScaleTransitionLerp { get; private set; }

    private BoneKeyFrame[] _rotationFrames = [];
    private BoneKeyFrame[] _positionFrames = [];
    private BoneKeyFrame[] _scaleFrames = [];
    private BoneKeyFrame[] _visibilityFrames = [];

    private Vector3 _snapshotPos;
    private Vector3 _snapshotRotBedrock;
    private Vector3 _snapshotScale;

    private Vector3 _cachedPosDelta;
    private Vector3 _cachedRotBedrock;
    private Vector3 _cachedScale;

    private readonly Vector3 _basePos;
    private readonly Vector3 _baseEulerGltf;

    public bool IsVisible { get; private set; } = true;
    public bool HasVisibilityControl { get; private set; }

    public BoneAnimationQueue(string boneName, Vector3 basePos, Vector3 baseEulerGltf)
    {
        BoneName = boneName;
        _basePos = basePos;
        _baseEulerGltf = baseEulerGltf;
        _snapshotPos = basePos;
        _snapshotRotBedrock = Vector3.Zero;
        _snapshotScale = Vector3.One;
    }

    public void CaptureSnapshot(Vector3 currentPos, Quaternion currentRot, Vector3 currentScale)
    {
        _snapshotPos = currentPos;
        _snapshotScale = currentScale;
        _snapshotRotBedrock = QuaternionToBedrockDelta(currentRot, _baseEulerGltf);
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
        _visibilityFrames = BuildKeyFrames(boneAnim.Visibility);
        HasVisibilityControl = _visibilityFrames.Length > 0;
        AnimationActive = true;
        ResetQueues();
    }

    public void Clear()
    {
        _rotationFrames = [];
        _positionFrames = [];
        _scaleFrames = [];
        _visibilityFrames = [];
        IsVisible = true;
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
        RotationTransitionLerp = 0f;
        PositionTransitionLerp = 0f;
        ScaleTransitionLerp = 0f;
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
        if (_visibilityFrames.Length > 0)
        {
            var visValue = EvaluateKeyFrames(_visibilityFrames, tick, molang);
            IsVisible = visValue.X > 0.5f;
        }
    }

    public void ProcessBeginningTransition(float progress, float adjustedTick, MolangService molang)
    {
        if (_rotationFrames.Length > 0)
        {
            Vector3 destValue = adjustedTick > 0f
                ? EvaluateKeyFrames(_rotationFrames, adjustedTick, molang)
                : EvaluateKeyFrames(_rotationFrames, 0f, molang);

            RotationValue = destValue;
            RotationTransitionOffset = _snapshotRotBedrock;
            RotationTransitionLerp = progress;
            RotationType = PointType.Transition;
        }
        if (_positionFrames.Length > 0)
        {
            Vector3 destValue = adjustedTick > 0f
                ? EvaluateKeyFrames(_positionFrames, adjustedTick, molang)
                : EvaluateKeyFrames(_positionFrames, 0f, molang);

            Vector3 result;
            if (progress >= 1f)
            {
                result = destValue;
            }
            else
            {
                Vector3 snapshotDeltaGltf = _snapshotPos - _basePos;
                Vector3 snapshotDeltaBedrock = new Vector3(
                    snapshotDeltaGltf.X * -16f,
                    snapshotDeltaGltf.Y * 16f,
                    snapshotDeltaGltf.Z * 16f);
                result = Vector3.Lerp(snapshotDeltaBedrock, destValue, progress);
            }

            PositionValue = result;
            PositionTransitionOffset = _snapshotPos - _basePos;
            PositionTransitionLerp = progress;
            PositionType = PointType.Transition;
        }
        if (_scaleFrames.Length > 0)
        {
            Vector3 destValue = adjustedTick > 0f
                ? EvaluateKeyFrames(_scaleFrames, adjustedTick, molang)
                : EvaluateKeyFrames(_scaleFrames, 0f, molang);

            Vector3 result;
            if (progress >= 1f)
            {
                result = destValue;
            }
            else
            {
                result = Vector3.Lerp(_snapshotScale, destValue, progress);
            }

            ScaleValue = result;
            ScaleTransitionOffset = _snapshotScale;
            ScaleTransitionLerp = progress;
            ScaleType = PointType.Transition;
        }
    }

    public void ProcessEndingTransition(float progress)
    {
        if (_rotationFrames.Length > 0)
        {
            RotationValue = Vector3.Lerp(_cachedRotBedrock, Vector3.Zero, progress);
            RotationTransitionLerp = 1f - progress;
            RotationType = PointType.Constant;
        }
        if (_positionFrames.Length > 0)
        {
            Vector3 zero = Vector3.Zero;
            PositionValue = Vector3.Lerp(_cachedPosDelta, zero, progress);
            PositionTransitionLerp = 1f - progress;
            PositionType = PointType.Constant;
        }
        if (_scaleFrames.Length > 0)
        {
            ScaleValue = Vector3.Lerp(_cachedScale, Vector3.One, progress);
            ScaleTransitionLerp = 1f - progress;
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

        int idx = BinarySearchFrame(frames, tick);
        if (idx < 0) return frames[0].GetValue(molang, true);
        if (idx >= frames.Length - 1) return frames[^1].GetValue(molang, false);

        float duration = frames[idx + 1].StartTime - frames[idx].StartTime;
        float progress = duration > 0f ? (tick - frames[idx].StartTime) / duration : 1f;
        return frames[idx].Evaluate(molang, progress);
    }

    private static int BinarySearchFrame(BoneKeyFrame[] frames, float tick)
    {
        int lo = 0, hi = frames.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (frames[mid].StartTime <= tick)
                lo = mid;
            else
                hi = mid - 1;
        }
        return lo;
    }

    private static BoneKeyFrame[] BuildKeyFrames(MinecraftKeyframeSet? kf)
    {
        if (kf is null) return [];
        return BoneKeyFrameProcessor.FromKeyframeSet(kf);
    }
}