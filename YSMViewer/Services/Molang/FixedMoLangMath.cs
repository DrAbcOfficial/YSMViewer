using ConcreteMC.MolangSharp.Runtime;
using ConcreteMC.MolangSharp.Runtime.Struct;

namespace YSMViewer.Services.Molang;

public static class FixedMoLangMath
{
    public static readonly QueryStruct Library = Create();

    private static QueryStruct Create()
    {
        var functions = new Dictionary<string, Func<MoParams, object>>(StringComparer.OrdinalIgnoreCase)
        {
            ["abs"] = p => Math.Abs(p.GetDouble(0)),
            ["acos"] = p => Math.Acos(p.GetDouble(0)),
            ["sin"] = p => Math.Sin(p.GetDouble(0) * Math.PI / 180.0),
            ["asin"] = p => Math.Asin(p.GetDouble(0)),
            ["atan"] = p => Math.Atan(p.GetDouble(0)),
            ["atan2"] = p => Math.Atan2(p.GetDouble(0), p.GetDouble(1)),
            ["ceil"] = p => Math.Ceiling(p.GetDouble(0)),
            ["clamp"] = p => Math.Min(p.GetDouble(2), Math.Max(p.GetDouble(1), p.GetDouble(0))),
            ["cos"] = p => Math.Cos(p.GetDouble(0) * Math.PI / 180.0),
            ["die_roll"] = p => MoLangMath.DieRoll(p.GetDouble(0), p.GetDouble(1), p.GetDouble(2)),
            ["die_roll_integer"] = p => (double)MoLangMath.DieRollInt(p.GetInt(0), p.GetInt(1), p.GetInt(2)),
            ["exp"] = p => Math.Exp(p.GetDouble(0)),
            ["mod"] = p => p.GetDouble(0) % p.GetDouble(1),
            ["floor"] = p => Math.Floor(p.GetDouble(0)),
            ["hermite_blend"] = p =>
            {
                double v = p.GetDouble(0);
                return 3.0 * v * v - 2.0 * v * v * v;
            },
            ["lerp"] = p => MoLangMath.Lerp(p.GetDouble(0), p.GetDouble(1), p.GetDouble(2)),
            ["lerp_rotate"] = p => MoLangMath.LerpRotate(p.GetDouble(0), p.GetDouble(1), p.GetDouble(2)),
            ["ln"] = p => Math.Log(p.GetDouble(0)),
            ["max"] = p => Math.Max(p.GetDouble(0), p.GetDouble(1)),
            ["min"] = p => Math.Min(p.GetDouble(0), p.GetDouble(1)),
            ["pi"] = _ => Math.PI,
            ["pow"] = p => Math.Pow(p.GetDouble(0), p.GetDouble(1)),
            ["random"] = p => MoLangMath.Random(p.GetDouble(0), p.GetDouble(1)),
            ["random_integer"] = p => MoLangMath.RandomInt(p.GetInt(0), p.GetInt(1)),
            ["round"] = p => Math.Round(p.GetDouble(0)),
            ["sqrt"] = p => Math.Sqrt(p.GetDouble(0)),
            ["trunc"] = p => Math.Truncate(p.GetDouble(0)),
        };

        return new QueryStruct(functions);
    }
}