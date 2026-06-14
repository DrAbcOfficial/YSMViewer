using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;
using System.Runtime.InteropServices;
using YSMViewer.Services;
using YSMViewer.ThumbnailProvider.Rendering;

namespace YSMViewer.ThumbnailProvider.NativeAotCom;

public static unsafe class NativeExports
{
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
            Util.Log($"Create: {length} bytes, models={document.Models.Count}, faces={scene.Faces.Count}");
            return (void*)GCHandle.ToIntPtr(handle);
        }
        catch (Exception ex)
        {
            Util.Log($"Create FAILED: {ex.GetType().Name}: {ex.Message}");
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
                Util.Log("Render: no scene in context");
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
                fixed (Rgba32* srcBase = pixels)
                {
                    CopyPixels(srcBase, bgra, size, width, offsetX, offsetY);
                }
            }
            else
            {
                var pixelCount = size * size;
                var pixels = new Rgba32[pixelCount];
                image.CopyPixelDataTo(pixels);
                fixed (Rgba32* srcBase = pixels)
                {
                    CopyPixels(srcBase, bgra, size, width, offsetX, offsetY);
                }
            }

            Util.Log($"Render: {width}x{height} -> used {size}x{size} in {sw.ElapsedMilliseconds}ms");
            return 0;
        }
        catch (Exception ex)
        {
            Util.Log($"Render FAILED: {ex.GetType().Name}: {ex.Message}");
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

    private static void CopyPixels(Rgba32* srcBase, byte* bgra, int size, int width, int offsetX, int offsetY)
    {
        int dstStride = width * 4;
        byte* dstRowStart = bgra + offsetY * dstStride + offsetX * 4;
        for (int y = 0; y < size; y++)
        {
            Rgba32* srcCol = srcBase + y * size;
            byte* dstCol = dstRowStart + y * dstStride;
            for (int x = 0; x < size; x++)
            {
                var pixel = srcCol[x];
                byte* d = dstCol + x * 4;
                d[0] = pixel.B;
                d[1] = pixel.G;
                d[2] = pixel.R;
                d[3] = pixel.A;
            }
        }
    }
}
