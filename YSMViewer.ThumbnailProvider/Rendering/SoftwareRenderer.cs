using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;
using System.Numerics;
using YSMViewer.Models.Document;

namespace YSMViewer.ThumbnailProvider.Rendering;

public sealed class ThumbnailRenderer : IDisposable
{
    private Image<Rgba32>? _texture;
    private Rgba32[]? _texPixels;
    private int _texW, _texH;

    public Image<Rgba32> Render(GeometryBuilder.ThumbnailScene scene, int size)
    {
        var sw = Stopwatch.StartNew();
        LoadTexture(scene.Texture);

        var cam = SetupCamera(scene.BoundsMin, scene.BoundsMax, size);
#if DEBUG
        Trace.WriteLine($"[Thumbnail] Camera eye={cam.Eye} center={0.5f * (scene.BoundsMin + scene.BoundsMax)} orthoScale={cam.OrthoScale}");
        Trace.WriteLine($"[Thumbnail] Texture={(_texture is null ? "none" : $"{_texW}x{_texH}")} Faces={scene.Faces.Count}");
#endif

        var image = new Image<Rgba32>(size, size, new Rgba32(0, 0, 0, 0));
        var depthBuffer = new float[size * size];
        Array.Fill(depthBuffer, float.MaxValue);

        int culled = 0, drawn = 0;
        foreach (var face in scene.Faces)
        {
            if (RasterizeFace(face, cam, size, image, depthBuffer))
                drawn++;
            else
                culled++;
        }

#if DEBUG
        Trace.WriteLine($"[Thumbnail] Drawn={drawn} Culled={culled} Time={sw.ElapsedMilliseconds}ms");
#endif
        return image;
    }

