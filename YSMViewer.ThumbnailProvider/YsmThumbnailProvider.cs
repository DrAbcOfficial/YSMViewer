using SixLabors.ImageSharp;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.InteropServices.Marshalling;
using YSMViewer.Services;
using YSMViewer.ThumbnailProvider.Rendering;

namespace YSMViewer.ThumbnailProvider;

[ComVisible(true)]
[Guid("F4E2C1A8-7B3D-4E5F-9A1C-2D8E6F0B4A3C")]
[ClassInterface(ClassInterfaceType.None)]
[GeneratedComClass]
public sealed partial class YsmThumbnailProvider : IThumbnailProvider, IInitializeWithStream
{
    private byte[]? _fileData;

    public int Initialize(nint pstream, uint grfMode)
    {
        if (pstream == nint.Zero)
            return 0;
        try
        {
            IStream managed_strem = (IStream)Marshal.GetObjectForIUnknown(pstream);
            using var stream = new ComStreamWrapper(managed_strem);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            _fileData = ms.ToArray();
#if DEBUG
            Trace.WriteLine($"[YsmThumb] Initialize: {_fileData.Length} bytes");
#endif
            return 0;
        }
        catch (Exception ex)
        {
#if DEBUG
            Trace.WriteLine($"[YsmThumb] Initialize failed: {ex.Message}");
#endif
            return unchecked((int)0x80004005);
        }
    }

    public int GetThumbnail(uint cx, out nint hBitmap, out WTS_ALPHATYPE alphaType)
    {
        hBitmap = nint.Zero;
        alphaType = WTS_ALPHATYPE.WTSAT_UNKNOWN;

        try
        {
            if (_fileData is null)
                return unchecked((int)0x80004005);

#if DEBUG
            var sw = Stopwatch.StartNew();
            Trace.WriteLine($"[YsmThumb] GetThumbnail cx={cx}");
#endif

            var document = YsmLoaderService.LoadDocumentFromBytes(_fileData);
            var scene = GeometryBuilder.Build(document);
            using var renderer = new ThumbnailRenderer();
            var size = Math.Min((int)cx, 256);
            using var image = renderer.Render(scene, size);

            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            ms.Position = 0;
            using var bmp = new System.Drawing.Bitmap(ms);
            hBitmap = bmp.GetHbitmap();
            alphaType = WTS_ALPHATYPE.WTSAT_ARGB;

#if DEBUG
            Trace.WriteLine($"[YsmThumb] Done in {sw.ElapsedMilliseconds}ms, hBitmap=0x{hBitmap:X}");
#endif
            return 0;
        }
        catch (Exception ex)
        {
#if DEBUG
            Trace.WriteLine($"[YsmThumb] GetThumbnail failed: {ex}");
#endif
            if (hBitmap != nint.Zero)
            {
                DeleteObject(hBitmap);
                hBitmap = nint.Zero;
            }
            return unchecked((int)0x80004005);
        }
    }

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(nint hObject);
}
