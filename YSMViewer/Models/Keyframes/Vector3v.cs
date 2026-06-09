namespace YSMViewer.Models.Keyframes;

using System.Globalization;
using System.Numerics;
using System.Text.Json;
using YSMViewer.Services.Molang;

public readonly struct Vector3v
{
    private readonly object? _x;
    private readonly object? _y;
    private readonly object? _z;

    public bool IsStatic { get; }

    public Vector3v(object? x, object? y, object? z)
    {
        _x = x;
        _y = y;
        _z = z;
        IsStatic = IsStaticComponent(x) && IsStaticComponent(y) && IsStaticComponent(z);
    }

    private static bool IsStaticComponent(object? c)
    {
        if (c is null) return true;
        if (c is float or double) return true;
        if (c is string s) return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        return false;
    }

    public Vector3 Evaluate(MolangService molang)
    {
        return new Vector3(
            EvalComponent(_x, molang),
            EvalComponent(_y, molang),
            EvalComponent(_z, molang));
    }

    private static float EvalComponent(object? component, MolangService molang)
    {
        if (component is null)
            return 0f;

        if (component is float f)
            return f;

        if (component is double d)
            return (float)d;

        if (component is string s)
        {
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                return parsed;

            var expr = molang.Parse(s);
            return molang.Evaluate(expr);
        }

        return 0f;
    }

    public static Vector3v FromArray(float[] values)
    {
        return values.Length switch
        {
            0 => new(0f, 0f, 0f),
            1 => new(values[0], values[0], values[0]),
            2 => new(values[0], values[1], 0f),
            _ => new(values[0], values[1], values[2]),
        };
    }

    public static Vector3v FromJson(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => new(element.GetDouble(), null, null),
            JsonValueKind.String => new(element.GetString(), null, null),
            JsonValueKind.Array =>
                FromJsonArray(element.EnumerateArray().ToList()),
            _ => default,
        };
    }

    private static Vector3v FromJsonArray(List<JsonElement> arr)
    {
        return arr.Count switch
        {
            0 => new(0f, 0f, 0f),
            1 => new(GetComponent(arr[0]), GetComponent(arr[0]), GetComponent(arr[0])),
            2 => new(GetComponent(arr[0]), GetComponent(arr[1]), 0f),
            _ => new(GetComponent(arr[0]), GetComponent(arr[1]), GetComponent(arr[2])),
        };
    }

    private static object? GetComponent(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Number => e.GetDouble(),
        JsonValueKind.String => e.GetString(),
        _ => 0f,
    };
}