namespace YSMViewer.Models.Document;

public sealed record YsmAnimationControllerResource(
    string Name,
    byte[] Data);

public sealed record YsmSoundResource(
    string Name,
    byte[] Data);

public sealed record YsmFunctionResource(
    string Name,
    byte[] Data);