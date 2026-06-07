using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SkiaSharp;

namespace YSMViewer.Rendering.Skia;

public sealed class SkiaProjectionRenderer : IRenderer, IAutoRotateRenderer, IDisposable
{
    private readonly Image _image;
    private WriteableBitmap? _framebuffer;
    private MeshData? _mesh;
    private YSMViewer.Models.Document.YsmModelDocument? _document;
    private readonly DispatcherTimer _timer;

    private float _autoRotateAngle;
    private bool _isAutoRotating = true;
    private float _autoRotateDegreesPerSecond = 20f;

    private const int RenderWidth = 960;
    private const int RenderHeight = 540;
    private const float FovRadians = 50f * MathF.PI / 180f;
    private const float FarPlane = 5000f;
    private const float NearPlane = 0.1f;

    private Vector3 _cameraTarget = Vector3.Zero;
    private float _cameraDistance = 30f;
    private float _cameraPitch = -15f;

    private (byte R, byte G, byte B, byte A) _bgColor = (30, 30, 30, 255);

    public SkiaProjectionRenderer()
    {
        _image = new Image { Stretch = Avalonia.Media.Stretch.UniformToFill };

        _framebuffer = new WriteableBitmap(
            new PixelSize(RenderWidth, RenderHeight),
            new Avalonia.Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888);

        _image.Source = _framebuffer;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public Control View => _image;
    public RendererCapabilities Capabilities => RendererCapabilities.Browser;

    public bool IsAutoRotating
    {
        get => _isAutoRotating;
        set => _isAutoRotating = value;
    }

    public float AutoRotateDegreesPerSecond
    {
        get => _autoRotateDegreesPerSecond;
        set => _autoRotateDegreesPerSecond = value;
    }

    public void LoadModel(YSMViewer.Models.Document.YsmModelDocument document)
    {
        _document = document;
        _mesh = TriangleMeshBuilder.BuildFromDocument(document);
        ComputeCameraDistance();
        RenderFrame();
    }

    public void Clear()
    {
        _document = null;
        _mesh = null;
        _autoRotateAngle = 0f;
        RenderFrame();
    }

    public void SetCameraView(Rendering.RenderCameraView view)
    {
        _autoRotateAngle = view switch
        {
            RenderCameraView.Side => 90f,
            RenderCameraView.Top => 0f,
            _ => 0f,
        };
        _cameraPitch = view switch
        {
            RenderCameraView.Top => -89f,
            _ => 0f,
        };
        RenderFrame();
    }

    public void SetTheme(RenderTheme theme)
    {
        _bgColor = (theme.BgR, theme.BgG, theme.BgB, theme.BgA);
        RenderFrame();
    }

    public void Update(float deltaTime)
    {
        if (_isAutoRotating && _mesh is not null)
        {
            _autoRotateAngle += _autoRotateDegreesPerSecond * deltaTime;
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        Update(1f / 30f);
        RenderFrame();
    }

    private void RenderFrame()
    {
        if (_framebuffer is null) return;

        using var skBitmap = new SKBitmap(RenderWidth, RenderHeight);
        using var canvas = new SKCanvas(skBitmap);

        var bg = new SKColor(_bgColor.R, _bgColor.G, _bgColor.B, _bgColor.A);
        canvas.Clear(bg);

        if (_mesh is not null && _mesh.Triangles.Count > 0)
        {
            RenderMesh(canvas, skBitmap);
        }

        // Copy to WriteableBitmap
        using var fbLock = _framebuffer.Lock();
        unsafe
        {
            var src = (byte*)skBitmap.GetPixels().ToPointer();
            var dst = (byte*)fbLock.Address.ToPointer();
            int stride = RenderWidth * 4;
            int skStride = skBitmap.RowBytes;
            for (int y = 0; y < RenderHeight; y++)
            {
                for (int x = 0; x < RenderWidth; x++)
                {
                    int si = y * skStride + x * 4;
                    int di = y * stride + x * 4;
                    dst[di] = src[si];         // B
                    dst[di + 1] = src[si + 1]; // G
                    dst[di + 2] = src[si + 2]; // R
                    dst[di + 3] = src[si + 3]; // A
                }
            }
        }
    }

    private void RenderMesh(SKCanvas canvas, SKBitmap framebuffer)
    {
        if (_mesh is null) return;

        float yaw = _autoRotateAngle * MathF.PI / 180f;
        float pitch = _cameraPitch * MathF.PI / 180f;

        var camPos = new Vector3(
            _cameraDistance * MathF.Cos(pitch) * MathF.Sin(yaw),
            _cameraDistance * MathF.Sin(pitch),
            _cameraDistance * MathF.Cos(pitch) * MathF.Cos(yaw));
        camPos += _cameraTarget;

        var viewMatrix = Matrix4x4.CreateLookAt(camPos, _cameraTarget, new Vector3(0, 1, 0));
        float aspectRatio = (float)RenderWidth / RenderHeight;
        var projMatrix = CreatePerspective(aspectRatio);
        var vpMatrix = viewMatrix * projMatrix;

        var lightDir = Vector3.Normalize(new Vector3(-0.5f, 1f, 0.5f));

        var projected = new List<(TriangleFace face, Vector3 s0, Vector3 s1, Vector3 s2, float depth)>();

        foreach (var tri in _mesh.Triangles)
        {
            var t0 = Vector4.Transform(new Vector4(tri.P0, 1), vpMatrix);
            var t1 = Vector4.Transform(new Vector4(tri.P1, 1), vpMatrix);
            var t2 = Vector4.Transform(new Vector4(tri.P2, 1), vpMatrix);

            if (t0.W <= 0 || t1.W <= 0 || t2.W <= 0) continue;

            var s0 = new Vector3(t0.X / t0.W, t0.Y / t0.W, t0.Z / t0.W);
            var s1 = new Vector3(t1.X / t1.W, t1.Y / t1.W, t1.Z / t1.W);
            var s2 = new Vector3(t2.X / t2.W, t2.Y / t2.W, t2.Z / t2.W);

            s0.X = (s0.X * 0.5f + 0.5f) * RenderWidth;
            s0.Y = (-s0.Y * 0.5f + 0.5f) * RenderHeight;
            s1.X = (s1.X * 0.5f + 0.5f) * RenderWidth;
            s1.Y = (-s1.Y * 0.5f + 0.5f) * RenderHeight;
            s2.X = (s2.X * 0.5f + 0.5f) * RenderWidth;
            s2.Y = (-s2.Y * 0.5f + 0.5f) * RenderHeight;

            float depth = (s0.Z + s1.Z + s2.Z) / 3f;
            projected.Add((tri, s0, s1, s2, depth));
        }

        projected.Sort((a, b) => a.depth.CompareTo(b.depth));

        foreach (var (tri, s0, s1, s2, depth) in projected)
        {
            float area = MathF.Abs((s1.X - s0.X) * (s2.Y - s0.Y) - (s2.X - s0.X) * (s1.Y - s0.Y));
            if (area < 1f) continue;

            float brightness = 0.3f + MathF.Max(0, Vector3.Dot(tri.Nrm, lightDir)) * 0.7f;

            if (tri.TextureIndex >= 0 && tri.TextureIndex < _mesh.Textures.Count)
            {
                DrawTexturedTriangle(framebuffer, tri, s0, s1, s2, brightness, _mesh.Textures[tri.TextureIndex]);
            }
            else
            {
                var paint = new SKPaint
                {
                    Color = new SKColor(
                        (byte)(brightness * 255f),
                        (byte)(brightness * 255f),
                        (byte)(brightness * 255f)),
                    IsAntialias = false,
                };
                var path = new SKPath();
                path.MoveTo(s0.X, s0.Y);
                path.LineTo(s1.X, s1.Y);
                path.LineTo(s2.X, s2.Y);
                path.Close();
                canvas.DrawPath(path, paint);
                paint.Dispose();
                path.Dispose();
            }
        }
    }

    private static unsafe void DrawTexturedTriangle(
        SKBitmap framebuffer, TriangleFace tri,
        Vector3 s0, Vector3 s1, Vector3 s2,
        float brightness, SKBitmap texture)
    {
        int xMin = Math.Max(0, (int)MathF.Min(MathF.Min(s0.X, s1.X), s2.X));
        int xMax = Math.Min(RenderWidth - 1, (int)MathF.Max(MathF.Max(s0.X, s1.X), s2.X));
        int yMin = Math.Max(0, (int)MathF.Min(MathF.Min(s0.Y, s1.Y), s2.Y));
        int yMax = Math.Min(RenderHeight - 1, (int)MathF.Max(MathF.Max(s0.Y, s1.Y), s2.Y));

        float denom = (s1.Y - s2.Y) * (s0.X - s2.X) + (s2.X - s1.X) * (s0.Y - s2.Y);
        if (MathF.Abs(denom) < 0.0001f) return;

        float invDenom = 1f / denom;
        int tw = texture.Width;
        int th = texture.Height;
        var texPtr = (byte*)texture.GetPixels().ToPointer();
        int texStride = texture.RowBytes;
        var fbPtr = (byte*)framebuffer.GetPixels().ToPointer();
        int fbStride = framebuffer.RowBytes;

        for (int y = yMin; y <= yMax; y++)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                float w0 = ((s1.Y - s2.Y) * (x - s2.X) + (s2.X - s1.X) * (y - s2.Y)) * invDenom;
                float w1 = ((s2.Y - s0.Y) * (x - s2.X) + (s0.X - s2.X) * (y - s2.Y)) * invDenom;
                float w2 = 1f - w0 - w1;

                if (w0 < -0.001f || w1 < -0.001f || w2 < -0.001f) continue;

                float wSum = w0 + w1 + w2;
                w0 /= wSum; w1 /= wSum; w2 /= wSum;

                float u = w0 * tri.U0 + w1 * tri.U1 + w2 * tri.U2;
                float v = w0 * tri.V0 + w1 * tri.V1 + w2 * tri.V2;

                int tx = Math.Clamp((int)(u * tw), 0, tw - 1);
                int ty = Math.Clamp((int)(v * th), 0, th - 1);

                int ti = ty * texStride + tx * 4;
                byte tb = texPtr[ti];
                byte tg = texPtr[ti + 1];
                byte tr = texPtr[ti + 2];
                byte ta = texPtr[ti + 3];

                if (ta < 128) continue;

                int fi = y * fbStride + x * 4;
                fbPtr[fi] = (byte)(tb * brightness);
                fbPtr[fi + 1] = (byte)(tg * brightness);
                fbPtr[fi + 2] = (byte)(tr * brightness);
                fbPtr[fi + 3] = 255;
            }
        }
    }

