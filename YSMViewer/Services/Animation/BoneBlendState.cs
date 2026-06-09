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

        Vector3 positionDelta = Vector3.Zero;
        Vector3 rotationDelta = Vector3.Zero;
        Vector3 scaleAccum = Vector3.Zero;
        float totalWeight = 0f;

        bool isFirstRotation = true;
        Quaternion rotResult = Quaternion.Identity;

        foreach (var source in _sources)
        {
            if (!source.ConditionActive) continue;
            var queue = source.Queue;
            float weight = source.Weight;
            if (weight <= 0f) continue;

            HasActiveSources = true;
            totalWeight += weight;

            if (queue.PositionType != BoneAnimationQueue.PointType.None)
            {
                float effectiveWeight = weight;
                if (queue.PositionType == BoneAnimationQueue.PointType.Constant)
                {
                    effectiveWeight *= MathF.Max(0f, 1f - queue.TransitionLerpFactor);
                }
                positionDelta += queue.PositionValue * effectiveWeight;
            }

            if (queue.ScaleType != BoneAnimationQueue.PointType.None)
            {
                float effectiveWeight = weight;
                if (queue.ScaleType == BoneAnimationQueue.PointType.Constant)
                {
                    effectiveWeight *= MathF.Max(0f, 1f - queue.TransitionLerpFactor);
                }
                if (effectiveWeight >= 1f)
                {
                    scaleAccum = queue.ScaleValue;
                }
                else
                {
                    scaleAccum = Vector3.Lerp(scaleAccum, queue.ScaleValue, effectiveWeight);
                }
            }

            if (queue.RotationType != BoneAnimationQueue.PointType.None)
            {
                float effectiveWeight = weight;
                if (queue.RotationType == BoneAnimationQueue.PointType.Constant)
                {
                    effectiveWeight *= MathF.Max(0f, 1f - queue.TransitionLerpFactor);
                }

                Vector3 rotBedrock = queue.RotationValue;
                Vector3 rotGltf = new Vector3(-rotBedrock.X, -rotBedrock.Y, rotBedrock.Z);

                Quaternion rotQuat;
                if (queue.PositionType != BoneAnimationQueue.PointType.None)
                {
                    rotQuat = AnimationService.CreateBlockbenchQuaternion(rotGltf);
                }
                else
                {
                    rotQuat = AnimationService.CreateBlockbenchQuaternion(baseEuler + rotGltf);
                }

                if (isFirstRotation)
                {
                    rotResult = rotQuat;
                    isFirstRotation = false;
                }
                else
                {
                    float t = effectiveWeight / (totalWeight);
                    rotResult = Quaternion.Normalize(Quaternion.Slerp(rotResult, rotQuat, t));
                }

                if (queue.RotationType == BoneAnimationQueue.PointType.Transition)
                {
                    if (MathF.Abs(queue.TransitionLerpFactor) < 1E-5f)
                    {
                        rotResult = AnimationService.CreateBlockbenchQuaternion(baseEuler + new Vector3(-queue.TransitionOffset.X, -queue.TransitionOffset.Y, queue.TransitionOffset.Z));
                    }
                }
            }
        }

        if (totalWeight > 0f)
        {
            positionDelta /= totalWeight;
        }

        BlendedPosition = basePosition + positionDelta;
        BlendedRotation = rotResult;
        BlendedScale = scaleAccum;
    }

    private struct BlendSource
    {
        public BoneAnimationQueue Queue;
        public float Weight;
        public bool ConditionActive;
    }
}