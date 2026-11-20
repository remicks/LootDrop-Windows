using System;
using System.Linq;

namespace LootDrop.Services;

public static class UrlSafety
{
    public static bool IsDomain(string host, string domain) =>
        host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);

    // Syntactic browser boundary; this does not certify destination content or DNS.
    public static bool TryHttps(string? raw, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 4096 ||
            raw.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format || c == '\\') ||
            !Uri.TryCreate(raw, UriKind.Absolute, out var parsed) ||
            parsed.Scheme != Uri.UriSchemeHttps || parsed.Port != 443 ||
            parsed.UserInfo.Length != 0 || parsed.HostNameType != UriHostNameType.Dns)
            return false;
        var host = parsed.IdnHost.TrimEnd('.');
        if (!host.Contains('.') || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            new[] { "local", "internal", "lan", "home", "test", "invalid", "example" }
                .Any(s => host.EndsWith("." + s, StringComparison.OrdinalIgnoreCase))) return false;
        uri = parsed;
        return true;
    }

    public static bool TryImage(string? raw, out Uri uri)
    {
        if (!TryHttps(raw, out uri)) return false;
        var host = uri.IdnHost;
        // Provider/CDN domains only. The transport refuses all redirects.
        return new[] { "gamerpower.com", "cdn1.epicgames.com", "cdn2.unrealengine.com",
            "shared.akamai.steamstatic.com", "cdn.akamai.steamstatic.com",
            "cdn.cloudflare.steamstatic.com", "shared.fastly.steamstatic.com",
            "cdn.fastly.steamstatic.com", "cheapshark.com" }
            .Any(domain => IsDomain(host, domain));
    }
}
