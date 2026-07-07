using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using YSMViewer.Models.Document;

namespace YSMViewer.ThumbnailProvider.Rendering;

public sealed unsafe class ThumbnailRenderer : IDisposable
{
    private byte[]? _texPixels;
    private GCHandle _texPixelsHandle;
    private byte* _texPixelsPtr;
    private int _texW, _texH;
    private YsmTextureResource? _loadedTexture;

    public byte[] Render(GeometryBuilder.ThumbnailScene scene, int size)
    {
        var cam = SetupCamera(scene.BoundsMin, scene.BoundsMax, size);

        int pixelCount = size * size;
        var image = new byte[pixelCount * 4];
        int depthLen = pixelCount;
        var depthBuffer = ArrayPool<float>.Shared.Rent(depthLen);
        try
        {
            new Span<float>(depthBuffer, 0, depthLen).Fill(float.MaxValue);

            fixed (byte* pixelsBase = image)
            {
                foreach (var face in scene.Faces)
                {
                    if (!ReferenceEquals(_loadedTexture, face.Texture))
                        LoadTexture(face.Texture);
                    RasterizeFace(face, cam, size, pixelsBase, depthBuffer);
                }
            }
        }
        finally
        {
            ArrayPool<float>.Shared.Return(depthBuffer);
        }

        return image;
    }

    private void LoadTexture(YsmTextureResource? texture)
    {
        if (_texPixelsHandle.IsAllocated)
        {
            _texPixelsHandle.Free();
            _texPixelsHandle = default;
        }
        _texPixels = null;
        _texPixelsPtr = null;
        _texW = _texH = 1;
        _loadedTexture = texture;

        if (texture?.Data is { Length: > 0 })
        {
            try
            {
                using var img = Image.Load<Rgba32>(texture.Data);
                _texW = img.Width;
                _texH = img.Height;
                _texPixels = new byte[_texW * _texH * 4];

                for (int y = 0; y < _texH; y++)
                {
                    var row = img.DangerousGetPixelRowMemory(y).Span;
                    int rowOffset = y * _texW * 4;
                    for (int x = 0; x < _texW; x++)
                    {
                        var pixel = row[x];
                        int offset = rowOffset + x * 4;
                        _texPixels[offset] = pixel.B;
                        _texPixels[offset + 1] = pixel.G;
                        _texPixels[offset + 2] = pixel.R;
                        _texPixels[offset + 3] = pixel.A;
                    }
                }

                _texPixelsHandle = GCHandle.Alloc(_texPixels, GCHandleType.Pinned);
                _texPixelsPtr = (byte*)_texPixelsHandle.AddrOfPinnedObject();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThumbnailRenderer] Failed to load texture: {ex.Message}");
                _texPixels = null;
                _texPixelsPtr = null;
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
        Span<Vector3> corners = stackalloc Vector3[8];
        corners[0] = new Vector3(boundsMin.X, boundsMin.Y, boundsMin.Z);
        corners[1] = new Vector3(boundsMin.X, boundsMin.Y, boundsMax.Z);
        corners[2] = new Vector3(boundsMin.X, boundsMax.Y, boundsMin.Z);
        corners[3] = new Vector3(boundsMin.X, boundsMax.Y, boundsMax.Z);
        corners[4] = new Vector3(boundsMax.X, boundsMin.Y, boundsMin.Z);
        corners[5] = new Vector3(boundsMax.X, boundsMin.Y, boundsMax.Z);
        corners[6] = new Vector3(boundsMax.X, boundsMax.Y, boundsMin.Z);
        corners[7] = new Vector3(boundsMax.X, boundsMax.Y, boundsMax.Z);
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

    private void RasterizeFace(
        GeometryBuilder.TexturedFace face,
        CameraData cam,
        int vpSize,
        byte* image,
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

        RasterizeTriangle(s0, s1, s2,
            face.U0, face.V0, face.U1, face.V1, face.U2, face.V2,
            vpSize, image, depthBuffer);
        RasterizeTriangle(s0, s2, s3,
            face.U0, face.V0, face.U2, face.V2, face.U3, face.V3,
            vpSize, image, depthBuffer);
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

    private void RasterizeTriangle(
        (float X, float Y, float Z) a,
        (float X, float Y, float Z) b,
        (float X, float Y, float Z) c,
        float ua, float va, float ub, float vb, float uc, float vc,
        int vpSize, byte* image, float[] depthBuffer)
    {
        int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(MathF.Min(a.X, b.X), c.X)));
        int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(MathF.Min(a.Y, b.Y), c.Y)));
        int maxX = Math.Min(vpSize - 1, (int)MathF.Ceiling(MathF.Max(MathF.Max(a.X, b.X), c.X)));
        int maxY = Math.Min(vpSize - 1, (int)MathF.Ceiling(MathF.Max(MathF.Max(a.Y, b.Y), c.Y)));

