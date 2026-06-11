using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace YSMViewer.ThumbnailProvider;

[ComImport]
[Guid("E357FCCD-A995-4576-B01F-234630154E96")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IThumbnailProvider
{
    [PreserveSig]
    int GetThumbnail(uint cx, out nint hBitmap, out WTS_ALPHATYPE alphaType);
}

[ComImport]
[Guid("B824B49D-22AC-4161-AC8A-9916E8FA3F7F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IInitializeWithStream
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
        var pcbRead = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            _stream.Read(tempBuffer, count, pcbRead);
            int bytesRead = Marshal.ReadInt32(pcbRead);
            if (bytesRead > 0)
                Array.Copy(tempBuffer, 0, buffer, offset, bytesRead);
            return bytesRead;
        }
        finally
        {
            Marshal.FreeHGlobal(pcbRead);
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var plibNewPosition = Marshal.AllocHGlobal(sizeof(long));
        try
        {
            _stream.Seek(offset, (int)origin, plibNewPosition);
            return Marshal.ReadInt64(plibNewPosition);
        }
        finally
        {
            Marshal.FreeHGlobal(plibNewPosition);
        }
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
