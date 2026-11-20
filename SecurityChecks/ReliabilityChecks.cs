using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LootDrop.Models;
using LootDrop.Services;
using LootDrop.Views;

internal static class ReliabilityChecks
{
    private static GameOffer Offer(string source, int id, string native = "", string title = "Fixture game") => new()
    { SourceName = source, Id = id, ProviderId = native, Title = title, Platforms = "PC", PublishedAtUtc = DateTime.UtcNow, ClaimUrl = "https://independent-store.com/game" };
    public static void RunCore(Action<bool, string> check, string root)
    {
        void Reject(Action action, string label)
        {
            try { action(); } catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException) { check(true, label); return; }
            throw new Exception("FAILED: " + label);
        }
        var a = Offer("GamerPower", 5); var b = Offer("Epic Games", 5);
        check(OfferIdentity.Key(a) != OfferIdentity.Key(b), "Same numeric ID across providers stays separate");
        check(OfferIdentity.Key(Offer("Epic Games", 5, "native-a")) != OfferIdentity.Key(Offer("Epic Games", 5, "native-b")), "Native IDs prevent within-provider hash collisions");
        var groups = OfferIdentity.Group(new[] { a, b, Offer("CheapShark", 5, "deal", "Fixture game Deluxe Edition") });
        check(groups.Count == 2 && groups[0].Routes.Count == 2, "Presentation groups exact titles but preserves edition variants and every route");
        a.IsClaimed = true;
        check(!b.IsClaimed, "Grouping never shares personal flags between routes");
        check(OfferIdentity.Group(new[] { Offer("GamerPower", 8, title: "Truncated \ud83d") }).Count == 1, "Truncated Unicode title cannot crash grouping");

        var folder = Path.Combine(root, "reliability");
        var library = new LocalLibraryService(folder);
        var transfer = new LibraryTransferService(library);
        library.Save(new LibraryState { Games = new() { SavedGame.FromOffer(a) }, ThemeName = "Mint Sorbet", StartWithWindows = false });
        library.Save(new LibraryState { Games = new() { SavedGame.FromOffer(a), SavedGame.FromOffer(b) }, ThemeName = "Mint Sorbet" });
        check(transfer.Backups().Count == 1, "First daily change creates a recovery snapshot");
        library.Save(library.Load());
        check(transfer.Backups().Count == 1, "Repeated changes do not churn daily backups");
        var dailyPath = Path.Combine(folder, "Backups", transfer.Backups().Single().Name);
        var dailyBytes = File.ReadAllBytes(dailyPath);
        var currentBytes = File.ReadAllBytes(Path.Combine(folder, "library.json"));
        File.WriteAllText(dailyPath, "{partial");
        Reject(() => library.Save(library.Load()), "Incomplete daily snapshot cannot be silently trusted");
        check(currentBytes.SequenceEqual(File.ReadAllBytes(Path.Combine(folder, "library.json"))), "A failed backup preserves the current library");
        File.WriteAllBytes(dailyPath, dailyBytes);
        var before = File.ReadAllBytes(Path.Combine(folder, "library.json"));
        var export = transfer.Export();
        var preview = transfer.PreviewExport(export);
        check(preview.GameCount == 2 && before.SequenceEqual(File.ReadAllBytes(Path.Combine(folder, "library.json"))), "Import preview does not change the current library");
        Reject(() => transfer.PreviewExport(System.Text.Encoding.UTF8.GetBytes("{\"Games\":[]}")), "Raw or foreign JSON cannot masquerade as a portable export");
        Reject(() => transfer.PreviewBackup("..\\library.json"), "Backup path traversal rejected");
        Reject(() => transfer.PreviewExport(new byte[LibraryTransferService.MaxBytes + 1]), "Oversized import rejected before parsing");
        var malicious = new LibraryExport { Library = new LibraryState { StartWithWindows = true, ThemeName = "Neon Arcade",
            ThemeBackgroundPath = @"\\untrusted\share\image.png", Games = new() { SavedGame.FromOffer(b) },
            CustomThemes = new() { new CustomThemeRecord { Name = "Imported", Hex = "#123456", BackgroundPath = @"\\untrusted\share\image.png", BasePreset = "Arctic Day" } } } };
        var reviewed = transfer.PreviewExport(JsonSerializer.SerializeToUtf8Bytes(malicious));
        var applied = transfer.Apply(reviewed);
        check(applied.Games.Count == 1 && !applied.StartWithWindows && applied.ThemeName == "Mint Sorbet", "Reviewed replacement imports collection but preserves local startup/appearance settings");
        check(applied.CustomThemes.Single().BackgroundPath == "" && applied.ThemeBackgroundPath == "", "Imported wallpaper paths never activate");
        check(transfer.Backups().Any(x => x.Name.StartsWith("before-restore-")), "Applying replacement creates a recovery backup first");
        var stale = transfer.PreviewExport(export);
        var changed = library.Load(); changed.ThemeName = "Rosewater"; library.Save(changed);
        Reject(() => transfer.Apply(stale), "Changed target library invalidates an old import preview");
        var duplicate = new LibraryExport { Library = new LibraryState { Games = new() { SavedGame.FromOffer(b), SavedGame.FromOffer(b) } } };
        Reject(() => transfer.PreviewExport(JsonSerializer.SerializeToUtf8Bytes(duplicate)), "Duplicate identities in imported data are rejected");
        var recoveryBytes = transfer.Export();
        File.WriteAllText(Path.Combine(folder, "library.json"), "{corrupt");
        var broken = new LocalLibraryService(folder);
        try { broken.Load(); } catch (JsonException) { }
        var rescue = new LibraryTransferService(broken);
        var rescuePreview = rescue.PreviewExport(recoveryBytes);
        rescue.Apply(rescuePreview);
        check(broken.Load().Games.Count == 1, "Reviewed restore can recover an unreadable library");
        check(rescue.Backups().Any(x => File.ReadAllText(Path.Combine(folder, "Backups", x.Name)) == "{corrupt"), "Recovery preserves the corrupt original bytes in a snapshot");
        for (var i = 0; i < 22; i++) rescue.Apply(rescue.PreviewExport(recoveryBytes));
        check(rescue.Backups().Count == 20, "Managed snapshot retention is bounded to twenty files");

