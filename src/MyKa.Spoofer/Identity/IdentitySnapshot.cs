using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyKa.Spoofer.Identity;

public sealed record IdentityValue(string Key, string Label, string? Value, string Scope, string? Problem = null);
public sealed record IdentitySnapshot(int SchemaVersion, DateTimeOffset CapturedUtc, string Computer, IdentityValue[] Values);
public enum DifferenceKind { Unchanged, Changed, Added, Removed, Unavailable }
public sealed record IdentityDifference(string Label, string? Before, string? After, DifferenceKind Kind);

internal static class IdentitySnapshots
{
    internal const int MaxFileBytes = 2_097_152;
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 24,
        Converters = { new JsonStringEnumConverter() },
    };

    public static void Validate(IdentitySnapshot snapshot)
    {
        if (snapshot.SchemaVersion != 1 || snapshot.CapturedUtc == default || !Text(snapshot.Computer, 256)
            || snapshot.Values is not { Length: > 0 and <= 2048 })
            throw new InvalidDataException("Invalid identity snapshot.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in snapshot.Values)
            if (value is null || !Text(value.Key, 512) || !Text(value.Label, 512) || !Text(value.Scope, 512)
                || !keys.Add(value.Key) || (value.Value is not null && !Text(value.Value, 8192))
                || (value.Problem is not null && !Text(value.Problem, 512))
                || ((value.Value is null) == (value.Problem is null)))
                throw new InvalidDataException("Invalid identity snapshot entry.");
    }

    public static IReadOnlyList<IdentityDifference> Compare(IdentitySnapshot before, IdentitySnapshot after)
    {
        Validate(before);
        Validate(after);
        if (!string.Equals(before.Computer, after.Computer, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("These snapshots name different computers.");
        var old = before.Values.ToDictionary(v => v.Key, StringComparer.Ordinal);
        var current = after.Values.ToDictionary(v => v.Key, StringComparer.Ordinal);
        var incompleteInventory = before.Values.Any(v => v.Problem is not null) || after.Values.Any(v => v.Problem is not null);
        return old.Keys.Union(current.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(key =>
        {
            old.TryGetValue(key, out var a);
            current.TryGetValue(key, out var b);
            var kind = a?.Problem is not null || b?.Problem is not null || (incompleteInventory && (a is null || b is null)) ? DifferenceKind.Unavailable
                : a is null ? DifferenceKind.Added : b is null ? DifferenceKind.Removed
                : string.Equals(a.Value, b.Value, StringComparison.Ordinal) ? DifferenceKind.Unchanged : DifferenceKind.Changed;
            return new IdentityDifference(b?.Label ?? a!.Label, a?.Value ?? a?.Problem, b?.Value ?? b?.Problem, kind);
        }).ToArray();
    }

    internal static bool Text(string? value, int max) => value is { Length: > 0 } && value.Length <= max
        && !value.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format);

    internal static string Display(string value) => new(value.Trim().Where(c => !char.IsControl(c)
        && char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Format).Take(8192).ToArray());

    public static IdentitySnapshot Read(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaxFileBytes) throw new InvalidDataException("The snapshot is too large.");
            var snapshot = JsonSerializer.Deserialize<IdentitySnapshot>(stream, JsonOptions)
                ?? throw new InvalidDataException("The snapshot is empty.");
            Validate(snapshot);
            return snapshot;
        }
        catch (JsonException) { throw new InvalidDataException("The snapshot JSON is invalid or unsupported."); }
    }
}
