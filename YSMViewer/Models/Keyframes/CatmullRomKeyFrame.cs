namespace YSMViewer.Models.Keyframes;

using System.Numerics;
using YSMViewer.Services.Molang;

public sealed record CatmullRomKeyFrame(
    float StartTime,
    float Duration,
    Vector3v Pre,
    Vector3v Post,
    Vector3v PreControl,
    Vector3v PostControl
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

        var p0 = PreControl.Evaluate(molang);
        var p1 = Pre.Evaluate(molang);
        var p2 = Post.Evaluate(molang);
        var p3 = PostControl.Evaluate(molang);

        float t2 = t * t;
        float t3 = t2 * t;

        return (p1 * 2f
                + (p2 - p0) * t
                + (p0 * 2f - p1 * 5f + p2 * 4f - p3) * t2
                + (-p0 + p1 * 3f - p2 * 3f + p3) * t3) * 0.5f;
    }
}