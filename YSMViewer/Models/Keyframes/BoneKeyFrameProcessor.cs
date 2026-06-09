namespace YSMViewer.Models.Keyframes;

using YSMViewer.Models;

public static class BoneKeyFrameProcessor
{
    public static BoneKeyFrame[] FromKeyframeSet(MinecraftKeyframeSet kf)
    {
        if (kf.IsConstant) return [];
        if (kf.Keyframes.Count == 0) return [];

        var sorted = kf.Keyframes.OrderBy(k => k.Key).ToList();
        var rawFrames = new List<RawBoneKeyFrame>(sorted.Count);

        foreach (var (time, values) in sorted.Select(kv => (kv.Key, kv.Value)))
        {
            kf.RawEntries.TryGetValue(time, out var rawEntry);
            string? lerpMode = rawEntry?.LerpMode;

            object? postX, postY, postZ;
            object? preX = null, preY = null, preZ = null;

            if (rawEntry is not null && rawEntry.Post.Length > 0)
            {
                postX = rawEntry.Post.Length > 0 ? rawEntry.Post[0] : 0f;
                postY = rawEntry.Post.Length > 1 ? rawEntry.Post[1] : 0f;
                postZ = rawEntry.Post.Length > 2 ? rawEntry.Post[2] : 0f;

                if (rawEntry.Pre is not null && rawEntry.Pre.Length >= 3)
                {
                    preX = rawEntry.Pre[0];
                    preY = rawEntry.Pre[1];
                    preZ = rawEntry.Pre[2];
                }
            }
            else
            {
                postX = values.Length > 0 ? values[0] : 0f;
                postY = values.Length > 1 ? values[1] : 0f;
                postZ = values.Length > 2 ? values[2] : 0f;
            }

            rawFrames.Add(new RawBoneKeyFrame(time, preX, preY, preZ, postX, postY, postZ, lerpMode, null));
        }

        return Process([.. rawFrames]);
    }
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
            float duration = r.Time > 0f ? r.Time : 1f;
            return [new LinearKeyFrame(0f, duration, pre, post) { EasingMode = easing }];
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
                startTime = 0f;
                duration = end.Time > 0f ? end.Time : 0.001f;
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
                        startTime, duration, pre, post, preControl, postControl)
                    { EasingMode = easing });
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