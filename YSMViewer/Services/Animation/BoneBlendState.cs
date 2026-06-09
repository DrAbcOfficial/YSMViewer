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

        Vector3 positionAccum = Vector3.Zero;
        Quaternion rotResult = Quaternion.Identity;
        Vector3 scaleAccum = Vector3.One;
        float totalPositionWeight = 0f;
        bool isFirstRotation = true;
        bool isRotationTransition = false;
        Vector3 rotTransitionOffset = Vector3.Zero;
        float rotTransitionLerp = 0f;

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
                float t = MathF.Min(effectiveWeight, 1f);
                scaleAccum = Vector3.Lerp(scaleAccum, s, t);
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
                {
                    rotQuat = AnimationService.CreateBlockbenchQuaternion(rotGltf);
                }
                else
                {
                    rotQuat = AnimationService.CreateBlockbenchQuaternion(baseEuler + rotGltf);
                }

                if (isFirstRotation)
                {
                    if (queue.RotationType == BoneAnimationQueue.PointType.Transition)
                    {
                        isRotationTransition = true;
                        Vector3 offsetGltf = new Vector3(-queue.RotationTransitionOffset.X, -queue.RotationTransitionOffset.Y, queue.RotationTransitionOffset.Z);
                        rotTransitionOffset = baseEuler + offsetGltf;
                        rotTransitionLerp = queue.RotationTransitionLerp;

                        if (MathF.Abs(rotTransitionLerp) < 1E-5f)
                        {
                            rotResult = AnimationService.CreateBlockbenchQuaternion(rotTransitionOffset);
                            isFirstRotation = false;
                            continue;
                        }
                    }
                    rotResult = rotQuat;
                    isFirstRotation = false;
                }
                else
                {
                    float t = MathF.Min(effectiveWeight, 1f);
                    rotResult = Quaternion.Normalize(Quaternion.Slerp(rotResult, rotQuat, t));
                }
            }
        }

        if (totalPositionWeight > 0f)
        {
            BlendedPosition = basePosition + positionAccum;
        }
        else
        {
            BlendedPosition = basePosition;
        }

        if (isRotationTransition)
        {
            Quaternion snapshotQuat = AnimationService.CreateBlockbenchQuaternion(rotTransitionOffset);
            rotResult = Quaternion.Normalize(Quaternion.Slerp(snapshotQuat, rotResult, rotTransitionLerp));
        }

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