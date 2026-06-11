using System.Diagnostics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using YSMViewer.Services;

var sw = Stopwatch.StartNew();

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: TestHarness <path-to-ysm-file> [output-png-path] [size]");
    Console.Error.WriteLine("  Default output: <input>.thumb.png");
    Console.Error.WriteLine("  Default size: 256");
    return 1;
}

var inputPath = args[0];
var outputPath = args.Length > 1 ? args[1] : Path.ChangeExtension(inputPath, ".thumb.png");
var thumbSize = args.Length > 2 && int.TryParse(args[2], out var s) ? s : 256;

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"File not found: {inputPath}");
    return 1;
}

Console.WriteLine($"=== YSM Thumbnail Test Harness ===");
Console.WriteLine($"Input:  {inputPath} ({new FileInfo(inputPath).Length:N0} bytes)");
Console.WriteLine($"Output: {outputPath} ({thumbSize}x{thumbSize})");
Console.WriteLine();

try
{
    var data = File.ReadAllBytes(inputPath);
    var t0 = sw.ElapsedMilliseconds;

    var document = YsmLoaderService.LoadDocumentFromBytes(data);
    var t1 = sw.ElapsedMilliseconds;
    Console.WriteLine($"Parse:  {t1 - t0}ms");

    Console.WriteLine($"  Name:      {document.Info.DisplayName}");
    Console.WriteLine($"  Models:    {document.Models.Count}");
    Console.WriteLine($"  Textures:  {document.Textures.Count}");
    var totalBones = 0;
    var totalCubes = 0;
    foreach (var model in document.Models)
    {
        Console.WriteLine($"    [{model.Name}] Bones={model.Bones.Count} Cubes={model.Bones.Sum(b => b.Cubes.Count)} " +
                          $"Tex={model.TextureWidth}x{model.TextureHeight} Vis={model.DefaultVisible}");
        totalBones += model.Bones.Count;
        totalCubes += model.Bones.Sum(b => b.Cubes.Count);
    }
    Console.WriteLine($"  Total:     {totalBones} bones, {totalCubes} cubes");

    var scene = YSMViewer.Rendering.Thumbnail.GeometryBuilder.Build(document);
    var t2 = sw.ElapsedMilliseconds;
    Console.WriteLine();
    Console.WriteLine($"Geo:    {t2 - t1}ms");
    Console.WriteLine($"  Faces:      {scene.Faces.Count}");
    Console.WriteLine($"  Bounds:     {scene.BoundsMin:F2} -> {scene.BoundsMax:F2}");
    Console.WriteLine($"  Texture:    {(scene.Texture is null ? "(none)" : $"{scene.Texture.Width}x{scene.Texture.Height} {scene.Texture.Data.Length} bytes")}");

    using var renderer = new YSMViewer.Rendering.Thumbnail.ThumbnailRenderer();
    using var image = renderer.Render(scene, thumbSize);
    image.SaveAsPng(outputPath);
    var t3 = sw.ElapsedMilliseconds;
    Console.WriteLine();
    Console.WriteLine($"Render: {t3 - t2}ms  ->  {outputPath}");
    Console.WriteLine();
    Console.WriteLine($"Total:  {t3}ms");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"ERROR [{sw.ElapsedMilliseconds}ms]: {ex}");
    return 1;
}
