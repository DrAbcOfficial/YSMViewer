using System.Drawing;
using System.Drawing.Imaging;

namespace YSMViewer.Services;

public static class YsmImageHelper
{
    public static byte[]? EnsurePng(byte[]? data)
    {
        if (data is null or { Length: 0 }) return null;

        if (data.Length >= 8)
        {
            if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
                return data;

            return ConvertImageToPng(data);
        }

        return data;
    }

    private static byte[] ConvertImageToPng(byte[] imageData)
    {
        try
        {
            using var ms = new MemoryStream(imageData);
            using var bitmap = new Bitmap(ms);
            using var outMs = new MemoryStream();
            bitmap.Save(outMs, ImageFormat.Png);
            return outMs.ToArray();
        }
        catch
        {
            return imageData;
        }
    }

    public static (int width, int height) GetPngDimensions(byte[] data)
    {
        try
        {
            if (data.Length < 24)
                return (0, 0);
            if (data[0] != 0x89 || data[1] != 0x50 || data[2] != 0x4E || data[3] != 0x47)
                return (0, 0);
            int width = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
            int height = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
            return (width, height);
        }
        catch
        {
            return (0, 0);
        }
    }
}
