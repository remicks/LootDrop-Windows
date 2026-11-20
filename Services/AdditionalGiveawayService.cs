using LootDrop.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LootDrop.Services;

// These feeds are independent of GamerPower. Only time-limited zero-price deals with
// a positive original price are included; normal free-to-play catalogs are excluded.
public sealed class AdditionalGiveawayService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = SafeHttp.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LootDrop/1.1 (desktop giveaway finder)");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public async Task<IReadOnlyList<GameOffer>> GetEpicGiveawaysAsync(CancellationToken token)
    {
        using var doc = JsonDocument.Parse(await SafeHttp.GetBytesAsync(Http, "https://store-site-backend-static.ak.epicgames.com/freeGamesPromotions?locale=en-US&country=US&allowCountries=US", 8 * 1024 * 1024, token));
        var elements = doc.RootElement.GetProperty("data").GetProperty("Catalog").GetProperty("searchStore").GetProperty("elements");
        var result = new List<GameOffer>();
        foreach (var element in elements.EnumerateArray().Take(500))
        {
            if (!Str(element, "title", out var title) || string.IsNullOrWhiteSpace(title) || title.Length > 512) continue;
            if (Str(element, "offerType", out var offerType) && !offerType.Equals("BASE_GAME", StringComparison.OrdinalIgnoreCase)) continue;
            if (!Prop(element, "price", out var price) || !Prop(price, "totalPrice", out var total) ||
                !Num(total, "originalPrice", out var original) || !Num(total, "discountPrice", out var discounted) ||
                original <= 0 || original > 100_000_000 || discounted != 0) continue;
            if (!Prop(element, "promotions", out var promotions) ||
                !Prop(promotions, "promotionalOffers", out var groups) || groups.ValueKind != JsonValueKind.Array) continue;
            DateTimeOffset? expiration = null;
            foreach (var group in groups.EnumerateArray())
            {
                if (!Prop(group, "promotionalOffers", out var offers) || offers.ValueKind != JsonValueKind.Array) continue;
                foreach (var offer in offers.EnumerateArray())
                {
                    if (!Prop(offer, "discountSetting", out var setting) || !Num(setting, "discountPercentage", out var percent) || percent != 0 ||
                        !Str(offer, "startDate", out var start) || !Str(offer, "endDate", out var end) ||
                        !DateTimeOffset.TryParse(start, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var from) ||
                        !DateTimeOffset.TryParse(end, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var to)) continue;
                    if (from <= DateTimeOffset.UtcNow && DateTimeOffset.UtcNow < to)
                        expiration = to;
                }
            }
            if (expiration is null) continue;
            var slug = "";
            if (Prop(element, "offerMappings", out var mappings) && mappings.ValueKind == JsonValueKind.Array)
                foreach (var mapping in mappings.EnumerateArray())
                    if (Str(mapping, "pageSlug", out var s) && !string.IsNullOrWhiteSpace(s)) { slug = s; break; }
            if (string.IsNullOrWhiteSpace(slug)) Str(element, "productSlug", out slug);
            if (string.IsNullOrWhiteSpace(slug)) Str(element, "urlSlug", out slug);
            slug = (slug ?? "").Trim('/');
            if (string.IsNullOrWhiteSpace(slug) || slug.Contains("..", StringComparison.Ordinal) || slug.Contains('?')) continue;
            var url = "https://store.epicgames.com/en-US/p/" + Uri.EscapeDataString(slug).Replace("%2F", "/", StringComparison.OrdinalIgnoreCase);
            var stableKey = Str(element, "id", out var epicId) ? epicId : slug;
            string imageUrl = "";
            if (Prop(element, "keyImages", out var images) && images.ValueKind == JsonValueKind.Array)
            {
                foreach (var img in images.EnumerateArray())
                {
                    if (Str(img, "url", out var image) && Uri.TryCreate(image, UriKind.Absolute, out _))
                    {
                        if (imageUrl.Length == 0) imageUrl = image;
                        if (Str(img, "type", out var type) && (type == "OfferImageWide" || type == "DieselStoreFrontWide"))
                        { imageUrl = image; break; }
                    }
                }
            }
            result.Add(new GameOffer
            {
                Id = StableId("epic:" + stableKey, 1_000_000_000),
                ProviderId = stableKey,
                Title = title.Trim(), Platforms = "PC, Epic Games Store", SourceName = "Epic Games",
                Description = "Free-to-keep promotion from the Epic Games Store (US). Check eligibility at checkout.",
                OriginalValue = original / 100m, Worth = (original / 100m).ToString("C", CultureInfo.GetCultureInfo("en-US")),
                ExpiresAtUtc = expiration.Value.UtcDateTime, PublishedAtUtc = DateTime.UtcNow,
                ClaimUrl = url, ImageUrl = imageUrl
            });
        }
        return result;
    }

    public async Task<IReadOnlyList<GameOffer>> GetCheapSharkFreeDealsAsync(CancellationToken token)
    {
        // A single page every 20 minutes, not a bulk crawler. CheapShark requires its own
        // redirect link for deals and prices are indicative, not checkout confirmation.
        using var doc = JsonDocument.Parse(await SafeHttp.GetBytesAsync(Http, "https://www.cheapshark.com/api/1.0/deals?upperPrice=0&onSale=1&pageSize=60&sortBy=Recent", 8 * 1024 * 1024, token));
        var result = new List<GameOffer>();
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;
        foreach (var element in doc.RootElement.EnumerateArray().Take(500))
        {
            if (!Str(element, "title", out var title) || string.IsNullOrWhiteSpace(title) || title.Length > 512 ||
                !Str(element, "dealID", out var dealId) || string.IsNullOrWhiteSpace(dealId) ||
                !Str(element, "salePrice", out var sale) ||
                !Str(element, "normalPrice", out var normal) ||
                !decimal.TryParse(sale, NumberStyles.Number, CultureInfo.InvariantCulture, out var salePrice) ||
                !decimal.TryParse(normal, NumberStyles.Number, CultureInfo.InvariantCulture, out var original) ||
                salePrice != 0m || original <= 0m || original > 1_000_000m) continue;
            var link = "https://www.cheapshark.com/redirect?dealID=" + Uri.EscapeDataString(dealId);
            Str(element, "thumb", out var thumb);
            result.Add(new GameOffer
            {
                Id = StableId("cheapshark:" + dealId, 1_400_000_000),
                ProviderId = dealId,
                Title = title.Trim(), Platforms = "PC, CheapShark Deals", SourceName = "CheapShark",
                Description = "Price aggregator reports a $0 deal. Store checkout is not independently verified; the deal may end without notice.",
                OriginalValue = original, Worth = original.ToString("C", CultureInfo.GetCultureInfo("en-US")),
                PublishedAtUtc = DateTime.UtcNow,
                ClaimUrl = link, ImageUrl = thumb ?? ""
            });
        }
        return result;
    }

    // Namespace-separated reproducible positive IDs for local library persistence.
    private static int StableId(string key, int offset)
    {
        uint hash = 2166136261;
        foreach (var ch in key) { hash ^= ch; hash *= 16777619; }
        return offset + (int)(hash % 300_000_000);
    }

    private static bool Prop(JsonElement source, string name, out JsonElement value)
    {
        if (source.ValueKind == JsonValueKind.Object && source.TryGetProperty(name, out value)) return true;
        value = default;
        return false;
    }
    private static bool Str(JsonElement source, string name, out string value)
    {
        value = "";
        if (!Prop(source, name, out var item) || item.ValueKind != JsonValueKind.String) return false;
        value = item.GetString() ?? "";
        return true;
    }
    private static bool Num(JsonElement source, string name, out long value)
    {
        value = 0;
        return Prop(source, name, out var item) && item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out value);
    }
}
