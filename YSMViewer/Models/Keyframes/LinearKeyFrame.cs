namespace YSMViewer.Models.Keyframes;

using System.Numerics;
using YSMViewer.Services.Molang;

public sealed record LinearKeyFrame(
    float StartTime,
    float Duration,
    Vector3v Pre,
    Vector3v Post
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
        return Vector3.Lerp(Pre.Evaluate(molang), Post.Evaluate(molang), t);
    }
}