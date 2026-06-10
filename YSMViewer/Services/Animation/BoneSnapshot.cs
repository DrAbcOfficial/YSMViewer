using System.Numerics;

namespace YSMViewer.Services.Animation;

public sealed class BoneSnapshot
{
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; } = Quaternion.Identity;
    public Vector3 Scale { get; set; } = Vector3.One;
    public Vector3 PivotPosition { get; set; }

    public static BoneSnapshot Capture(IAnimatableBone bone) => new()
    {
        Position = bone.Position,
        Rotation = bone.RotationQuaternion,
        Scale = bone.Scale,
        PivotPosition = bone.PivotPosition,
    };

    public static BoneSnapshot BasePose(Vector3 basePosition, Vector3 baseEuler)
    {
        return new BoneSnapshot
        {
            Position = basePosition,
            Rotation = AnimationService.CreateBlockbenchQuaternion(baseEuler),
            Scale = Vector3.One,
        };
    }
}