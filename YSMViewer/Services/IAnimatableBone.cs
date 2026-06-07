using System.Numerics;

namespace YSMViewer.Services;

public interface IAnimatableBone
{
    Vector3 Position { get; set; }
    Quaternion RotationQuaternion { get; set; }
    Vector3 Scale { get; set; }
}
