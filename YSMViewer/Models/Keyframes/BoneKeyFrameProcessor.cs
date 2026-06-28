namespace YSMViewer.Models.Keyframes;

using YSMViewer.Models;

public static class BoneKeyFrameProcessor
{
    public static BoneKeyFrame[] FromKeyframeSet(MinecraftKeyframeSet kf)
    {
        if (kf.IsConstant)
            return Process([new RawBoneKeyFrame(0f, null, null, null, kf.ConstantValue, kf.ConstantValue, kf.ConstantValue, null, null)]);
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
                ExpandComponents(rawEntry.Post, out postX, out postY, out postZ);

                if (rawEntry.Pre is not null && rawEntry.Pre.Length > 0)
                    ExpandComponents(rawEntry.Pre, out preX, out preY, out preZ);
            }
            else
            {
                ExpandComponents(values, out postX, out postY, out postZ);
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

    private static void ExpandComponents(float[] values, out object? x, out object? y, out object? z)
    {
        switch (values.Length)
        {
            case 0:
                x = y = z = 0f;
                break;
            case 1:
                x = y = z = values[0];
                break;
            case 2:
                x = values[0];
                y = values[1];
                z = 0f;
                break;
            default:
                x = values[0];
                y = values[1];
                z = values[2];
                break;
        }
    }

    private static void ExpandComponents(object?[] values, out object? x, out object? y, out object? z)
    {
        switch (values.Length)
        {
            case 0:
                x = y = z = 0f;
                break;
            case 1:
                x = y = z = values[0];
                break;
            case 2:
                x = values[0];
                y = values[1];
                z = 0f;
                break;
            default:
                x = values[0];
                y = values[1];
                z = values[2];
                break;
        }
    }
}
