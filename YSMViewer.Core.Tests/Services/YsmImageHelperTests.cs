using YSMViewer.Services;

namespace YSMViewer.Core.Tests.Services;

public sealed class YsmImageHelperTests
{
    [Fact]
    public void GetPngDimensions_ValidPng_ReturnsCorrectDimensions()
    {
        var png = CreateMinimalPng(64, 32);

        var (w, h) = YsmImageHelper.GetPngDimensions(png);

        Assert.Equal(64, w);
        Assert.Equal(32, h);
    }

    [Fact]
    public void GetPngDimensions_TooShort_ReturnsZero()
    {
        var data = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        var (w, h) = YsmImageHelper.GetPngDimensions(data);

        Assert.Equal(0, w);
        Assert.Equal(0, h);
    }

    [Fact]
    public void GetPngDimensions_NotPngSignature_ReturnsZero()
    {
        var data = new byte[24];

        var (w, h) = YsmImageHelper.GetPngDimensions(data);

        Assert.Equal(0, w);
        Assert.Equal(0, h);
    }

    [Fact]
    public void EnsurePng_AlreadyPng_ReturnsSameData()
    {
        var png = CreateMinimalPng(16, 16);

        var result = YsmImageHelper.EnsurePng(png);

        Assert.Same(png, result);
    }

    [Fact]
    public void EnsurePng_NullData_ReturnsNull()
    {
        var result = YsmImageHelper.EnsurePng(null);
        Assert.Null(result);
    }

    [Fact]
    public void EnsurePng_EmptyData_ReturnsNull()
    {
        var result = YsmImageHelper.EnsurePng([]);
        Assert.Null(result);
    }

    [Fact]
    public void EnsurePng_ShortNonPng_ReturnsSameData()
    {
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        var result = YsmImageHelper.EnsurePng(data);

        Assert.Same(data, result);
    }

    private static byte[] CreateMinimalPng(int width, int height)
    {
        var signature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var widthBytes = BitConverter.GetBytes(width);
        var heightBytes = BitConverter.GetBytes(height);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(widthBytes);
            Array.Reverse(heightBytes);
        }

        var header = new byte[24];
        Array.Copy(signature, 0, header, 0, 8);
        Array.Copy(widthBytes, 0, header, 16, 4);
        Array.Copy(heightBytes, 0, header, 20, 4);
        return header;
    }
}
