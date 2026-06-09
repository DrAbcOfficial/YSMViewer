namespace YSMViewer.Models.Keyframes;

using System.Numerics;
using YSMViewer.Services.Molang;

public sealed record BezierKeyFrame(
    float StartTime,
    float Duration,
    Vector3v Pre,
    Vector3v Post,
    Vector3v HandleRight,
    Vector3v HandleLeft
) : BoneKeyFrame(StartTime, Duration)
{
    public override Vector3v? PreValue => Pre;
    public override Vector3v? PostValue => Post;

    public override Vector3 Evaluate(MolangService molang, float progress)
    {
        if (IsBegin(progress))
            return Pre.Evaluate(molang);

        if (IsEnd(progress))
            return Post.Evaluate(molang);

        float t = Easing.Apply(EasingMode, progress);

        var p0 = Pre.Evaluate(molang);
        var p1 = HandleRight.Evaluate(molang);
        var p2 = HandleLeft.Evaluate(molang);
        var p3 = Post.Evaluate(molang);

        float u = 1f - t;
        float u2 = u * u;
        float u3 = u2 * u;
        float t2 = t * t;
        float t3 = t2 * t;

        return p0 * u3 + p1 * (3f * u2 * t) + p2 * (3f * u * t2) + p3 * t3;
    }
}