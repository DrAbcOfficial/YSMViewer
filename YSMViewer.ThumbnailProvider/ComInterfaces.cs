using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.InteropServices.Marshalling;

namespace YSMViewer.ThumbnailProvider;

[GeneratedComInterface]
[Guid("E357FCCD-A995-4576-B01F-234630154E96")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public partial interface IThumbnailProvider
{
    [PreserveSig]
    int GetThumbnail(uint cx, out nint hBitmap, out WTS_ALPHATYPE alphaType);
}

[GeneratedComInterface]
[Guid("B824B49D-22AC-4161-AC8A-9916E8FA3F7F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public partial interface IInitializeWithStream
{
    [PreserveSig]
    int Initialize(nint stream, uint grfMode);
}

public enum WTS_ALPHATYPE : int
{
    WTSAT_UNKNOWN = 0,
    WTSAT_RGB = 1,
    WTSAT_ARGB = 2,
}

internal sealed class ComStreamWrapper(IStream stream) : Stream
{
    private readonly IStream _stream = stream;

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length
    {
        get
        {
            _stream.Stat(out var stat, 0);
            return stat.cbSize;
        }
    }

    public override long Position
    {
        get => Seek(0, SeekOrigin.Current);
        set => Seek(value, SeekOrigin.Begin);
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var tempBuffer = new byte[count];
        _stream.Read(tempBuffer, count, IntPtr.Zero);
        Array.Copy(tempBuffer, 0, buffer, offset, count);
        return count;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        _stream.Seek(offset, (int)origin, IntPtr.Zero);
        var pos = IntPtr.Zero;
        _stream.Seek(0, 1, pos);
        return pos.ToInt64();
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
