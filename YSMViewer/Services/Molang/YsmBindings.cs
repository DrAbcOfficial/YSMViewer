using ConcreteMC.MolangSharp.Runtime;
using ConcreteMC.MolangSharp.Runtime.Struct;
using System.Numerics;
using YSMViewer.Models;

namespace YSMViewer.Services.Molang;

internal static class YsmBindings
{
    public static IMoStruct CreateYsmStruct(MolangService service)
    {
        var functions = new Dictionary<string, Func<MoParams, object>>(StringComparer.OrdinalIgnoreCase)
        {
            ["bone_rot"] = p => QueryBoneRotation(service, p),
            ["bone_pos"] = p => QueryBonePosition(service, p),
            ["bone_scale"] = p => QueryBoneScale(service, p),
            ["bone_pivot_abs"] = p => QueryBonePivotAbs(service, p),
            ["perlin_noise"] = p => PhysicsSimulator.PerlinNoise(
                    p.GetDouble(0),
                    p.GetDouble(1),
                    p.Contains(2) ? p.GetDouble(2) : 0.0,
                    p.Contains(3) ? p.GetDouble(3) : 0.0),
            ["first_order"] = p => FirstOrder(service, p),
            ["second_order"] = p => SecondOrder(service, p),
            ["play_sound"] = p => { service.AudioHost?.PlaySound(p.GetString(0)); return 0.0; },
            ["stop_sound"] = p => { service.AudioHost?.StopSound(p.GetString(0)); return 0.0; },
            ["stop_all_sounds"] = _ => { service.AudioHost?.StopAllSounds(); return 0.0; },
            ["keyboard"] = p => service.SafeGetUserVar($"keyboard_{p.GetString(0)}"),
            ["mouse"] = p => service.SafeGetUserVar($"mouse_{p.GetString(0)}"),
        };

        return new QueryStruct(functions);
    }

    private static double FirstOrder(MolangService service, MoParams p)
    {
        if (!p.Contains(0) || !p.Contains(1)) return 0.0;
        var id = p.GetString(0);
        if (string.IsNullOrEmpty(id)) return 0.0;

        return service.Physics.FirstOrder(
            id,
            p.GetDouble(1),
            p.Contains(2) ? p.GetDouble(2) : 1.0,
            0.0,
            0.0);
    }

    private static double SecondOrder(MolangService service, MoParams p)
    {
        if (!p.Contains(0) || !p.Contains(1)) return 0.0;
        var id = p.GetString(0);
        if (string.IsNullOrEmpty(id)) return 0.0;

        var input = p.GetDouble(1);
        return service.Physics.SecondOrder(
            id,
            input,
            p.Contains(2) ? p.GetDouble(2) : 1.0,
            p.Contains(3) ? p.GetDouble(3) : 1.0,
            p.Contains(4) ? p.GetDouble(4) : 1.0,
            input);
    }

    private static double QueryBoneRotation(MolangService service, MoParams p)
    {
        if (service.BoneNodes is null) return 0.0;
        var boneName = p.GetString(0);
        if (!service.BoneNodes.TryGetValue(boneName, out var bone)) return 0.0;

        var euler = ToBedrockEulerDegrees(bone.RotationQuaternion);
        return p.Contains(1) ? GetAxis(euler, p.GetInt(1)) : euler.Y;
    }

    private static double QueryBonePosition(MolangService service, MoParams p)
    {
        if (service.BoneNodes is null) return 0.0;
        var boneName = p.GetString(0);
        if (!service.BoneNodes.TryGetValue(boneName, out var bone)) return 0.0;

        var basePosition = service.BasePositions is not null && service.BasePositions.TryGetValue(boneName, out var basePos)
            ? basePos
            : Vector3.Zero;
        var delta = bone.Position - basePosition;
        var bedrockPosition = new Vector3(-delta.X * BedrockUnits.PixelsPerBlock, delta.Y * BedrockUnits.PixelsPerBlock, delta.Z * BedrockUnits.PixelsPerBlock);
        return p.Contains(1)
            ? GetAxis(bedrockPosition, p.GetInt(1))
            : bedrockPosition.Length();
    }

    private static double QueryBoneScale(MolangService service, MoParams p)
    {
        if (service.BoneNodes is null) return 0.0;
        var boneName = p.GetString(0);
        if (!service.BoneNodes.TryGetValue(boneName, out var bone)) return 0.0;

        return p.Contains(1)
            ? GetAxis(bone.Scale, p.GetInt(1))
            : (bone.Scale.X + bone.Scale.Y + bone.Scale.Z) / 3.0;
    }

    private static double QueryBonePivotAbs(MolangService service, MoParams p)
    {
        if (service.BoneNodes is null) return 0.0;
        var boneName = p.GetString(0);
        if (!service.BoneNodes.TryGetValue(boneName, out var bone)) return 0.0;

        Vector3 pivot = bone.PivotPosition;
        return p.Contains(1) ? GetAxis(pivot, p.GetInt(1)) : pivot.Length();
    }

    private static float GetAxis(Vector3 v, int axis) => axis switch
    {
        0 => v.X,
        1 => v.Y,
        2 => v.Z,
        _ => 0f
    };

    private static Vector3 ToBedrockEulerDegrees(Quaternion q)
    {
        float sinrCosp = 2f * (q.W * q.X + q.Y * q.Z);
        float cosrCosp = 1f - 2f * (q.X * q.X + q.Y * q.Y);
        float roll = MathF.Atan2(sinrCosp, cosrCosp);

        float sinp = 2f * (q.W * q.Y - q.Z * q.X);
        float pitch = MathF.Abs(sinp) >= 1f
            ? MathF.CopySign(MathF.PI / 2f, sinp)
            : MathF.Asin(sinp);

        float sinyCosp = 2f * (q.W * q.Z + q.X * q.Y);
        float cosyCosp = 1f - 2f * (q.Y * q.Y + q.Z * q.Z);
        float yaw = MathF.Atan2(sinyCosp, cosyCosp);

        float toDegrees = 180f / MathF.PI;
        return new Vector3(
            -roll * toDegrees,
            -pitch * toDegrees,
            yaw * toDegrees);
    }
}
