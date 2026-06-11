using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using YSMViewer.Rendering.Thumbnail;
using YSMViewer.Services;

namespace YSMViewer.ThumbnailProvider;

[ComVisible(true)]
[Guid("F4E2C1A8-7B3D-4E5F-9A1C-2D8E6F0B4A3C")]
[ClassInterface(ClassInterfaceType.None)]
public sealed class YsmThumbnailProvider : IThumbnailProvider, IInitializeWithStream
{
    private byte[]? _fileData;

    public int Initialize(IStream pstream, uint grfMode)
    {
        try
        {
            using var stream = new ComStreamWrapper(pstream);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            _fileData = ms.ToArray();
            return 0;
        }
        catch
        {
            return unchecked((int)0x80004005);
        }
    }

    public int GetThumbnail(uint cx, out IntPtr hBitmap, out WTS_ALPHATYPE alphaType)
    {
        hBitmap = IntPtr.Zero;
        alphaType = WTS_ALPHATYPE.WTSAT_UNKNOWN;

        try
        {
            if (_fileData is null)
                return unchecked((int)0x80004005);

            var document = YsmLoaderService.LoadDocumentFromBytes(_fileData);
            var scene = GeometryBuilder.Build(document);
            using var renderer = new ThumbnailRenderer();
            using var bitmap = renderer.Render(scene, (int)cx);
            hBitmap = bitmap.GetHbitmap();
            alphaType = WTS_ALPHATYPE.WTSAT_ARGB;
            return 0;
        }
        catch
        {
            if (hBitmap != IntPtr.Zero)
            {
                DeleteObject(hBitmap);
                hBitmap = IntPtr.Zero;
            }
            return unchecked((int)0x80004005);
        }
    }

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
