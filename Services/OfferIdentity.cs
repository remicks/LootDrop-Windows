using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using LootDrop.Models;

namespace LootDrop.Services;

public static class OfferIdentity
{
    public static string Key(GameOffer game) => Key(game.SourceName, game.Id, game.ProviderId);
    public static string Key(SavedGame game) => Key(game.SourceName, game.Id, game.ProviderId);
    public static string LegacyKey(string source, int id) => Key(source, id, "");
    private static string Key(string source, int id, string providerId)
    {
        var name = source.Trim().ToUpperInvariant();
        var native = providerId.Length == 0 ? "I:" + id.ToString(CultureInfo.InvariantCulture) : "N:" + providerId;
        return name.Length + ":" + name + ":" + native;
    }
    // Presentation only: never combine flags/URLs or strip edition, DLC, platform or key suffixes.
    public static IReadOnlyList<OfferGroup> Group(IEnumerable<GameOffer> offers) => offers
        .GroupBy(g => TitleKey(g.Title))
        .Select(group => new OfferGroup(group.First().Title, group.ToArray())).ToArray();
    private static string TitleKey(string title)
    {
        string normalized;
        try { normalized = title.Normalize(NormalizationForm.FormKC); }
        catch (ArgumentException) { normalized = title; }
        return string.Join(" ", normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    }
}
