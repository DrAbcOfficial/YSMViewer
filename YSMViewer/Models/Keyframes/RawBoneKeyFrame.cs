namespace YSMViewer.Models.Keyframes;

public sealed record RawBoneKeyFrame(
    float Time,
    object? PreX,
    object? PreY,
    object? PreZ,
    object? PostX,
    object? PostY,
    object? PostZ,
    string? LerpMode,
    string? Easing
);