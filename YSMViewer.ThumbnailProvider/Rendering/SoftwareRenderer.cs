using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using YSMViewer.Models.Document;

namespace YSMViewer.Rendering.Thumbnail;

public sealed class ThumbnailRenderer : IDisposable
{
    private Bitmap? _texture;
    private int _texW, _texH;

    public Bitmap Render(GeometryBuilder.ThumbnailScene scene, int size)
    {
        LoadTexture(scene.Texture);

        var cam = SetupCamera(scene.BoundsMin, scene.BoundsMax, size);

        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        var depthBuffer = new float[size * size];
        Array.Fill(depthBuffer, float.MaxValue);

        var bmpData = bitmap.LockBits(
            new Rectangle(0, 0, size, size),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            foreach (var face in scene.Faces)
                RasterizeFace(face, cam, size, bmpData, depthBuffer);
        }
        finally
        {
            bitmap.UnlockBits(bmpData);
        }

        return bitmap;
    }

    private void LoadTexture(YsmTextureResource? texture)
    {
        _texture?.Dispose();
        _texture = null;
        _texW = _texH = 1;

        if (texture?.Data is { Length: > 0 })
        {
            try
            {
                using var ms = new MemoryStream(texture.Data);
                _texture = new Bitmap(ms);
                _texW = _texture.Width;
                _texH = _texture.Height;
            }
            catch
            {
                _texture = null;
            }
        }
    }

    private static CameraData SetupCamera(Vector3 boundsMin, Vector3 boundsMax, int viewportSize)
    {
        var center = (boundsMin + boundsMax) * 0.5f;
        var extent = boundsMax - boundsMin;
        float maxExtent = MathF.Max(MathF.Max(extent.X, extent.Y), extent.Z);
        if (maxExtent < 0.001f) maxExtent = 1f;

        var forward = Vector3.Normalize(new Vector3(1f, 0.55f, 1f));
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var up = Vector3.Normalize(Vector3.Cross(right, forward));

        float dist = maxExtent * 2.2f;
        var eye = center - forward * dist;

        float orthoHalf = maxExtent * 0.7f;
        float orthoScale = (viewportSize - 20f) / (orthoHalf * 2f);

        return new CameraData(eye, right, up, forward, orthoScale, maxExtent);
    }

    private void RasterizeFace(
        GeometryBuilder.TexturedFace face,
        CameraData cam,
        int vpSize,
        BitmapData bmpData,
        float[] depthBuffer)
    {
        var nz = Vector3.Dot(face.WorldNormal, cam.Forward);
        if (nz <= 0f) return;

        var p0 = ProjectVertex(face.P0, cam);
        var p1 = ProjectVertex(face.P1, cam);
        var p2 = ProjectVertex(face.P2, cam);
        var p3 = ProjectVertex(face.P3, cam);

        if (p0.Z < 0 || p1.Z < 0 || p2.Z < 0 || p3.Z < 0) return;

        var s0 = ToScreen(p0, cam, vpSize);
        var s1 = ToScreen(p1, cam, vpSize);
        var s2 = ToScreen(p2, cam, vpSize);
        var s3 = ToScreen(p3, cam, vpSize);

        float light = ComputeLighting(face.WorldNormal);

        RasterizeTriangle(s0, s1, s2,
            face.U0, face.V0, face.U1, face.V1, face.U2, face.V2,
            light, vpSize, bmpData, depthBuffer);
        RasterizeTriangle(s0, s2, s3,
            face.U0, face.V0, face.U2, face.V2, face.U3, face.V3,
            light, vpSize, bmpData, depthBuffer);
    }

    private static (float X, float Y, float Z) ProjectVertex(Vector3 worldPos, CameraData cam)
    {
        var rel = worldPos - cam.Eye;
        return (
            Vector3.Dot(rel, cam.Right),
            Vector3.Dot(rel, cam.Up),
            Vector3.Dot(rel, cam.Forward)
        );
    }

    private static (float X, float Y, float Z) ToScreen(
        (float X, float Y, float Z) viewPos, CameraData cam, int vpSize)
    {
        float sx = viewPos.X * cam.OrthoScale + vpSize * 0.5f;
        float sy = viewPos.Y * cam.OrthoScale + vpSize * 0.5f;
        sy = vpSize - 1f - sy;
        return (sx, sy, viewPos.Z);
    }

