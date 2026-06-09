using System.Numerics;
using YSMViewer.Models.Keyframes;
using YSMViewer.Services.Molang;

namespace YSMViewer.Services.Animation;

public readonly struct AnimationPoint
{
    public Vector3 Value { get; }

    public AnimationPoint(Vector3 value)
    {
        Value = value;
    }
}

public readonly struct TransitionPoint
{
    public Vector3 Offset { get; }
    public float LerpFactor { get; }
    public Vector3 Destination { get; }

    public TransitionPoint(Vector3 offset, float lerpFactor, Vector3 destination)
    {
        Offset = offset;
        LerpFactor = lerpFactor;
        Destination = destination;
    }

    public Vector3 Evaluate()
    {
        if (MathF.Abs(LerpFactor) < 1E-5f)
            return Offset;
        return Vector3.Lerp(Offset, Destination, LerpFactor);
    }
}

public readonly struct ConstantPoint
{
    public Vector3 Value { get; }
    public float PercentCompleted { get; }

    public ConstantPoint(Vector3 value, float percentCompleted)
    {
        Value = value;
        PercentCompleted = percentCompleted;
    }
}