using System.Diagnostics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;
using YSMViewer.Services;
using YSMViewer.ThumbnailProvider.Rendering;

namespace YSMViewer.ThumbnailProvider.NativeAotCom;

public static unsafe class NativeExports
{
    [Conditional("DEBUG")]
    private static void Log(string msg)
    {
        try
        {
            var logPath = Path.Combine(Path.GetTempPath(), "YsmThumbnail.log");
            File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss.fff} [{Environment.ProcessId}] {msg}{Environment.NewLine}");
        }
        catch { }
    }

    private sealed class ThumbnailContext
    {
        public GeometryBuilder.ThumbnailScene? Scene;
    }

    [UnmanagedCallersOnly(EntryPoint = "YsmThumbnail_Create")]
    public static void* Create(byte* data, int length)
    {
        try
        {
            var fileData = new byte[length];
            Marshal.Copy((nint)data, fileData, 0, length);

            var document = YsmLoaderService.LoadDocumentForThumbnail(fileData);
            var scene = GeometryBuilder.Build(document);

            var ctx = new ThumbnailContext { Scene = scene };
            var handle = GCHandle.Alloc(ctx);
            Log($"Create: {length} bytes, models={document.Models.Count}, faces={scene.Faces.Count}");
            return (void*)GCHandle.ToIntPtr(handle);
        }
        catch (Exception ex)
        {
            Log($"Create FAILED: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "YsmThumbnail_Render")]
    public static int Render(void* ctx, byte* bgra, int width, int height)
    {
        try
        {
            var scene = ((ThumbnailContext)GCHandle.FromIntPtr((nint)ctx).Target!).Scene;
            if (scene is null)
            {
                Log("Render: no scene in context");
                return -1;
            }

            var sw = Stopwatch.StartNew();
            var size = Math.Max(1, Math.Min(width, height));
            using var renderer = new ThumbnailRenderer();
            using var image = renderer.Render(scene, size);

            int offsetX = (width - size) / 2;
            int offsetY = (height - size) / 2;

            new Span<byte>(bgra, width * height * 4).Clear();

            if (image.DangerousTryGetSinglePixelMemory(out var pixelMem))
            {
                var pixels = pixelMem.Span;
                for (int y = 0; y < size; y++)
                {
                    int dstRowStart = (y + offsetY) * width + offsetX;
                    int srcRowStart = y * size;
                    for (int x = 0; x < size; x++)
                    {
                        int srcIdx = srcRowStart + x;
                        int dstIdx = (dstRowStart + x) * 4;
                        bgra[dstIdx] = pixels[srcIdx].B;
                        bgra[dstIdx + 1] = pixels[srcIdx].G;
                        bgra[dstIdx + 2] = pixels[srcIdx].R;
                        bgra[dstIdx + 3] = pixels[srcIdx].A;
                    }
                }
            }
            else
            {
                var pixelCount = size * size;
                var pixels = new Rgba32[pixelCount];
                image.CopyPixelDataTo(pixels);
                for (int y = 0; y < size; y++)
                {
                    int dstRowStart = (y + offsetY) * width + offsetX;
                    int srcRowStart = y * size;
                    for (int x = 0; x < size; x++)
                    {
                        int srcIdx = srcRowStart + x;
                        int dstIdx = (dstRowStart + x) * 4;
                        bgra[dstIdx] = pixels[srcIdx].B;
                        bgra[dstIdx + 1] = pixels[srcIdx].G;
                        bgra[dstIdx + 2] = pixels[srcIdx].R;
                        bgra[dstIdx + 3] = pixels[srcIdx].A;
                    }
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

    [UnmanagedCallersOnly(EntryPoint = "YsmThumbnail_Destroy")]
    public static void Destroy(void* ctx)
    {
        if (ctx is null) return;
        var handle = GCHandle.FromIntPtr((nint)ctx);
        if (handle.IsAllocated)
            handle.Free();
    }
}
