namespace YSMViewer.Models;

/// <summary>
/// Bedrock/Minecraft unit conventions shared across the animation and molang
/// subsystems. Bedrock positions are expressed in pixels (16 per block) while
/// the GLTF/Aura3D scene uses block units, so conversions multiply or divide
/// by <see cref="BedrockPixelsPerBlock"/>.
/// </summary>
public static class BedrockUnits
{
    public const float PixelsPerBlock = 16f;
}
