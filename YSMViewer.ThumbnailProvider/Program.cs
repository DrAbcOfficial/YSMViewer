using SixLabors.ImageSharp;
using YSMViewer.Services;
using YSMViewer.ThumbnailProvider.Rendering;

var filePath = args.Length > 0 ? args[0] : "test.ysm";
var outputPath = args.Length > 1 ? args[1] : "output.png";
var size = args.Length > 2 && int.TryParse(args[2], out var s) ? s : 256;
size = Math.Clamp(size, 1, 4096);

if (!File.Exists(filePath))
{
    Console.Error.WriteLine($"File not found: {filePath}");
    Environment.Exit(1);
}

var data = File.ReadAllBytes(filePath);
var document = YsmLoaderService.LoadDocumentForThumbnail(data);
var scene = GeometryBuilder.Build(document);

using var renderer = new ThumbnailRenderer();
using var image = renderer.Render(scene, size);
image.SaveAsPng(outputPath);
