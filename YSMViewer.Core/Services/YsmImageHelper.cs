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
        try
        {
            var info = Image.Identify(data);
            if (info is not null)
                return (info.Width, info.Height);
            return (0, 0);
        }
        catch
        {
            return (0, 0);
        }
    }
}