        float area = EdgeFunc(a.X, a.Y, b.X, b.Y, c.X, c.Y);
        if (MathF.Abs(area) < 0.0001f) return;
        float invArea = 1f / area;

        fixed (float* depthBase = depthBuffer)
        {
            for (int py = minY; py <= maxY; py++)
            {
                int rowStart = py * vpSize;
                float* depthRow = depthBase + rowStart;
                byte* pixelRow = image + rowStart * 4;
                for (int px = minX; px <= maxX; px++)
                {
                    float w0 = EdgeFunc(b.X, b.Y, c.X, c.Y, px + 0.5f, py + 0.5f);
                    float w1 = EdgeFunc(c.X, c.Y, a.X, a.Y, px + 0.5f, py + 0.5f);
                    float w2 = EdgeFunc(a.X, a.Y, b.X, b.Y, px + 0.5f, py + 0.5f);

                    if (area > 0f)
                    {
                        if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                    }
                    else
                    {
                        if (w0 > 0 || w1 > 0 || w2 > 0) continue;
                    }

                    float alpha = w0 * invArea;
                    float beta = w1 * invArea;
                    float gamma = w2 * invArea;

                    float depth = alpha * a.Z + beta * b.Z + gamma * c.Z;

                    if (depth >= depthRow[px]) continue;

                    float tu = alpha * ua + beta * ub + gamma * uc;
                    float tv = alpha * va + beta * vb + gamma * vc;

                    var color = SampleTexture(tu, tv);
                    if (color == 0) continue;
                    depthRow[px] = depth;

                    byte* d = pixelRow + px * 4;
                    d[0] = (byte)(color);
                    d[1] = (byte)(color >> 8);
                    d[2] = (byte)(color >> 16);
                    d[3] = (byte)(color >> 24);
                }
            }
        }
    }

    private static float EdgeFunc(float ax, float ay, float bx, float by, float px, float py)
    {
        return (px - ax) * (by - ay) - (py - ay) * (bx - ax);
    }

    private uint SampleTexture(float u, float v)
    {
        if (_texPixelsPtr is null)
            return 0xFFE6E6E6; // BGRA: B=230, G=230, R=230, A=255

        u -= MathF.Floor(u);
        v -= MathF.Floor(v);

        int tx = (int)(u * _texW) % _texW;
        int ty = (int)(v * _texH) % _texH;
        if (tx < 0) tx += _texW;
        if (ty < 0) ty += _texH;

        int texIdx = (ty * _texW + tx) * 4;
        byte b = _texPixelsPtr[texIdx];
        byte g = _texPixelsPtr[texIdx + 1];
        byte r = _texPixelsPtr[texIdx + 2];
        byte a = _texPixelsPtr[texIdx + 3];

        if (a < 128)
            return 0;

        // Return packed BGRA with A=255
        return (uint)(b | (g << 8) | (r << 16) | (0xFF << 24));
    }

    public void Dispose()
    {
        if (_texPixelsHandle.IsAllocated)
            _texPixelsHandle.Free();
        _texPixels = null;
        _texPixelsPtr = null;
        _loadedTexture = null;
    }
}

internal sealed record CameraData(
    Vector3 Eye,
    Vector3 Right,
    Vector3 Up,
    Vector3 Forward,
    float OrthoScale);
