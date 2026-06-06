using System.Text.Json;
using System.Text.Json.Serialization;

namespace YSMViewer.Models;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(MinecraftAnimationFile))]
[JsonSerializable(typeof(MinecraftAnimation))]
[JsonSerializable(typeof(MinecraftBoneAnimation))]
[JsonSerializable(typeof(MinecraftKeyframeSet))]
[JsonSerializable(typeof(MinecraftGeometryFile))]
[JsonSerializable(typeof(MinecraftGeometry))]
[JsonSerializable(typeof(MinecraftGeometryDescription))]
[JsonSerializable(typeof(MinecraftBone))]
[JsonSerializable(typeof(MinecraftCube))]
[JsonSerializable(typeof(MinecraftCubeUV))]
[JsonSerializable(typeof(MinecraftCubeFaceUV))]
[JsonSerializable(typeof(Dictionary<string, MinecraftAnimation>))]
[JsonSerializable(typeof(List<MinecraftGeometry>))]
[JsonSerializable(typeof(List<MinecraftBone>))]
[JsonSerializable(typeof(List<MinecraftCube>))]
[JsonSerializable(typeof(List<float>))]
[JsonSerializable(typeof(float[]))]
[JsonSerializable(typeof(Dictionary<string, MinecraftBoneAnimation>))]
[JsonSerializable(typeof(Dictionary<float, float[]>))]
public sealed partial class YsmJsonContext : JsonSerializerContext
{
}
