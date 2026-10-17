using System.Globalization;

namespace MyKa.Spoofer.Accounts;

public enum ExperienceResult { Closed, HandoffRequested, Failed }

/// <summary>One browser-to-desktop attempt. It never retains a launch URI or claims a running Player.</summary>
internal sealed class ExperienceLaunch
{
    private readonly string expectedUserId;
    private bool verified;
    private bool pageReady;
    private bool stopped;

    public ExperienceLaunch(string expectedUserId, string placeId)
    {
        if (!RobloxIdentity.ValidUserId(expectedUserId)) throw new ArgumentException("Invalid saved Roblox user ID.");
        if (!ValidPlaceId(placeId)) throw new ArgumentException("Enter a positive numeric Roblox place ID, not a URL.");
        this.expectedUserId = expectedUserId;
        PlaceId = placeId;
    }

    public string PlaceId { get; }
    public string PlaceUrl => "https://www.roblox.com/games/" + PlaceId;
    public ExperienceResult Result { get; private set; }
    public bool Verified => verified && !stopped;

    public static bool ValidPlaceId(string? value) => value is { Length: > 0 and <= 20 }
        && ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
        && id > 0 && id.ToString(CultureInfo.InvariantCulture) == value;

    public void ConfirmIdentity(RobloxIdentity identity)
    {
        if (stopped) throw new InvalidOperationException("Close this window and start a new attempt.");
        if (identity.UserId != expectedUserId)
        {
            Fail();
            throw new InvalidOperationException("The browser is signed in to a different account. Close this window and use Sign in again.");
        }
        verified = true;
    }

    public bool IsPlacePage(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.Host == "www.roblox.com" && uri.Port == 443 && uri.UserInfo.Length == 0
        && (uri.AbsolutePath == "/games/" + PlaceId || uri.AbsolutePath.StartsWith("/games/" + PlaceId + "/", StringComparison.Ordinal));

    public bool BeginNavigation(string url)
    {
        if (stopped) return false;
        // External navigation is followed by a separate event with the initiating origin and user gesture.
        if (pageReady && IsPlayerUri(url)) return true;
        pageReady = false;
        if ((!verified && RobloxIdentity.IsIdentityUrl(url)) || (verified && IsPlacePage(url))) return true;
        Fail();
        return false;
    }

    public void CompletePlace(string url, bool success)
    {
        if (stopped) return;
        pageReady = verified && success && IsPlacePage(url);
        if (!pageReady) Fail();
    }

    public bool TryRequestHandoff(string url, string origin, string source, bool userInitiated)
    {
        if (stopped || !pageReady || !verified || !userInitiated || !IsPlayerUri(url) || !IsPlacePage(source)
            || !(origin is "https://www.roblox.com" or "https://www.roblox.com/" || IsPlacePage(origin))) return false;
        Result = ExperienceResult.HandoffRequested;
        stopped = true; // Windows may launch even when its response is unknown. Never retry in this window.
        pageReady = false;
        return true;
    }

    public void Fail()
    {
        stopped = true;
        pageReady = false;
        if (Result != ExperienceResult.HandoffRequested) Result = ExperienceResult.Failed;
    }

    public static bool IsPlayerUri(string value) => value.Length is > 14 and <= 16384
        && value.StartsWith("roblox-player:", StringComparison.OrdinalIgnoreCase)
        && !value.Any(c => c < 0x21 || c > 0x7e || c is '"' or '<' or '>')
        && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "roblox-player";
}