        var cache = new FeedCacheService(folder);
        cache.Save("GamerPower", new[] { a });
        var cached = FeedCacheService.OfflineOffers(cache.Load("GamerPower")!);
        check(cached.Single().IsCached && !cached.Single().ClaimButtonEnabled && !cached.Single().IsClaimed, "Cache reload is stale, unclaimable and not authoritative for personal flags");
        cache.Save("GamerPower", Array.Empty<GameOffer>());
        check(cache.Load("GamerPower")!.Games.Count == 0, "Successful empty refresh retires previous cache content");
        Reject(() => cache.Load("..\\library"), "Cache path/source traversal rejected");
        File.WriteAllText(Path.Combine(folder, "FeedCache", "GamerPower.json"), "{bad");
        Reject(() => cache.Load("GamerPower"), "Malformed cache is rejected without touching the library");
    }

    public static void RunWindow(Action<bool, string> check, string root, bool screenshots)
    {
        var folder = Path.Combine(root, "reliability-window");
        var library = new LocalLibraryService(folder);
        library.Save(new LibraryState { Games = new()
        {
            new SavedGame { SourceName = "GamerPower", Id = 55, Title = "Fixture game", IsWishlisted = true },
            new SavedGame { SourceName = "Epic Games", Id = 55, Title = "Fixture game", IsClaimed = true }
        } });
        var cache = new FeedCacheService(folder);
        cache.Save("CheapShark", new[] { Offer("CheapShark", 2, "old-deal", "Offline deal") });
        var liveA = Offer("GamerPower", 55); var liveB = Offer("Epic Games", 55, "epic-native");
        var emptyRefresh = false;
        var providers = new FeedProvider[]
        {
            new("GamerPower", _ => Task.FromResult<IReadOnlyList<GameOffer>>(emptyRefresh ? Array.Empty<GameOffer>() : new[] { liveA })),
            new("Epic Games", _ => Task.FromResult<IReadOnlyList<GameOffer>>(emptyRefresh ? Array.Empty<GameOffer>() : new[] { liveB })),
            new("CheapShark", _ => emptyRefresh ? Task.FromResult<IReadOnlyList<GameOffer>>(Array.Empty<GameOffer>()) : Task.FromException<IReadOnlyList<GameOffer>>(new IOException("Fixture outage")))
        };
        var window = new MainWindow(library, enableBackgroundWork: false, providers);
        window.Show(); Dispatcher.UIThread.RunJobs();
        check(window.FindControl<ItemsControl>("OffersList")!.Items.OfType<OfferGroup>().SelectMany(g => g.Routes).Any(g => g.IsCached && !g.ClaimButtonEnabled), "Discovery shows offline cached offers with claims disabled");
        window.RefreshAsync().GetAwaiter().GetResult(); Dispatcher.UIThread.RunJobs();
        var groups = window.FindControl<ItemsControl>("OffersList")!.Items.OfType<OfferGroup>().ToArray();
        check(groups.Any(g => g.Title == "Fixture game" && g.Routes.Count == 2), "Actual window groups refreshed provider routes together");
        check(liveA.IsWishlisted && !liveA.IsClaimed && liveB.IsClaimed && !liveB.IsWishlisted, "Refresh preserves per-provider flags without cross-provider collisions");
        check(library.Load().Games.Any(g => g.SourceName == "Epic Games" && g.ProviderId == "epic-native"), "Unambiguous legacy provider IDs migrate without losing flags");
        check(groups.SelectMany(g => g.Routes).Any(g => g.SourceName == "CheapShark" && g.IsCached && !g.ClaimButtonEnabled), "One provider outage preserves its disabled cache beside healthy live sources");
        check(window.FindControl<TextBlock>("SourceHealthText")!.Text!.Contains("Unavailable") && window.FindControl<TextBlock>("SourceHealthText")!.Text!.Contains("Last success"), "Source-health panel shows both failures and last-success times");
        if (screenshots)
        {
            window.Height = 1400; Dispatcher.UIThread.RunJobs();
            using var frame = Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);
            frame?.Save(Path.Combine(AppContext.BaseDirectory, "theme-screenshots", "Reliability-Grouped-Offers.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }
        emptyRefresh = true;
        window.RefreshAsync().GetAwaiter().GetResult(); Dispatcher.UIThread.RunJobs();
        check(window.FindControl<ItemsControl>("OffersList")!.Items.Count == 0, "Successful empty provider refresh clears stale discovery results");
        window.FindControl<Button>("NavWishlist")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var savedRoutes = window.FindControl<ItemsControl>("OffersList")!.Items.OfType<OfferGroup>().SelectMany(g => g.Routes).ToArray();
        check(savedRoutes.Length == 1 && savedRoutes[0].IsWishlisted && !savedRoutes[0].ClaimButtonEnabled, "Empty refresh preserves personal library as unclaimable saved records");
        check(window.FindControl<TextBlock>("LibraryTransferStatus") != null, "Backup and transfer settings are wired into the actual window");
        window.AllowExit(); window.Close();
        var collisionLibrary = new LocalLibraryService(Path.Combine(root, "collision-window"));
        collisionLibrary.Save(new LibraryState { Games = new() { new SavedGame { Id = 99, SourceName = "Epic Games", Title = "Ambiguous legacy", IsWishlisted = true } } });
        var nativeA = Offer("Epic Games", 99, "a", "Native title"); var nativeB = Offer("Epic Games", 99, "b", "Native title");
        var collisionWindow = new MainWindow(collisionLibrary, false, new[]
        { new FeedProvider("Epic Games", _ => Task.FromResult<IReadOnlyList<GameOffer>>(new[] { nativeA, nativeB })) });
        collisionWindow.RefreshAsync().GetAwaiter().GetResult();
        check(!nativeA.IsWishlisted && !nativeB.IsWishlisted && collisionLibrary.Load().Games.Single().ProviderId == "", "Ambiguous legacy hash collision never guesses which native route owns the saved flag");
        collisionWindow.AllowExit(); collisionWindow.Close();
    }
}
