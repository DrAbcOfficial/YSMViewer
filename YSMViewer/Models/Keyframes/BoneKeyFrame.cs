namespace YSMViewer.Models.Keyframes;

using System.Numerics;
using YSMViewer.Services.Molang;

public abstract record BoneKeyFrame(
    float StartTime,
    float Duration
)
{
    public float EndTime => StartTime + Duration;

    public abstract Vector3v? PreValue { get; }
    public abstract Vector3v? PostValue { get; }
    public EasingType EasingMode { get; init; } = EasingType.Linear;

    public abstract Vector3 Evaluate(MolangService molang, float progress);

    public Vector3 GetValue(MolangService molang, bool usePre)
    {
        var v = usePre ? PreValue : PostValue;
        return v?.Evaluate(molang) ?? Vector3.Zero;
    }

    public static bool IsBegin(float progress) => progress < 0.00001f;
    public static bool IsEnd(float progress) => progress > 0.99999f;
}