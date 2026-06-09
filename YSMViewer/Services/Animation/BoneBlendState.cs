using System.Numerics;
using YSMViewer.Services.Molang;

namespace YSMViewer.Services.Animation;

public sealed class BoneBlendState
{
    private readonly List<BlendSource> _sources = [];

    public string BoneName { get; }

    public Vector3 BlendedPosition { get; private set; }
    public Quaternion BlendedRotation { get; private set; } = Quaternion.Identity;
    public Vector3 BlendedScale { get; private set; } = Vector3.One;
    public bool HasActiveSources { get; private set; }
    public bool IsVisibilityControlled { get; private set; }
    public bool VisibilityValue { get; private set; } = true;

    public BoneBlendState(string boneName)
    {
        BoneName = boneName;
    }

    public void Reset()
    {
        _sources.Clear();
        BlendedPosition = Vector3.Zero;
        BlendedRotation = Quaternion.Identity;
        BlendedScale = Vector3.One;
        HasActiveSources = false;
        IsVisibilityControlled = false;
        VisibilityValue = true;
    }

    public void AddSource(BoneAnimationQueue queue)
    {
        if (!queue.AnimationActive) return;

        _sources.Add(new BlendSource
        {
            Queue = queue,
            Weight = queue.BlendWeight,
            ConditionActive = true,
        });
    }

    public void Blend(
        Vector3 basePosition,
        Vector3 baseEuler,
        MolangService molang)
    {
        if (_sources.Count == 0)
        {
            HasActiveSources = false;
            return;
        }

        HasActiveSources = false;

        Vector3 positionAccum = Vector3.Zero;
        float totalPositionWeight = 0f;

        Vector3 scaleAccum = Vector3.Zero;
        float totalScaleWeight = 0f;

        Quaternion? firstRotQuat = null;
        float rotWeightSum = 0f;
        bool hasRotation = false;

        Quaternion? snapshotQuat = null;
        float snapshotLerp = 0f;
        bool applySnapshot = false;

        foreach (var source in _sources)
        {
            if (!source.ConditionActive) continue;
            var queue = source.Queue;
            float weight = source.Weight;
            if (weight <= 0f) continue;

            HasActiveSources = true;

            if (queue.PositionType != BoneAnimationQueue.PointType.None)
            {
                float effectiveWeight = weight;
                if (queue.PositionType == BoneAnimationQueue.PointType.Constant)
                    effectiveWeight *= MathF.Max(0f, 1f - queue.PositionTransitionLerp);

                Vector3 posBedrock = queue.PositionValue;
                Vector3 posGltf = new Vector3(-posBedrock.X, posBedrock.Y, posBedrock.Z) / 16f;
                positionAccum += posGltf * effectiveWeight;
                totalPositionWeight += effectiveWeight;
            }

            if (queue.ScaleType != BoneAnimationQueue.PointType.None)
            {
                float effectiveWeight = weight;
                if (queue.ScaleType == BoneAnimationQueue.PointType.Constant)
                    effectiveWeight *= MathF.Max(0f, 1f - queue.ScaleTransitionLerp);

                Vector3 s = queue.ScaleValue;
                scaleAccum += s * effectiveWeight;
                totalScaleWeight += effectiveWeight;
            }

            if (queue.RotationType != BoneAnimationQueue.PointType.None)
            {
                float effectiveWeight = weight;
                if (queue.RotationType == BoneAnimationQueue.PointType.Constant)
                    effectiveWeight *= MathF.Max(0f, 1f - queue.RotationTransitionLerp);

                Vector3 rotBedrock = queue.RotationValue;
                Vector3 rotGltf = new Vector3(-rotBedrock.X, -rotBedrock.Y, rotBedrock.Z);

                Quaternion rotQuat;
                if (queue.PositionType != BoneAnimationQueue.PointType.None)
                    rotQuat = AnimationService.CreateBlockbenchQuaternion(rotGltf);
                else
                    rotQuat = AnimationService.CreateBlockbenchQuaternion(baseEuler + rotGltf);

                if (queue.RotationType == BoneAnimationQueue.PointType.Transition)
                {
                    applySnapshot = true;
                    Vector3 offsetGltf = new Vector3(-queue.RotationTransitionOffset.X, -queue.RotationTransitionOffset.Y, queue.RotationTransitionOffset.Z);
                    Vector3 snapEuler = baseEuler + offsetGltf;
                    snapshotQuat = AnimationService.CreateBlockbenchQuaternion(snapEuler);
                    snapshotLerp = queue.RotationTransitionLerp;
                }

                if (firstRotQuat is null)
                {
                    firstRotQuat = rotQuat;
                    rotWeightSum = effectiveWeight;
                    hasRotation = true;
                }
                else
                {
                    float t = effectiveWeight / (rotWeightSum + effectiveWeight);
                    firstRotQuat = Quaternion.Normalize(Quaternion.Slerp(firstRotQuat.Value, rotQuat, t));
                    rotWeightSum += effectiveWeight;
                }
            }
        }

        if (totalPositionWeight > 0f)
            BlendedPosition = basePosition + positionAccum / totalPositionWeight;
        else
            BlendedPosition = basePosition;

        if (totalScaleWeight > 0f)
            BlendedScale = scaleAccum / totalScaleWeight;
        else
            BlendedScale = Vector3.One;

        if (hasRotation)
        {
            BlendedRotation = firstRotQuat!.Value;
            if (applySnapshot && snapshotQuat is not null && snapshotLerp > 1E-5f && snapshotLerp < 1f)
                BlendedRotation = Quaternion.Normalize(Quaternion.Slerp(snapshotQuat.Value, BlendedRotation, snapshotLerp));
            else if (applySnapshot && snapshotLerp <= 1E-5f)
                BlendedRotation = snapshotQuat!.Value;
        }
        else
        {
            BlendedRotation = Quaternion.Identity;
        }

        bool anyVisibilitySource = false;
        bool anyVisible = false;
        foreach (var source in _sources)
        {
            if (!source.ConditionActive) continue;
            var queue = source.Queue;
            if (!queue.AnimationActive) continue;
            if (queue.HasVisibilityControl)
            {
                anyVisibilitySource = true;
                if (queue.IsVisible) anyVisible = true;
            }
        }
        IsVisibilityControlled = anyVisibilitySource;
        VisibilityValue = anyVisibilitySource ? anyVisible : true;
    }

    private struct BlendSource
    {
        public BoneAnimationQueue Queue;
        public float Weight;
        public bool ConditionActive;
    }
}