using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LootDrop.Models;

namespace LootDrop.Services;

public sealed record FeedProvider(string Name, Func<CancellationToken, Task<IReadOnlyList<GameOffer>>> Fetch);
public sealed class FeedSnapshot
{
    public int Schema { get; set; } = 1;
    public string Source { get; set; } = "";
    public DateTime FetchedAtUtc { get; set; }
    public List<SavedGame> Games { get; set; } = new();
}
public sealed class FeedCacheService(string root)
{
    public static readonly string[] Sources = { "GamerPower", "Epic Games", "CheapShark" };
    private string PathFor(string source)
    {
        if (!Sources.Contains(source, StringComparer.Ordinal)) throw new InvalidDataException("Unknown cache source.");
        var directory = Path.Combine(root, "FeedCache");
        SafeLocalFiles.Check(root, directory);
        var path = Path.Combine(directory, source.Replace(" ", "") + ".json");
        SafeLocalFiles.Check(path);
        return path;
    }
    public FeedSnapshot? Load(string source)
    {
        var path = PathFor(source);
        if (!File.Exists(path)) return null;
        var snapshot = JsonSerializer.Deserialize<FeedSnapshot>(SafeLocalFiles.Read(path, 8 * 1024 * 1024))
            ?? throw new InvalidDataException("Invalid feed cache.");
        Validate(snapshot, source);
        return snapshot;
    }
    private static void Validate(FeedSnapshot snapshot, string source)
    {
        if (snapshot.Schema != 1 || snapshot.Source != source || snapshot.FetchedAtUtc.Kind != DateTimeKind.Utc ||
            snapshot.FetchedAtUtc > DateTime.UtcNow.AddMinutes(5) || snapshot.FetchedAtUtc < new DateTime(2020, 1, 1) ||
            snapshot.Games == null || snapshot.Games.Count > 500 || snapshot.Games.Any(g => g == null || g.SourceName != source || g.Id <= 0))
            throw new InvalidDataException("Unsupported feed cache.");
        LocalLibraryService.Validate(new LibraryState { Games = snapshot.Games });
    }
    public void Save(string source, IReadOnlyList<GameOffer> games)
    {
        var snapshot = new FeedSnapshot { Source = source, FetchedAtUtc = DateTime.UtcNow,
            Games = games.Take(500).GroupBy(OfferIdentity.Key).Select(g => SavedGame.FromOffer(g.First())).ToList() };
        Validate(snapshot, source);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot);
        if (bytes.Length > 8 * 1024 * 1024) throw new IOException("Cache exceeds size limit.");
        SafeLocalFiles.WriteAtomic(PathFor(source), bytes);
    }
    public static IReadOnlyList<GameOffer> OfflineOffers(FeedSnapshot snapshot) => snapshot.Games.Select(saved =>
    {
        var game = saved.ToOffer(cached: true, $"OFFLINE CACHE · {snapshot.Source} · Last fetched {snapshot.FetchedAtUtc.ToLocalTime():g} · Refresh required");
        game.LastFetchedUtc = snapshot.FetchedAtUtc;
        // Cache is never authoritative for personal flags or store verification.
        game.IsWishlisted = false; game.IsClaimed = false; game.IsReportedNotFree = false;
        return game;
    }).ToArray();
}
