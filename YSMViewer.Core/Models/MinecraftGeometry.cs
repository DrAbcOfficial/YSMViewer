using System.Text.Json;
using System.Text.Json.Serialization;

namespace YSMViewer.Models;

[JsonConverter(typeof(MinecraftGeometryFileConverter))]
public sealed record MinecraftGeometryFile(
    [property: JsonPropertyName("format_version")] string? FormatVersion,
    [property: JsonPropertyName("minecraft:geometry")] List<MinecraftGeometry> Geometries);

/// <summary>
/// Accepts both Bedrock geometry layouts: the modern
/// <c>{"format_version":..., "minecraft:geometry":[...]}</c> envelope and the
/// legacy single-object <c>{"geometry.<name>": {...bones/cubes...}}</c> file
/// (mirrors Blockbench's bedrock_old codec and the GUI's pickGeometry).
/// </summary>
public sealed class MinecraftGeometryFileConverter : JsonConverter<MinecraftGeometryFile>
{
    public override MinecraftGeometryFile? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        if (root.TryGetProperty("minecraft:geometry", out var modern) && modern.ValueKind == JsonValueKind.Array)
        {
            string? formatVersion = root.TryGetProperty("format_version", out var fv) && fv.ValueKind == JsonValueKind.String
                ? fv.GetString()
                : null;
            return new MinecraftGeometryFile(
                formatVersion,
                Deserialize(modern, options, () => new List<MinecraftGeometry>()));
        }

        foreach (var prop in root.EnumerateObject())
        {
            if (!prop.Name.StartsWith("geometry.", StringComparison.OrdinalIgnoreCase))
                continue;
            if (prop.Value.ValueKind != JsonValueKind.Object)
                continue;
            if (!prop.Value.TryGetProperty("bones", out var bones) || bones.ValueKind != JsonValueKind.Array)
                continue;

            var value = prop.Value;
            var description = new MinecraftGeometryDescription(
                Identifier: prop.Name,
                TextureWidth: GetFloat(value, "texturewidth", 64f),
                TextureHeight: GetFloat(value, "textureheight", 64f),
                VisibleBoundsWidth: GetFloat(value, "visible_bounds_width", 0f),
                VisibleBoundsHeight: GetFloat(value, "visible_bounds_height", 0f),
                VisibleBoundsOffset: value.TryGetProperty("visible_bounds_offset", out var vbo) && vbo.ValueKind == JsonValueKind.Array
                    ? Deserialize(vbo, options, () => new List<float>())
                    : null);

            var geometry = new MinecraftGeometry(
                description,
                Deserialize(bones, options, () => new List<MinecraftBone>()));

            return new MinecraftGeometryFile(null, [geometry]);
        }

        return new MinecraftGeometryFile(null, []);
    }

    public override void Write(Utf8JsonWriter writer, MinecraftGeometryFile value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.FormatVersion is not null)
        {
            writer.WriteString("format_version", value.FormatVersion);
            writer.WritePropertyName("minecraft:geometry");
        }
        JsonSerializer.Serialize(writer, value.Geometries, options);
        writer.WriteEndObject();
    }

    private static T Deserialize<T>(JsonElement element, JsonSerializerOptions options, Func<T> fallback)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText(), options) ?? fallback();
        }
        catch (JsonException)
        {
            return fallback();
        }
    }

    private static float GetFloat(JsonElement element, string propertyName, float fallback)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number
            && property.TryGetSingle(out var value)
            ? value
            : fallback;
    }
}

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
    bool? Mirror = null,
    float? Inflate = null,
    List<MinecraftCube>? Cubes = null);

public sealed record MinecraftCube(
    List<float>? Origin = null,
    List<float>? Size = null,
    List<float>? Pivot = null,
    List<float>? Rotation = null,
    MinecraftCubeUV? Uv = null,
    float? Inflate = null,
    bool? Mirror = null);

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

    /// <summary>
    /// Expands box UV [u,v] into per-face UVs. With <paramref name="mirror"/>
    /// (cube mirror, falling back to the bone's), every face's U axis is
    /// flipped and East/West are swapped — the Blockbench mirror_uv layout.
    /// </summary>
    public MinecraftCubeUV Expand(float sizeX, float sizeY, float sizeZ, bool mirror = false)
    {
        if (!IsBoxUV) return this;

        float u = BoxU!.Value;
        float v = BoxV!.Value;
        float x = sizeX;
        float y = sizeY;
        float z = sizeZ;

        var north = new MinecraftCubeFaceUV(
            [u + z, v + z],
            [x, y]);
        var south = new MinecraftCubeFaceUV(
            [u + z + z + x, v + z],
            [x, y]);
        var east = new MinecraftCubeFaceUV(
            [u, v + z],
            [z, y]);
        var west = new MinecraftCubeFaceUV(
            [u + z + x, v + z],
            [z, y]);
        var up = new MinecraftCubeFaceUV(
            [u + z + x, v + z],
            [-x, -z]);
        var down = new MinecraftCubeFaceUV(
            [u + z + x + x, v],
            [-x, z]);

        if (mirror)
        {
            north = FlipU(north);
            south = FlipU(south);
            east = FlipU(east);
            west = FlipU(west);
            up = FlipU(up);
            down = FlipU(down);
            (east, west) = (west, east);
        }

        return new MinecraftCubeUV(north, south, east, west, up, down);
    }

    private static MinecraftCubeFaceUV FlipU(MinecraftCubeFaceUV face)
    {
        var uv = face.UvCoords!;
        var size = face.UvSize!;
        return new MinecraftCubeFaceUV([uv[0] + size[0], uv[1]], [-size[0], size[1]]);
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
                    var faceUV = ReadFaceUV(ref reader);

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
        WriteFaceUV(writer, "north", value.North);
        WriteFaceUV(writer, "south", value.South);
        WriteFaceUV(writer, "east", value.East);
        WriteFaceUV(writer, "west", value.West);
        WriteFaceUV(writer, "up", value.Up);
        WriteFaceUV(writer, "down", value.Down);
        writer.WriteEndObject();
    }

    private static MinecraftCubeFaceUV? ReadFaceUV(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            return null;

        List<float>? uv = null;
        List<float>? uvSize = null;
        string? material = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var prop = reader.GetString()?.ToLowerInvariant();
                if (!reader.Read()) break;

                switch (prop)
                {
                    case "uv":
                        uv = ReadFloatArray(ref reader);
                        break;
                    case "uv_size":
                        uvSize = ReadFloatArray(ref reader);
                        break;
                    case "material_instance":
                        material = reader.GetString();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }
        }

        return new MinecraftCubeFaceUV(uv, uvSize, material);
    }

    private static void WriteFaceUV(Utf8JsonWriter writer, string name, MinecraftCubeFaceUV? face)
    {
        if (face is null) return;
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        WriteFloatArray(writer, "uv", face.UvCoords);
        WriteFloatArray(writer, "uv_size", face.UvSize);
        if (face.MaterialInstance is not null)
            writer.WriteString("material_instance", face.MaterialInstance);
        writer.WriteEndObject();
    }

    private static List<float>? ReadFloatArray(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray) return null;
        var list = new List<float>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.Number)
                list.Add(reader.GetSingle());
        }
        return list;
    }

    private static void WriteFloatArray(Utf8JsonWriter writer, string name, List<float>? values)
    {
        if (values is not { Count: > 0 }) return;
        writer.WritePropertyName(name);
        writer.WriteStartArray();
        foreach (var v in values) writer.WriteNumberValue(v);
        writer.WriteEndArray();
    }
}
