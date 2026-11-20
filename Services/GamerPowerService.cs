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

public sealed class GamerPowerService
{
    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = SafeHttp.CreateClient();

        client.DefaultRequestHeaders.UserAgent.ParseAdd("LootDrop/1.0 (Windows desktop app)");
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        return client;
    }

    public async Task<IReadOnlyList<GameOffer>> GetActiveFreeGamesAsync(
        CancellationToken cancellationToken = default)
    {
        var bytes = await SafeHttp.GetBytesAsync(Http,
            "https://www.gamerpower.com/api/giveaways?type=game&sort-by=date", 8 * 1024 * 1024, cancellationToken);
        using var stream = new System.IO.MemoryStream(bytes);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var giveaways = await JsonSerializer.DeserializeAsync<List<GamerPowerGiveaway>>(
            stream, options, cancellationToken);

        if (giveaways is null)
            return Array.Empty<GameOffer>();

        return giveaways
            .Where(g => g.Id > 0 &&
                        !string.IsNullOrWhiteSpace(g.Title) &&
                        string.Equals(g.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .Take(500).Select(Map)
            .Where(g => !g.IsExpired)
            .GroupBy(g => g.Id)
            .Select(g => g.First())
            .ToList();
    }

    private static GameOffer Map(GamerPowerGiveaway item)
    {
        var worth = ParseWorth(item.Worth);
        var published = ParseUtcDate(item.PublishedDate) ?? DateTime.UtcNow;
        var endDate = ParseUtcDate(item.EndDate);

        return new GameOffer
        {
            Id = item.Id,
            Title = Limit(item.Title?.Trim() ?? "Untitled giveaway", 512),
            Description = Limit(item.Description?.Trim() ?? "", 16384),
            Platforms = Limit(item.Platforms?.Trim() ?? "Platform not listed", 512),
            Worth = item.Worth ?? "N/A",
            OriginalValue = worth,
            ExpiresAtUtc = endDate,
            PublishedAtUtc = published,
            ImageUrl = item.Image ?? item.Thumbnail ?? "",
            ClaimUrl = item.OpenGiveawayUrl ?? item.GamerPowerUrl ?? "",
            GamerPowerUrl = item.GamerPowerUrl ?? "",
            Users = item.Users
        };
    }

    private static decimal ParseWorth(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0m;

        var cleaned = value.Replace("$", "").Replace(",", "").Trim();

        return decimal.TryParse(
            cleaned,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var amount)
            ? Math.Clamp(amount, 0m, 1_000_000m)
            : 0m;
    }

    private static string Limit(string value, int max) => value.Length <= max ? value : value[..max];

    private static DateTime? ParseUtcDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Equals("N/A", StringComparison.OrdinalIgnoreCase))
            return null;

        if (DateTime.TryParseExact(
            value,
            "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed))
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        return null;
    }
}
