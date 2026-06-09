using System.Numerics;
using YSMViewer.Services.Molang;

namespace YSMViewer.Services.Animation;

public sealed class AnimationContext
{
    public string ControllerName { get; set; } = "";

    public float AnimTime { get; set; }

    public float DeltaTime { get; set; }

    public float AnimLength { get; set; }

    public bool IsMoving { get; set; }

    public required MolangService Molang { get; init; }

    public required IReadOnlyDictionary<string, Models.MinecraftAnimation> Animations { get; init; }

    public required IReadOnlyDictionary<string, IAnimatableBone> BoneNodes { get; init; }

    public required IReadOnlyDictionary<string, Vector3> BasePositions { get; init; }
    public required IReadOnlyDictionary<string, Vector3> BaseEulers { get; init; }
}