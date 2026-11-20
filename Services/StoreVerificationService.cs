using LootDrop.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LootDrop.Services;

// Conservative store verification. A working URL, a GamerPower listing, and a free-to-play
// price are NOT evidence of a time-limited free-to-keep giveaway.
public sealed class StoreVerificationService
{
    private static readonly HttpClient Http = SafeHttp.CreateClient();
    private readonly SemaphoreSlim _epicGate = new(1, 1);
    private HashSet<string>? _epicTitles;
    private DateTime _epicFetchedUtc;

    public async Task<(VerificationResult result, string detail)> VerifyAsync(GameOffer game, CancellationToken token)
    {
        if (game.IsExpired) return (VerificationResult.Expired, "Giveaway expiration has passed");
        if (game.IsReportedNotFree) return (VerificationResult.ReportedNotFree, "Reported not free on this computer");
        if (game.IsCached || !game.IsInLiveFeed) return (VerificationResult.Unverified, "Cached or saved listing requires a live refresh");
        try
        {
            if (IsEpic(game)) return await VerifyEpicAsync(game, token);
            if (IsSteam(game)) return await VerifySteamAsync(game, token);
            if (IsGog(game)) return (VerificationResult.Unverified, "GOG price not independently verified · Check checkout");
            return (VerificationResult.Unverified, "No supported store price verification · Check checkout");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            return (VerificationResult.Unverified, "Store verification unavailable · Check checkout");
        }
    }

    private static bool IsEpic(GameOffer g) => IsHost(g.ClaimUrl, "store.epicgames.com");
    private static bool IsSteam(GameOffer g) => IsHost(g.ClaimUrl, "store.steampowered.com");
    private static bool IsGog(GameOffer g) => IsHost(g.ClaimUrl, "gog.com");
    private static bool IsHost(string raw, string host) => UrlSafety.TryHttps(raw, out var uri) &&
        uri.IdnHost.Equals(host, StringComparison.OrdinalIgnoreCase);

    private async Task<(VerificationResult, string)> VerifyEpicAsync(GameOffer game, CancellationToken token)
    {
        var titles = await GetEpicFreeTitlesAsync(token);
        if (titles == null) return (VerificationResult.Unverified, "Epic promotion feed unavailable · Check checkout");
        game.LastPriceCheckUtc = _epicFetchedUtc;
        // Only exact normalized names count: never infer identity from substring similarity.
        var normalized = NormalizeTitle(game.Title);
        if (titles.Contains(normalized) && game.SourceName == "Epic Games")
            return (VerificationResult.VerifiedFree, "Epic US catalog: active 100% off promotion · Regional/account restrictions may apply");
        return (VerificationResult.Unverified, "Not matched to Epic's active free-game promotions · Check checkout");
    }