    private void LoadTexture(YsmTextureResource? texture)
    {
        _texture?.Dispose();
        _texture = null;
        _texPixels = null;
        _texW = _texH = 1;

        if (texture?.Data is { Length: > 0 })
        {
            try
            {
                _texture = Image.Load<Rgba32>(texture.Data);
                _texW = _texture.Width;
                _texH = _texture.Height;
                if (_texPixels is null || _texPixels.Length != _texW * _texH)
                    _texPixels = new Rgba32[_texW * _texH];
                _texture.CopyPixelDataTo(_texPixels);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Thumbnail] Texture load failed: {ex.Message}");
                _texture = null;
                _texPixels = null;
            }
        }
    }

    private static CameraData SetupCamera(Vector3 boundsMin, Vector3 boundsMax, int viewportSize)
    {
        var center = (boundsMin + boundsMax) * 0.5f;

        var forward = Vector3.Normalize(new Vector3(0f, 0.15f, 1f));
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var up = Vector3.Normalize(Vector3.Cross(right, forward));

        float viewMinX = float.MaxValue, viewMaxX = float.MinValue;
        float viewMinY = float.MaxValue, viewMaxY = float.MinValue;
        var corners = new[]
        {
            new Vector3(boundsMin.X, boundsMin.Y, boundsMin.Z),
            new Vector3(boundsMin.X, boundsMin.Y, boundsMax.Z),
            new Vector3(boundsMin.X, boundsMax.Y, boundsMin.Z),
            new Vector3(boundsMin.X, boundsMax.Y, boundsMax.Z),
            new Vector3(boundsMax.X, boundsMin.Y, boundsMin.Z),
            new Vector3(boundsMax.X, boundsMin.Y, boundsMax.Z),
            new Vector3(boundsMax.X, boundsMax.Y, boundsMin.Z),
            new Vector3(boundsMax.X, boundsMax.Y, boundsMax.Z),
        };
        foreach (var c in corners)
        {
            float vx = Vector3.Dot(c - center, right);
            float vy = Vector3.Dot(c - center, up);
            viewMinX = MathF.Min(viewMinX, vx);
            viewMaxX = MathF.Max(viewMaxX, vx);
            viewMinY = MathF.Min(viewMinY, vy);
            viewMaxY = MathF.Max(viewMaxY, vy);
        }
        float viewExtentX = viewMaxX - viewMinX;
        float viewExtentY = viewMaxY - viewMinY;

        float orthoScaleX = viewportSize / viewExtentX;
        float orthoScaleY = viewportSize / viewExtentY;
        float orthoScale = MathF.Min(orthoScaleX, orthoScaleY);

        float dist = (boundsMax - boundsMin).Length() * 1.5f;
        return new CameraData(center - forward * dist, right, up, forward, orthoScale);
    }

    private bool RasterizeFace(
        GeometryBuilder.TexturedFace face,
        CameraData cam,
        int vpSize,
        Image<Rgba32> image,
        float[] depthBuffer)
    {
        var nz = Vector3.Dot(face.WorldNormal, cam.Forward);
        if (nz <= 0f) return false;

        var p0 = ProjectVertex(face.P0, cam);
        var p1 = ProjectVertex(face.P1, cam);
        var p2 = ProjectVertex(face.P2, cam);
        var p3 = ProjectVertex(face.P3, cam);

        if (p0.Z < 0 || p1.Z < 0 || p2.Z < 0 || p3.Z < 0) return false;

        var s0 = ToScreen(p0, cam, vpSize);
        var s1 = ToScreen(p1, cam, vpSize);
        var s2 = ToScreen(p2, cam, vpSize);
        var s3 = ToScreen(p3, cam, vpSize);

        float light = ComputeLighting(face.WorldNormal);

        RasterizeTriangle(s0, s1, s2,
            face.U0, face.V0, face.U1, face.V1, face.U2, face.V2,
            light, vpSize, image, depthBuffer);
        RasterizeTriangle(s0, s2, s3,
            face.U0, face.V0, face.U2, face.V2, face.U3, face.V3,
            light, vpSize, image, depthBuffer);

        return true;
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
        return 0.55f + 0.55f * diff;
    }

    private void RasterizeTriangle(
        (float X, float Y, float Z) a,
        (float X, float Y, float Z) b,
        (float X, float Y, float Z) c,
        float ua, float va, float ub, float vb, float uc, float vc,
        float light, int vpSize, Image<Rgba32> image, float[] depthBuffer)
    {
        int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(MathF.Min(a.X, b.X), c.X)));
        int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(MathF.Min(a.Y, b.Y), c.Y)));
        int maxX = Math.Min(vpSize - 1, (int)MathF.Ceiling(MathF.Max(MathF.Max(a.X, b.X), c.X)));
        int maxY = Math.Min(vpSize - 1, (int)MathF.Ceiling(MathF.Max(MathF.Max(a.Y, b.Y), c.Y)));

        float area = EdgeFunc(a.X, a.Y, b.X, b.Y, c.X, c.Y);
        if (MathF.Abs(area) < 0.0001f) return;
        float invArea = 1f / area;

        if (!image.DangerousTryGetSinglePixelMemory(out var pixelMem))
            return;
        var pixels = pixelMem.Span;

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

                var color = SampleTexture(tu, tv, light);
                if (color.A == 0) continue;

                pixels[idx] = color;
            }
        }
    }

    private static float EdgeFunc(float ax, float ay, float bx, float by, float px, float py)
    {
        return (px - ax) * (by - ay) - (py - ay) * (bx - ax);
    }

    private Rgba32 SampleTexture(float u, float v, float light)
    {
        if (_texPixels is null)
            return new Rgba32(
                (byte)Math.Clamp((int)(230 * light), 0, 255),
                (byte)Math.Clamp((int)(230 * light), 0, 255),
                (byte)Math.Clamp((int)(230 * light), 0, 255),
                255);

        u -= MathF.Floor(u);
        v -= MathF.Floor(v);

        int tx = (int)(u * _texW) % _texW;
        int ty = (int)(v * _texH) % _texH;
        if (tx < 0) tx += _texW;
        if (ty < 0) ty += _texH;

        int texIdx = ty * _texW + tx;
        var pixel = _texPixels![texIdx];

        if (pixel.A < 128)
            return new Rgba32(0, 0, 0, 0);

        byte r = (byte)Math.Clamp((int)(pixel.R * light), 0, 255);
        byte g = (byte)Math.Clamp((int)(pixel.G * light), 0, 255);
        byte b = (byte)Math.Clamp((int)(pixel.B * light), 0, 255);

        return new Rgba32(r, g, b, 255);
    }

    public void Dispose()
    {
        _texture?.Dispose();
        _texture = null;
        _texPixels = null;
    }
}

internal sealed record CameraData(
    Vector3 Eye,
    Vector3 Right,
    Vector3 Up,
    Vector3 Forward,
    float OrthoScale);
