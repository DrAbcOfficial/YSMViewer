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
            return (void*)GCHandle.ToIntPtr(handle);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[YsmThumbnail] Create failed: {ex.Message}");
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
                return -1;

            var size = Math.Max(1, Math.Min(width, height));
            using var renderer = new ThumbnailRenderer();
            var pixels = renderer.Render(scene, size);

            int offsetX = (width - size) / 2;
            int offsetY = (height - size) / 2;

            new Span<byte>(bgra, width * height * 4).Clear();

            fixed (byte* src = pixels)
            {
                int srcStride = size * 4;
                int dstStride = width * 4;
                for (int y = 0; y < size; y++)
                {
                    Buffer.MemoryCopy(
                        src + y * srcStride,
                        bgra + (offsetY + y) * dstStride + offsetX * 4,
                        srcStride, srcStride);
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[YsmThumbnail] Render failed: {ex.Message}");
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
