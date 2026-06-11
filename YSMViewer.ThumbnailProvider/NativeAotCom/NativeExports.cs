using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using YSMViewer.Services;
using YSMViewer.ThumbnailProvider.Rendering;

namespace YSMViewer.ThumbnailProvider;

public static unsafe class NativeExports
{
    private static byte[]? _fileData;
    private static GeometryBuilder.ThumbnailScene? _scene;

    private static void Log(string msg)
    {
        try
        {
            var logPath = Path.Combine(Path.GetTempPath(), "YsmThumbnail.log");
            File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss.fff} [{Environment.ProcessId}] {msg}{Environment.NewLine}");
        }
        catch { }
    }

    [UnmanagedCallersOnly(EntryPoint = "YsmThumbnail_Init")]
    public static int Init(byte* data, int length)
    {
        try
        {
            _scene = null;
            _fileData = new byte[length];
            Marshal.Copy((nint)data, _fileData, 0, length);

            var document = YsmLoaderService.LoadDocumentFromBytes(_fileData);
            _scene = GeometryBuilder.Build(document);
            Log($"Init: {length} bytes, models={document.Models.Count}, faces={_scene.Faces.Count}");
            return 0;
        }
        catch (Exception ex)
        {
            Log($"Init FAILED: {ex.GetType().Name}: {ex.Message}");
            _scene = null;
            _fileData = null;
            return -1;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "YsmThumbnail_Render")]
    public static int Render(byte* rgba, int width, int height)
    {
        try
        {
            if (_scene is null)
            {
                Log("Render: no scene loaded");
                return -1;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var size = Math.Max(1, Math.Min(width, height));
            using var renderer = new ThumbnailRenderer();
            using var image = renderer.Render(_scene, size);

            var pixelCount = size * size;
            var pixels = new Rgba32[pixelCount];
            image.CopyPixelDataTo(pixels);

            int offsetX = (width - size) / 2;
            int offsetY = (height - size) / 2;

            // Fill output buffer with transparent
            for (int i = 0; i < width * height * 4; i++)
                rgba[i] = 0;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int srcIdx = y * size + x;
                    int dstIdx = ((y + offsetY) * width + (x + offsetX)) * 4;
                    rgba[dstIdx] = pixels[srcIdx].R;
                    rgba[dstIdx + 1] = pixels[srcIdx].G;
                    rgba[dstIdx + 2] = pixels[srcIdx].B;
                    rgba[dstIdx + 3] = pixels[srcIdx].A;
                }
            }

            Log($"Render: {width}x{height} -> used {size}x{size} in {sw.ElapsedMilliseconds}ms");
            return 0;
        }
        catch (Exception ex)
        {
            Log($"Render FAILED: {ex.GetType().Name}: {ex.Message}");
            return -1;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "YsmThumbnail_Free")]
    public static void Free()
    {
        _fileData = null;
        _scene = null;
    }
}
