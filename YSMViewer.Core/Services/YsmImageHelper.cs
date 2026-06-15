using SixLabors.ImageSharp;

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
            using var image = Image.Load(imageData);
            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            return ms.ToArray();
        }
        catch
        {
            return imageData;
        }
    }

    public static (int width, int height) GetPngDimensions(byte[] data)
    {
        if (data is { Length: >= 24 }
            && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
        {
            int w = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
            int h = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
            return (w, h);
        }
        return (0, 0);
    }
}
