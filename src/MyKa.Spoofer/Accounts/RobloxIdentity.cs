using System.Globalization;
using System.Text.Json;

namespace MyKa.Spoofer.Accounts;

public sealed record RobloxIdentity(string UserId, string Username, string DisplayName)
{
    public const string LoginUrl = "https://www.roblox.com/login";
    public const string IdentityUrl = "https://users.roblox.com/v1/users/authenticated";

    public static bool IsIdentityUrl(string url) => string.Equals(url, IdentityUrl, StringComparison.Ordinal);
    public static bool IsAllowedPage(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || uri.Port != 443 || uri.UserInfo.Length != 0) return false;
        return uri.Host is "www.roblox.com" or "roblox.com" || IsIdentityUrl(url);
    }

    public static RobloxIdentity Parse(string finalUrl, int httpStatus, string json)
    {
        if (!IsIdentityUrl(finalUrl) || httpStatus != 200 || json.Length > 16_384)
            throw new InvalidDataException("Roblox did not confirm this account.");
        try
        {
            using var document = JsonDocument.Parse(json);
            var body = document.RootElement;
            var id = body.GetProperty("id").GetUInt64();
            var name = body.GetProperty("name").GetString();
            var display = body.GetProperty("displayName").GetString();
            if (id == 0 || !ValidName(name) || !ValidName(display))
                throw new InvalidDataException("Roblox returned an invalid account identity.");
            return new(id.ToString(CultureInfo.InvariantCulture), name!, display!);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException
            or FormatException or OverflowException)
        {
            throw new InvalidDataException("Roblox returned an unreadable account identity.");
        }
    }

    public static bool ValidName(string? value) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128 && !value.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) == UnicodeCategory.Format);
    public static bool ValidUserId(string? value) => value is not null
        && ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
        && id > 0 && id.ToString(CultureInfo.InvariantCulture) == value;
}
