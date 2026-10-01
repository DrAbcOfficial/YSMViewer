using System.Numerics;

namespace YSMViewer.Models;

/// <summary>
/// Shared per-face UV mapping used by the desktop, browser and thumbnail
/// renderers: normalizes a face's uv/uv_size against the texture size and
/// applies the Bedrock 1.21+ <c>uv_rotation</c> corner cycling exactly like
/// Blockbench's cube preview (each 90° step moves TL→TR→BR→BL).
/// </summary>
public static class CubeFaceUvMapper
{
    /// <summary>Corner UVs for one face, ordered (v0..v3) as the quad vertices expect.</summary>
    public static (float U0, float V0, float U1, float V1, float U2, float V2, float U3, float V3) GetFaceUv(
        MinecraftCubeFaceUV? faceUv, float texW, float texH)
    {
        if (faceUv?.UvCoords is not { Count: >= 2 })
            return (0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);

        float fu = faceUv.UvCoords[0];
        float fv = faceUv.UvCoords[1];
        float du = faceUv.UvSize is { Count: >= 2 } ? faceUv.UvSize[0] : 0f;
        float dv = faceUv.UvSize is { Count: >= 2 } ? faceUv.UvSize[1] : 0f;

        float u0 = fu / texW;
        float v0 = fv / texH;
        float u1 = (fu + du) / texW;
        float v1 = (fv + dv) / texH;

        Vector2 tl = new(u0, v0);
        Vector2 tr = new(u1, v0);
        Vector2 bl = new(u0, v1);
        Vector2 br = new(u1, v1);

        int rotationSteps = (((faceUv.UvRotation ?? 0) / 90) % 4 + 4) % 4;
        (Vector2 a, Vector2 b, Vector2 c, Vector2 d) = rotationSteps switch
        {
            1 => (bl, tl, br, tr),
            2 => (br, bl, tr, tl),
            3 => (tr, br, tl, bl),
            _ => (tl, tr, bl, br),
        };

        return (a.X, a.Y, b.X, b.Y, c.X, c.Y, d.X, d.Y);
    }
}
