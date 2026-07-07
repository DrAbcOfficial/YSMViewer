using System.Numerics;

namespace YSMViewer.Models.Keyframes;

/// <summary>
/// Linear keyframe evaluation utilities shared by the simple animation
/// playback path (<see cref="AnimationService"/>) and the fallback branch of
/// the advanced path. Operates on a <see cref="MinecraftKeyframeSet"/> whose
/// <see cref="MinecraftKeyframeSet.HasMolangExpressions"/> and
/// <see cref="MinecraftKeyframeSet.HasAdvancedInterpolation"/> flags are
/// already known to be false; callers must dispatch advanced sets themselves.
/// </summary>
public static class KeyframeEvaluator
{
    /// <summary>
    /// Evaluates a non-advanced keyframe set at <paramref name="time"/> using
    /// piecewise-linear interpolation. Returns a sanitized <see cref="Vector3"/>
    /// (NaN/Infinity components replaced with 0).
    /// </summary>
    public static Vector3 EvaluateLinear(MinecraftKeyframeSet kf, float time)
    {
        if (kf.IsConstant)
            return new Vector3(kf.ConstantValue);

        if (kf.Keyframes.Count == 0)
            return Vector3.Zero;

        var sorted = kf.Keyframes.OrderBy(kv => kv.Key).ToList();

        if (time <= sorted[0].Key)
            return Sanitize(ToVector3(sorted[0].Value));

        if (time >= sorted[^1].Key)
            return Sanitize(ToVector3(sorted[^1].Value));

        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (time >= sorted[i].Key && time <= sorted[i + 1].Key)
            {
                float span = sorted[i + 1].Key - sorted[i].Key;
                float t = span > 0f ? (time - sorted[i].Key) / span : 0f;
                var a = ToVector3(sorted[i].Value);
                var b = ToVector3(sorted[i + 1].Value);
                return Sanitize(Vector3.Lerp(a, b, t));
            }
        }

        return Sanitize(ToVector3(sorted[^1].Value));
    }

    /// <summary>
    /// Converts a Bedrock keyframe value array (length 0..3) to a
    /// <see cref="Vector3"/>, padding missing components with zero.
    /// </summary>
    public static Vector3 ToVector3(float[] values)
    {
        if (values.Length == 0) return Vector3.Zero;
        if (values.Length == 1) return new Vector3(values[0]);
        if (values.Length == 2) return new Vector3(values[0], values[1], 0);
        return new Vector3(values[0], values[1], values[2]);
    }

    /// <summary>
    /// Replaces NaN/Infinity components with 0 to keep downstream math stable.
    /// </summary>
    public static Vector3 Sanitize(Vector3 v)
    {
        return new Vector3(
            float.IsNaN(v.X) || float.IsInfinity(v.X) ? 0f : v.X,
            float.IsNaN(v.Y) || float.IsInfinity(v.Y) ? 0f : v.Y,
            float.IsNaN(v.Z) || float.IsInfinity(v.Z) ? 0f : v.Z);
    }
}
