using Aura3D.Core.Nodes;
using System.Numerics;
using YSMViewer.Services;

namespace YSMViewer.Rendering.Aura3D;

internal sealed class Aura3DBoneNode(Node node) : IAnimatableBone
{
    public Vector3 Position
    {
        get => node.Position;
        set => node.Position = value;
    }

    public Quaternion RotationQuaternion
    {
        get => node.RotationQuaternion;
        set => node.RotationQuaternion = value;
    }

    public Vector3 Scale
    {
        get => node.Scale;
        set => node.Scale = value;
    }

    public Vector3 PivotPosition { get; set; }
}