    private static float ComputeLighting(Vector3 worldNormal)
    {
        var lightDir = Vector3.Normalize(new Vector3(-1f, 1f, -1f));
        float diff = MathF.Max(0f, Vector3.Dot(Vector3.Normalize(worldNormal), lightDir));
        return 0.25f + 0.75f * diff;
    }

    private void RasterizeTriangle(
        (float X, float Y, float Z) a,
        (float X, float Y, float Z) b,
        (float X, float Y, float Z) c,
        float ua, float va, float ub, float vb, float uc, float vc,
        float light, int vpSize, BitmapData bmpData, float[] depthBuffer)
    {
        int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(MathF.Min(a.X, b.X), c.X)));
        int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(MathF.Min(a.Y, b.Y), c.Y)));
        int maxX = Math.Min(vpSize - 1, (int)MathF.Ceiling(MathF.Max(MathF.Max(a.X, b.X), c.X)));
        int maxY = Math.Min(vpSize - 1, (int)MathF.Ceiling(MathF.Max(MathF.Max(a.Y, b.Y), c.Y)));

        float area = EdgeFunc(a.X, a.Y, b.X, b.Y, c.X, c.Y);
        if (MathF.Abs(area) < 0.0001f) return;
        float invArea = 1f / area;

        for (int py = minY; py <= maxY; py++)
        {
            int rowStart = py * vpSize;
            for (int px = minX; px <= maxX; px++)
            {
                float w0 = EdgeFunc(b.X, b.Y, c.X, c.Y, px + 0.5f, py + 0.5f);
                float w1 = EdgeFunc(c.X, c.Y, a.X, a.Y, px + 0.5f, py + 0.5f);
                float w2 = EdgeFunc(a.X, a.Y, b.X, b.Y, px + 0.5f, py + 0.5f);

                if (w0 < 0 || w1 < 0 || w2 < 0) continue;

                float alpha = w0 * invArea;
                float beta = w1 * invArea;
                float gamma = w2 * invArea;

                float depth = alpha * a.Z + beta * b.Z + gamma * c.Z;
                int idx = rowStart + px;

                if (depth >= depthBuffer[idx]) continue;
                depthBuffer[idx] = depth;

                float tu = alpha * ua + beta * ub + gamma * uc;
                float tv = alpha * va + beta * vb + gamma * vc;

                Color color = SampleTexture(tu, tv, light);
                if (color.A < 5) continue;

                int pixelOffset = py * bmpData.Stride + px * 4;
                unsafe
                {
                    byte* ptr = (byte*)bmpData.Scan0 + pixelOffset;
                    ptr[0] = color.B;
                    ptr[1] = color.G;
                    ptr[2] = color.R;
                    ptr[3] = 255;
                }
            }
        }
    }

    private static float EdgeFunc(float ax, float ay, float bx, float by, float px, float py)
    {
        return (px - ax) * (by - ay) - (py - ay) * (bx - ax);
    }

    private Color SampleTexture(float u, float v, float light)
    {
        if (_texture is null)
            return Color.FromArgb((int)(200 * light), (int)(200 * light), (int)(200 * light));

        u = u - MathF.Floor(u);
        v = v - MathF.Floor(v);

        int tx = (int)(u * _texW) % _texW;
        int ty = (int)(v * _texH) % _texH;
        if (tx < 0) tx += _texW;
        if (ty < 0) ty += _texH;

        var pixel = _texture.GetPixel(tx, ty);

        if (pixel.A < 128)
            return Color.Transparent;

        int r = Math.Clamp((int)(pixel.R * light), 0, 255);
        int g = Math.Clamp((int)(pixel.G * light), 0, 255);
        int b = Math.Clamp((int)(pixel.B * light), 0, 255);

        return Color.FromArgb(255, r, g, b);
    }

    public void Dispose()
    {
        _texture?.Dispose();
        _texture = null;
    }
}

internal sealed record CameraData(
    Vector3 Eye,
    Vector3 Right,
    Vector3 Up,
    Vector3 Forward,
    float OrthoScale,
    float MaxExtent);
