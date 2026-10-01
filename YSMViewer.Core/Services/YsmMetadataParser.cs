using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace YSMViewer.Services;

public static class YsmMetadataParser
{
    private static readonly ILogger Logger = YsmLog.For(nameof(YsmMetadataParser));
    public static YsmMetadata? Parse(byte[]? ysmJson, byte[]? infoJson)
    {
        var ysm = TryParse(ysmJson, ParseYsmJson);
        var info = TryParse(infoJson, ParseInfoJson);
        return Merge(ysm, info);
    }

    private static YsmMetadata? TryParse(byte[]? data, Func<byte[], YsmMetadata?> parse)
    {
        if (data is not { Length: > 0 }) return null;
        try { return parse(data); }
        catch (Exception ex) { Logger.LogWarning(ex, "Failed to parse metadata JSON"); return null; }
    }

    private static YsmMetadata? Merge(YsmMetadata? primary, YsmMetadata? fallback)
    {
        if (primary is null) return fallback;
        if (fallback is null) return primary;

        return primary with
        {
            Name = string.IsNullOrWhiteSpace(primary.Name) ? fallback.Name : primary.Name,
            Tips = string.IsNullOrWhiteSpace(primary.Tips) ? fallback.Tips : primary.Tips,
            LicenseType = string.IsNullOrWhiteSpace(primary.LicenseType) ? fallback.LicenseType : primary.LicenseType,
            LicenseDescription = string.IsNullOrWhiteSpace(primary.LicenseDescription) ? fallback.LicenseDescription : primary.LicenseDescription,
            IsFree = primary.IsFree || fallback.IsFree,
            Authors = primary.Authors.Length > 0 ? primary.Authors : fallback.Authors,
            AuthorDetails = primary.AuthorDetails.Count > 0 ? primary.AuthorDetails : fallback.AuthorDetails,
            Links = primary.Links.Count > 0 ? primary.Links : fallback.Links,
        };
    }

    private static YsmMetadata? ParseYsmJson(byte[] data)
    {
        using var doc = JsonDocument.Parse(data);
        var root = doc.RootElement;

        string? name = null;
        string? tips = null;
        string? licenseType = null;
        string? licenseDescription = null;
        bool isFree = false;
        var authors = new List<string>();
        var authorDetails = new List<YsmAuthorInfo>();
        var links = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        float widthScale = 1f;
        float heightScale = 1f;

        if (root.TryGetProperty("metadata", out var meta))
        {
            if (meta.TryGetProperty("name", out var n)) name = n.GetString();
            if (meta.TryGetProperty("tips", out var t)) tips = t.GetString();
            if (meta.TryGetProperty("license", out var lic))
            {
                if (lic.TryGetProperty("type", out var lt)) licenseType = lt.GetString();
                if (lic.TryGetProperty("desc", out var ld)) licenseDescription = ld.GetString();
            }
            if (meta.TryGetProperty("authors", out var auths))
            {
                foreach (var a in auths.EnumerateArray())
                {
                    if (a.ValueKind == JsonValueKind.String)
                    {
                        var plain = a.GetString() ?? "";
                        authors.Add(plain);
                        authorDetails.Add(new YsmAuthorInfo(plain, null, null, null, new Dictionary<string, string>()));
                        continue;
                    }
                    if (a.ValueKind != JsonValueKind.Object) continue;

                    string? authorName = null, role = null, comment = null, avatar = null;
                    if (a.TryGetProperty("name", out var an)) authorName = an.GetString();
                    if (a.TryGetProperty("role", out var ar)) role = ar.GetString();
                    if (a.TryGetProperty("comment", out var ac)) comment = ac.GetString();
                    if (a.TryGetProperty("avatar", out var av)) avatar = av.GetString();

                    var contacts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (a.TryGetProperty("contact", out var contact) && contact.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var c in contact.EnumerateObject())
                        {
                            if (c.Value.ValueKind == JsonValueKind.String)
                                contacts[c.Name] = c.Value.GetString() ?? "";
                        }
                    }

                    authors.Add(authorName ?? "");
                    authorDetails.Add(new YsmAuthorInfo(
                        authorName ?? string.Empty,
                        string.IsNullOrWhiteSpace(role) ? null : role,
                        string.IsNullOrWhiteSpace(comment) ? null : comment,
                        string.IsNullOrWhiteSpace(avatar) ? null : avatar,
                        contacts));
                }
            }
            if (meta.TryGetProperty("link", out var link) && link.ValueKind == JsonValueKind.Object)
            {
                foreach (var l in link.EnumerateObject())
                {
                    if (l.Value.ValueKind == JsonValueKind.String)
                        links[l.Name] = l.Value.GetString() ?? "";
                }
            }
        }

        if (root.TryGetProperty("properties", out var props))
        {
            if (props.TryGetProperty("free", out var fr) && fr.ValueKind == JsonValueKind.True) isFree = true;
            if (props.TryGetProperty("width_scale", out var ws)
                && ws.TryGetSingle(out var wsv)) widthScale = wsv;
            if (props.TryGetProperty("height_scale", out var hs)
                && hs.TryGetSingle(out var hsv)) heightScale = hsv;
        }

        return new YsmMetadata(name, tips, licenseType, isFree, [.. authors], widthScale, heightScale,
            licenseDescription, authorDetails, links);
    }

    private static YsmMetadata? ParseInfoJson(byte[] data)
    {
        using var doc = JsonDocument.Parse(data);
        var root = doc.RootElement;

        string? name = null;
        string? tips = null;
        string? licenseType = null;
        bool isFree = false;
        var authors = new List<string>();

        if (root.TryGetProperty("name", out var n)) name = n.GetString();
        if (root.TryGetProperty("tips", out var t)) tips = t.GetString();
        if (root.TryGetProperty("license", out var lic)) licenseType = lic.GetString();
        if (root.TryGetProperty("free", out var fr)
            && (fr.ValueKind == JsonValueKind.True || (fr.ValueKind == JsonValueKind.Number && fr.TryGetSingle(out var fsv) && fsv > 0.5f)))
            isFree = true;
        if (root.TryGetProperty("authors", out var auths))
        {
            foreach (var a in auths.EnumerateArray())
            {
                if (a.ValueKind == JsonValueKind.String) authors.Add(a.GetString() ?? "");
                else if (a.TryGetProperty("name", out var an)) authors.Add(an.GetString() ?? "");
            }
        }

        return new YsmMetadata(name, tips, licenseType, isFree, [.. authors], 1f, 1f,
            null, [], new Dictionary<string, string>());
    }

    public sealed record YsmMetadata(
        string? Name,
        string? Tips,
        string? LicenseType,
        bool IsFree,
        string[] Authors,
        float WidthScale,
        float HeightScale,
        string? LicenseDescription = null,
        IReadOnlyList<YsmAuthorInfo> AuthorDetails = null!,
        IReadOnlyDictionary<string, string> Links = null!)
    {
        public IReadOnlyList<YsmAuthorInfo> AuthorDetails { get; init; } = AuthorDetails ?? [];
        public IReadOnlyDictionary<string, string> Links { get; init; } = Links ?? new Dictionary<string, string>();
    }
}

/// <summary>One rich metadata.authors[] entry from ysm.json (GUI YsmInfoPreview schema).</summary>
public sealed record YsmAuthorInfo(
    string Name,
    string? Role,
    string? Comment,
    string? Avatar,
    IReadOnlyDictionary<string, string> Contacts);