    private void ComputeCameraDistance()
    {
        if (_mesh is null || _mesh.Triangles.Count == 0) return;

        float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

        foreach (var tri in _mesh.Triangles)
        {
            foreach (var v in new[] { tri.P0, tri.P1, tri.P2 })
            {
                if (v.X < minX) minX = v.X;
                if (v.Y < minY) minY = v.Y;
                if (v.Z < minZ) minZ = v.Z;
                if (v.X > maxX) maxX = v.X;
                if (v.Y > maxY) maxY = v.Y;
                if (v.Z > maxZ) maxZ = v.Z;
            }
        }

        _cameraTarget = new Vector3(
            (minX + maxX) / 2f,
            (minY + maxY) / 2f,
            (minZ + maxZ) / 2f);

        var size = new Vector3(maxX - minX, maxY - minY, maxZ - minZ);
        _cameraDistance = MathF.Max(size.X, MathF.Max(size.Y, size.Z)) * 1.5f;
        if (_cameraDistance < 0.5f) _cameraDistance = 2f;
    }

    private static Matrix4x4 CreatePerspective(float aspectRatio)
    {
        float f = 1f / MathF.Tan(FovRadians / 2f);
        var result = new Matrix4x4();
        result.M11 = f / aspectRatio;
        result.M22 = f;
        result.M33 = FarPlane / (NearPlane - FarPlane);
        result.M43 = -1f;
        result.M34 = NearPlane * FarPlane / (NearPlane - FarPlane);
        result.M44 = 0f;
        return result;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
