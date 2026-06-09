namespace YSMViewer.Models.Keyframes;

public static class BoneKeyFrameProcessor
{
    public static BoneKeyFrame[] Process(RawBoneKeyFrame[] rawFrames)
    {
        if (rawFrames.Length == 0) return [];

        var sorted = rawFrames.OrderBy(f => f.Time).ToArray();

        if (sorted.Length == 1)
        {
            var r = sorted[0];
            var post = new Vector3v(r.PostX, r.PostY, r.PostZ);
            var pre = HasPreValues(r) ? new Vector3v(r.PreX, r.PreY, r.PreZ) : post;
            var easing = Easing.ParseEasingType(r.Easing);
            return [new LinearKeyFrame(r.Time, float.MaxValue, pre, post) { EasingMode = easing }];
        }

        var frames = new List<BoneKeyFrame>(sorted.Length);

        for (int i = 0; i < sorted.Length; i++)
        {
            var end = sorted[i];

            string lerpMode;
            EasingType easing;

            if (i == 0)
            {
                lerpMode = end.LerpMode?.ToLowerInvariant() ?? "linear";
                easing = Easing.ParseEasingType(end.Easing);
            }
            else
            {
                var prev = sorted[i - 1];
                var endLerp = end.LerpMode?.ToLowerInvariant();
                if (endLerp == "catmullrom")
                    lerpMode = "catmullrom";
                else
                    lerpMode = prev.LerpMode?.ToLowerInvariant() ?? "linear";
                easing = Easing.ParseEasingType(end.Easing ?? prev.Easing);
            }

            float startTime;
            float duration;

            if (i == 0)
            {
                startTime = end.Time;
                duration = sorted[1].Time - end.Time;
            }
            else
            {
                startTime = sorted[i - 1].Time;
                duration = end.Time - sorted[i - 1].Time;
            }

            if (duration <= 0f) duration = 0.001f;

            if (i == 0)
            {
                var post = new Vector3v(end.PostX, end.PostY, end.PostZ);
                var pre = HasPreValues(end) ? new Vector3v(end.PreX, end.PreY, end.PreZ) : post;
                frames.Add(new LinearKeyFrame(startTime, duration, pre, post) { EasingMode = easing });
            }
            else
            {
                var beginFrame = sorted[i - 1];
                Vector3v pre = new(beginFrame.PostX, beginFrame.PostY, beginFrame.PostZ);
                Vector3v post = new(
                    end.PreX ?? end.PostX,
                    end.PreY ?? end.PostY,
                    end.PreZ ?? end.PostZ);

                if (lerpMode == "catmullrom")
                {
                    Vector3v preControl = i >= 2
                        ? new Vector3v(sorted[i - 2].PostX, sorted[i - 2].PostY, sorted[i - 2].PostZ)
                        : pre;

                    Vector3v postControl = i + 1 < sorted.Length
                        ? new Vector3v(
                            sorted[i + 1].PreX ?? sorted[i + 1].PostX,
                            sorted[i + 1].PreY ?? sorted[i + 1].PostY,
                            sorted[i + 1].PreZ ?? sorted[i + 1].PostZ)
                        : post;

                    frames.Add(new CatmullRomKeyFrame(
                        startTime, duration, pre, post, preControl, postControl) { EasingMode = easing });
                }
                else
                {
                    frames.Add(new LinearKeyFrame(startTime, duration, pre, post) { EasingMode = easing });
                }
            }
        }

        return [.. frames];
    }

    private static bool HasPreValues(RawBoneKeyFrame r)
    {
        return r.PreX is not null || r.PreY is not null || r.PreZ is not null;
    }
}