    private async Task<HashSet<string>?> GetEpicFreeTitlesAsync(CancellationToken token)
    {
        if (_epicTitles != null && DateTime.UtcNow - _epicFetchedUtc < TimeSpan.FromMinutes(15)) return _epicTitles;
        await _epicGate.WaitAsync(token);
        try
        {
            if (_epicTitles != null && DateTime.UtcNow - _epicFetchedUtc < TimeSpan.FromMinutes(15)) return _epicTitles;
            using var document = JsonDocument.Parse(await SafeHttp.GetBytesAsync(Http, "https://store-site-backend-static.ak.epicgames.com/freeGamesPromotions?locale=en-US&country=US&allowCountries=US", 8 * 1024 * 1024, token));
            var elements = document.RootElement.GetProperty("data").GetProperty("Catalog")
                .GetProperty("searchStore").GetProperty("elements");
            var matched = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in elements.EnumerateArray())
            {
                if (!TryGet(item, "title", out var titleValue) || titleValue.ValueKind != JsonValueKind.String) continue;
                if (!TryGet(item, "price", out var price) || !TryGet(price, "totalPrice", out var total)) continue;
                if (!TryGet(total, "originalPrice", out var original) || !TryGet(total, "discountPrice", out var discount)) continue;
                if (original.ValueKind != JsonValueKind.Number || discount.ValueKind != JsonValueKind.Number ||
                    original.GetInt64() <= 0 || discount.GetInt64() != 0) continue;
                if (!TryGet(item, "promotions", out var promotions) ||
                    !TryGet(promotions, "promotionalOffers", out var groups) || groups.ValueKind != JsonValueKind.Array) continue;
                bool active = false;
                foreach (var group in groups.EnumerateArray())
                {
                    if (!TryGet(group, "promotionalOffers", out var offers) || offers.ValueKind != JsonValueKind.Array) continue;
                    foreach (var offer in offers.EnumerateArray())
                    {
                        if (!TryGet(offer, "discountSetting", out var setting) ||
                            !TryGet(setting, "discountPercentage", out var pct) ||
                            pct.ValueKind != JsonValueKind.Number || pct.GetInt32() != 0) continue;
                        if (!TryGet(offer, "startDate", out var start) || !TryGet(offer, "endDate", out var end) ||
                            !DateTimeOffset.TryParse(start.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var startUtc) ||
                            !DateTimeOffset.TryParse(end.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var endUtc)) continue;
                        var now = DateTimeOffset.UtcNow;
                        if (startUtc <= now && now < endUtc) active = true;
                    }
                }
                if (active) matched.Add(NormalizeTitle(titleValue.GetString() ?? ""));
            }
            _epicTitles = matched;
            _epicFetchedUtc = DateTime.UtcNow;
            return matched;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { return null; }
        finally { _epicGate.Release(); }
    }

    private static async Task<(VerificationResult, string)> VerifySteamAsync(GameOffer game, CancellationToken token)
    {
        // Only check a Steam app when the exact app ID exists in the claim link. Never guess IDs from names.
        if (!UrlSafety.TryHttps(game.ClaimUrl, out var uri) || uri.IdnHost != "store.steampowered.com")
            return (VerificationResult.Unverified, "Not a direct Steam store URL");
        var match = Regex.Match(uri.AbsolutePath, @"^/app/([0-9]{1,10})(?:/|$)", RegexOptions.CultureInvariant);
        if (!match.Success) return (VerificationResult.Unverified, "Steam app ID not present in claim link · Check checkout");
        using var doc = JsonDocument.Parse(await SafeHttp.GetBytesAsync(Http, $"https://store.steampowered.com/api/appdetails?appids={match.Groups[1].Value}&cc=us&l=en", 8 * 1024 * 1024, token));
        game.LastPriceCheckUtc = DateTime.UtcNow;
        var root = doc.RootElement.GetProperty(match.Groups[1].Value);
        if (!TryGet(root, "success", out var success) || success.ValueKind != JsonValueKind.True ||
            !TryGet(root, "data", out var data))
            return (VerificationResult.Unverified, "Steam catalog entry unavailable · Check checkout");
        if (TryGet(data, "is_free", out var isFree) && isFree.ValueKind == JsonValueKind.True)
            return (VerificationResult.NotGiveaway, "Steam lists this as free-to-play, not a paid game temporarily free");
        if (!TryGet(data, "price_overview", out var price) ||
            !TryGet(price, "initial", out var initial) || !TryGet(price, "final", out var final) ||
            initial.ValueKind != JsonValueKind.Number || final.ValueKind != JsonValueKind.Number)
            return (VerificationResult.Unverified, "Steam price information unavailable · Check checkout");
        var original = initial.GetInt64();
        var current = final.GetInt64();
        if (original > 0 && current == 0)
            return (VerificationResult.VerifiedFree, "Steam US catalog: paid game currently priced $0 · Check checkout");
        if (current > 0)
            return (VerificationResult.NotFree, "Steam US catalog currently shows a nonzero price");
        return (VerificationResult.Unverified, "Steam price could not establish a giveaway");
    }

    private static bool TryGet(JsonElement element, string key, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out value)) return true;
        value = default;
        return false;
    }

    private static string NormalizeTitle(string title)
    {
        var lower = title.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        return Regex.Replace(lower, "[^a-z0-9]+", " ").Trim();
    }
}
