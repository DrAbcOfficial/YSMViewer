namespace YSMViewer.Models.Keyframes;

public enum EasingType
{
    Linear,
    InQuad, OutQuad, InOutQuad,
    InCubic, OutCubic, InOutCubic,
    InQuart, OutQuart, InOutQuart,
    InQuint, OutQuint, InOutQuint,
    InSine, OutSine, InOutSine,
    InExpo, OutExpo, InOutExpo,
    InCirc, OutCirc, InOutCirc,
    InBack, OutBack, InOutBack,
    InElastic, OutElastic, InOutElastic,
    InBounce, OutBounce, InOutBounce,
}

public static class Easing
{
    private const float Pi = MathF.PI;
    private const float HalfPi = MathF.PI / 2f;

    public static float Apply(EasingType type, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        switch (type)
        {
            case EasingType.Linear: return t;
            case EasingType.InQuad: return t * t;
            case EasingType.OutQuad: return t * (2f - t);
            case EasingType.InOutQuad: return t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;
            case EasingType.InCubic: return t * t * t;
            case EasingType.OutCubic: { float u = t - 1f; return u * u * u + 1f; }
            case EasingType.InOutCubic: return t < 0.5f ? 4f * t * t * t : (t - 1f) * (2f * t - 2f) * (2f * t - 2f) + 1f;
            case EasingType.InQuart: return t * t * t * t;
            case EasingType.OutQuart: { float u = t - 1f; return 1f - u * u * u * u; }
            case EasingType.InOutQuart: return t < 0.5f ? 8f * t * t * t * t : 1f - 8f * (t - 1f) * (t - 1f) * (t - 1f) * (t - 1f);
            case EasingType.InQuint: return t * t * t * t * t;
            case EasingType.OutQuint: { float u = t - 1f; return 1f + u * u * u * u * u; }
            case EasingType.InOutQuint: return t < 0.5f ? 16f * t * t * t * t * t : 1f + 16f * (t - 1f) * (t - 1f) * (t - 1f) * (t - 1f) * (t - 1f);
            case EasingType.InSine: return 1f - MathF.Cos(t * HalfPi);
            case EasingType.OutSine: return MathF.Sin(t * HalfPi);
            case EasingType.InOutSine: return -(MathF.Cos(Pi * t) - 1f) / 2f;
            case EasingType.InExpo: return t == 0f ? 0f : MathF.Pow(2f, 10f * t - 10f);
            case EasingType.OutExpo: return t == 1f ? 1f : 1f - MathF.Pow(2f, -10f * t);
            case EasingType.InOutExpo:
                return t == 0f ? 0f : t == 1f ? 1f : t < 0.5f
                    ? MathF.Pow(2f, 20f * t - 10f) / 2f
                    : (2f - MathF.Pow(2f, -20f * t + 10f)) / 2f;
            case EasingType.InCirc: return 1f - MathF.Sqrt(1f - t * t);
            case EasingType.OutCirc: return MathF.Sqrt(1f - (t - 1f) * (t - 1f));
            case EasingType.InOutCirc:
                return t < 0.5f
                    ? (1f - MathF.Sqrt(1f - 4f * t * t)) / 2f
                    : (MathF.Sqrt(1f - (t - 1f) * (t - 1f) * 4f) + 1f) / 2f;
            case EasingType.InBack: { float s = 1.70158f; return t * t * ((s + 1f) * t - s); }
            case EasingType.OutBack: { float s = 1.70158f; float u = t - 1f; return u * u * ((s + 1f) * u + s) + 1f; }
            case EasingType.InOutBack:
                {
                    float s = 1.70158f * 1.525f;
                    return t < 0.5f
                        ? (2f * t) * (2f * t) * ((s + 1f) * 2f * t - s) / 2f
                        : ((2f * t - 2f) * (2f * t - 2f) * ((s + 1f) * (2f * t - 2f) + s) + 2f) / 2f;
                }
            case EasingType.InElastic:
                return t == 0f ? 0f : t == 1f ? 1f
                    : -MathF.Pow(2f, 10f * t - 10f) * MathF.Sin((t * 10f - 10.75f) * 2f * Pi / 3f);
            case EasingType.OutElastic:
                return t == 0f ? 0f : t == 1f ? 1f
                    : MathF.Pow(2f, -10f * t) * MathF.Sin((t * 10f - 0.75f) * 2f * Pi / 3f) + 1f;
            case EasingType.InOutElastic:
                return t == 0f ? 0f : t == 1f ? 1f : t < 0.5f
                    ? -(MathF.Pow(2f, 20f * t - 10f) * MathF.Sin((20f * t - 11.125f) * 2f * Pi / 4.5f)) / 2f
                    : (MathF.Pow(2f, -20f * t + 10f) * MathF.Sin((20f * t - 11.125f) * 2f * Pi / 4.5f)) / 2f + 1f;
            case EasingType.InBounce: return 1f - ApplyBounce(1f - t);
            case EasingType.OutBounce: return ApplyBounce(t);
            case EasingType.InOutBounce:
                return t < 0.5f
                    ? (1f - ApplyBounce(1f - 2f * t)) / 2f
                    : (1f + ApplyBounce(2f * t - 1f)) / 2f;
            default: return t;
        }
    }

