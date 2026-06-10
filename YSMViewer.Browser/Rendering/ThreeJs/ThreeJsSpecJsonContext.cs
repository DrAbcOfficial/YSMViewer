using System.Text.Json.Serialization;
using static YSMViewer.Rendering.ThreeJs.ThreeJsPayloadBuilder;

namespace YSMViewer.Rendering.ThreeJs;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ThreeJsModelSpec))]
[JsonSerializable(typeof(ThreeJsModelGroup))]
[JsonSerializable(typeof(ThreeJsBoneData))]
[JsonSerializable(typeof(ThreeJsMeshData))]
[JsonSerializable(typeof(List<ThreeJsModelGroup>))]
[JsonSerializable(typeof(List<ThreeJsBoneData>))]
[JsonSerializable(typeof(List<ThreeJsMeshData>))]
[JsonSerializable(typeof(float[]))]
[JsonSerializable(typeof(int[]))]
internal sealed partial class ThreeJsSpecJsonContext : JsonSerializerContext
{
}
