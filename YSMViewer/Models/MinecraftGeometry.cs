using System.Text.Json;
using System.Text.Json.Serialization;

namespace YSMViewer.Models;

public sealed record MinecraftGeometryFile(
    [property: JsonPropertyName("format_version")] string FormatVersion,
    [property: JsonPropertyName("minecraft:geometry")] List<MinecraftGeometry> Geometries);

public sealed record MinecraftGeometry(
    MinecraftGeometryDescription Description,
    List<MinecraftBone> Bones);

public sealed record MinecraftGeometryDescription(
    string Identifier,
    [property: JsonPropertyName("texture_width")] float TextureWidth = 64,
    [property: JsonPropertyName("texture_height")] float TextureHeight = 64,
    [property: JsonPropertyName("visible_bounds_width")] float VisibleBoundsWidth = 0,
    [property: JsonPropertyName("visible_bounds_height")] float VisibleBoundsHeight = 0,
    [property: JsonPropertyName("visible_bounds_offset")] List<float>? VisibleBoundsOffset = null);

public sealed record MinecraftBone(
    string Name,
    string? Parent = null,
    List<float>? Pivot = null,
    List<float>? Rotation = null,
    [property: JsonPropertyName("bind_pose_rotation")] List<float>? BindPoseRotation = null,
    bool Mirror = false,
    List<MinecraftCube>? Cubes = null);

public sealed record MinecraftCube(
    List<float>? Origin = null,
    List<float>? Size = null,
    List<float>? Pivot = null,
    List<float>? Rotation = null,
    MinecraftCubeUV? Uv = null,
    float Inflate = 0f,
    bool Mirror = false);

[JsonConverter(typeof(MinecraftCubeUVConverter))]
public sealed record MinecraftCubeUV(
    MinecraftCubeFaceUV? North = null,
    MinecraftCubeFaceUV? South = null,
    MinecraftCubeFaceUV? East = null,
    MinecraftCubeFaceUV? West = null,
    MinecraftCubeFaceUV? Up = null,
    MinecraftCubeFaceUV? Down = null)
{
    [JsonIgnore]
    public float? BoxU { get; init; }
    [JsonIgnore]
    public float? BoxV { get; init; }

    [JsonIgnore]
    public bool IsBoxUV => BoxU.HasValue && BoxV.HasValue;

    public MinecraftCubeUV Expand(float sizeX, float sizeY, float sizeZ)
    {
        if (!IsBoxUV) return this;

        float u = BoxU!.Value;
        float v = BoxV!.Value;
        float x = sizeX;
        float y = sizeY;
        float z = sizeZ;

        return new MinecraftCubeUV(
            North: new MinecraftCubeFaceUV(
                new List<float> { u + z, v + z },
                new List<float> { x, y }),
            South: new MinecraftCubeFaceUV(
                new List<float> { u + z + z + x, v + z },
                new List<float> { x, y }),
            East: new MinecraftCubeFaceUV(
                new List<float> { u, v + z },
                new List<float> { z, y }),
            West: new MinecraftCubeFaceUV(
                new List<float> { u + z + x, v + z },
                new List<float> { z, y }),
            Up: new MinecraftCubeFaceUV(
                new List<float> { u + z + x, v + z },
                new List<float> { -x, -z }),
            Down: new MinecraftCubeFaceUV(
                new List<float> { u + z + x + x, v },
                new List<float> { -x, z })
        );
    }
}

public sealed record MinecraftCubeFaceUV(
    [property: JsonPropertyName("uv")] List<float>? UvCoords = null,
    [property: JsonPropertyName("uv_size")] List<float>? UvSize = null,
    [property: JsonPropertyName("material_instance")] string? MaterialInstance = null);

public sealed class MinecraftCubeUVConverter : JsonConverter<MinecraftCubeUV>
{
    public override MinecraftCubeUV? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var values = new List<float>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.Number)
                    values.Add(reader.GetSingle());
                else if (reader.TokenType == JsonTokenType.String && float.TryParse(reader.GetString(), out var num))
                    values.Add(num);
            }
            if (values.Count >= 2)
                return new MinecraftCubeUV(null, null, null, null, null, null) { BoxU = values[0], BoxV = values[1] };
            return null;
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            MinecraftCubeFaceUV? north = null, south = null, east = null, west = null, up = null, down = null;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propName = reader.GetString()?.ToLowerInvariant();
                    if (!reader.Read()) break;
                    var faceUV = JsonSerializer.Deserialize<MinecraftCubeFaceUV>(ref reader, options);

                    switch (propName)
                    {
                        case "north": north = faceUV; break;
                        case "south": south = faceUV; break;
                        case "east": east = faceUV; break;
                        case "west": west = faceUV; break;
                        case "up": up = faceUV; break;
                        case "down": down = faceUV; break;
                    }
                }
            }

            return new MinecraftCubeUV(north, south, east, west, up, down);
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, MinecraftCubeUV value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
        writer.WriteStartObject();
        if (value.North != null) { writer.WritePropertyName("north"); JsonSerializer.Serialize(writer, value.North, options); }
        if (value.South != null) { writer.WritePropertyName("south"); JsonSerializer.Serialize(writer, value.South, options); }
        if (value.East  != null) { writer.WritePropertyName("east");  JsonSerializer.Serialize(writer, value.East,  options); }
        if (value.West  != null) { writer.WritePropertyName("west");  JsonSerializer.Serialize(writer, value.West,  options); }
        if (value.Up    != null) { writer.WritePropertyName("up");    JsonSerializer.Serialize(writer, value.Up,    options); }
        if (value.Down  != null) { writer.WritePropertyName("down");  JsonSerializer.Serialize(writer, value.Down,  options); }
        writer.WriteEndObject();
    }
}