    private static float ApplyBounce(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;

        if (t < 1f / d1)
            return n1 * t * t;
        if (t < 2f / d1)
        { float u = t - 1.5f / d1; return n1 * u * u + 0.75f; }
        if (t < 2.5f / d1)
        { float u = t - 2.25f / d1; return n1 * u * u + 0.9375f; }
        { float u = t - 2.625f / d1; return n1 * u * u + 0.984375f; }
    }

    public static EasingType ParseEasingType(string? s)
    {
        if (string.IsNullOrEmpty(s)) return EasingType.Linear;
        return s.ToLowerInvariant() switch
        {
            "linear" => EasingType.Linear,
            "easeinquad" or "in_quad" => EasingType.InQuad,
            "easeoutquad" or "out_quad" => EasingType.OutQuad,
            "easeinoutquad" or "in_out_quad" => EasingType.InOutQuad,
            "easeincubic" or "in_cubic" => EasingType.InCubic,
            "easeoutcubic" or "out_cubic" => EasingType.OutCubic,
            "easeinoutcubic" or "in_out_cubic" => EasingType.InOutCubic,
            "easeinquart" or "in_quart" => EasingType.InQuart,
            "easeoutquart" or "out_quart" => EasingType.OutQuart,
            "easeinoutquart" or "in_out_quart" => EasingType.InOutQuart,
            "easeinquint" or "in_quint" => EasingType.InQuint,
            "easeoutquint" or "out_quint" => EasingType.OutQuint,
            "easeinoutquint" or "in_out_quint" => EasingType.InOutQuint,
            "easeinsine" or "in_sine" => EasingType.InSine,
            "easeoutsine" or "out_sine" => EasingType.OutSine,
            "easeinoutsine" or "in_out_sine" => EasingType.InOutSine,
            "easeinexpo" or "in_expo" => EasingType.InExpo,
            "easeoutexpo" or "out_expo" => EasingType.OutExpo,
            "easeinoutexpo" or "in_out_expo" => EasingType.InOutExpo,
            "easeincirc" or "in_circ" => EasingType.InCirc,
            "easeoutcirc" or "out_circ" => EasingType.OutCirc,
            "easeinoutcirc" or "in_out_circ" => EasingType.InOutCirc,
            "easeinback" or "in_back" => EasingType.InBack,
            "easeoutback" or "out_back" => EasingType.OutBack,
            "easeinoutback" or "in_out_back" => EasingType.InOutBack,
            "easeinelastic" or "in_elastic" => EasingType.InElastic,
            "easeoutelastic" or "out_elastic" => EasingType.OutElastic,
            "easeinoutelastic" or "in_out_elastic" => EasingType.InOutElastic,
            "easeinbounce" or "in_bounce" => EasingType.InBounce,
            "easeoutbounce" or "out_bounce" => EasingType.OutBounce,
            "easeinoutbounce" or "in_out_bounce" => EasingType.InOutBounce,
            _ => EasingType.Linear,
        };
    }
}