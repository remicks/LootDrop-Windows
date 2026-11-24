using LootDrop.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace LootDrop.Services;

// JSON stored under %LOCALAPPDATA%\LootDrop, separate from the app installation folder.
public sealed class LocalLibraryService
{
    private readonly string _path;
    private bool _loadFailed;
    private const int MaxLibraryBytes = 8 * 1024 * 1024;
    public string DataDirectory => Path.GetDirectoryName(_path)!;

    public LocalLibraryService(string? dataDirectory = null)
    {
        _path = Path.Combine(dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LootDrop"), "library.json");
    }

    private void CheckPaths()
    {
        foreach (var path in new[] { Path.GetDirectoryName(_path)!, _path })
            if ((File.Exists(path) || Directory.Exists(path)) &&
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Library links are not supported.");
    }

    public LibraryState Load()
    {
        try
        {
            CheckPaths();
            if (!File.Exists(_path)) return new LibraryState();
            using var input = File.OpenRead(_path);
            if (input.Length > MaxLibraryBytes) throw new IOException("Library exceeds 8 MiB.");
            var state = JsonSerializer.Deserialize<LibraryState>(input)
                ?? throw new InvalidDataException("Library is empty or invalid.");
            Validate(state);
            return state;
        }
        catch { _loadFailed = true; throw; }
    }

    public static void Validate(LibraryState state)
    {
            if (state.DesktopDockWidth is not (360 or 400 or 480 or 560) ||
                state.DesktopDockMonitor == null || state.DesktopDockMonitor.Length > 32 ||
                state.DesktopDockMonitor.Any(char.IsControl))
                throw new InvalidDataException("Unsupported desktop docking preferences.");
            if (!FontCatalog.IsValid(state.AppFontKey)) throw new InvalidDataException("Unsupported embedded font choice.");
            if (!AppIconCatalog.IsValid(state.AppIconKey)) throw new InvalidDataException("Unsupported app icon choice.");
            if (state.SelectedPlatforms == null || state.SelectedStores == null ||
                state.SelectedPlatforms.Count > 32 || state.SelectedStores.Count > 32 ||
                state.SelectedPlatforms.Any(s => s == null || s.Length > 128) ||
                state.SelectedStores.Any(s => s == null || s.Length > 128) ||
                state.SelectedPlatform == null || state.SelectedPlatform.Length > 128 ||
                state.SelectedStore == null || state.SelectedStore.Length > 128)
                throw new InvalidDataException("Library contains unsupported filter settings.");
            if (state.Games == null || state.Games.Count > 10000 ||
                state.Games.Any(g => g == null || g.Title == null || g.Description == null ||
                    g.Platforms == null || g.ClaimUrl == null || g.ImageUrl == null || g.SourceName == null ||
                    g.Title.Length > 4096 || g.Description.Length > 65536 || g.Platforms.Length > 4096 ||
                    g.OriginalValue < 0 || g.OriginalValue > 1_000_000m ||
                    g.ClaimUrl.Length > 4096 || g.ImageUrl.Length > 4096 || g.SourceName.Length > 128 ||
                    g.ProviderId == null || g.ProviderId.Length > 2048) ||
                state.ThemeName == null || state.ThemeName.Length > 64 || !IsHex(state.CustomAccentHex) ||
                state.CustomThemeBasePreset == null || state.CustomThemeBasePreset.Length > 64 ||
                state.ThemeBackgroundPath == null || state.ThemeBackgroundPath.Length > 4096 ||
                state.CustomThemes == null || state.CustomThemes.Count > 256 ||
                state.CustomThemes.Any(t => t == null || string.IsNullOrWhiteSpace(t.Name) || t.Name.Length > 45 ||
                    !IsHex(t.Hex) || t.BackgroundPath == null || t.BackgroundPath.Length > 4096 ||
                    t.BasePreset == null || t.BasePreset.Length > 64))
                throw new InvalidDataException("Library contains unsupported data.");
    }

    private static bool IsHex(string? hex) => hex is { Length: 7 } && hex[0] == '#' && hex.Skip(1).All(char.IsAsciiHexDigit);

    public void Save(LibraryState state)
    {
        if (_loadFailed) throw new IOException("Saving disabled to preserve the unreadable library. Back it up and repair it before restarting.");
        Validate(state);
        CheckPaths();
        if (File.Exists(_path))
        {
            var existing = SafeLocalFiles.Read(_path, MaxLibraryBytes);
            new LibraryTransferService(this).DailyBackup(existing);
        }
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state, new JsonSerializerOptions { WriteIndented = true });
        if (bytes.Length > MaxLibraryBytes) throw new IOException("Library exceeds 8 MiB.");
        var folder = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(folder);
        var tempPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { output.Write(bytes); output.Flush(flushToDisk: true); }
            CheckPaths();
            File.Move(tempPath, _path, true);
        }
        finally { if (File.Exists(tempPath)) File.Delete(tempPath); }
    }

    internal void ReplaceReviewed(LibraryState state)
    {
        Validate(state); CheckPaths();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state, new JsonSerializerOptions { WriteIndented = true });
        if (bytes.Length > MaxLibraryBytes) throw new IOException("Library exceeds 8 MiB.");
        SafeLocalFiles.WriteAtomic(_path, bytes);
        _loadFailed = false;
    }
}

public sealed class LibraryState
{
    public List<SavedGame> Games { get; set; } = new();
    // Legacy single-platform setting is kept to read older library.json files.
    public string SelectedPlatform { get; set; } = "All";
    public List<string> SelectedPlatforms { get; set; } = new();
    public bool RememberFiltersOnLaunch { get; set; } = false;
    public string SelectedStore { get; set; } = "All";
    public int SortIndex { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;
    public List<string> SelectedStores { get; set; } = new();
    public string AppFontKey { get; set; } = "outfit";
    public string AppIconKey { get; set; } = "theme";
    public string ThemeName { get; set; } = "Purple";
    public string CustomAccentHex { get; set; } = "#A448F4";
    public string CustomThemeBasePreset { get; set; } = "Purple";
    public List<CustomThemeRecord> CustomThemes { get; set; } = new();
    public string ThemeBackgroundPath { get; set; } = "";
    public bool SidebarCollapsed { get; set; } = false;
    public bool SidebarRight { get; set; } = false;
    // Device-only preferences; imports never enable or change desktop docking.
    public bool DesktopDockEnabled { get; set; } = false;
    public bool DesktopDockRight { get; set; } = true;
    public string DesktopDockMonitor { get; set; } = "";
    public int DesktopDockWidth { get; set; } = 400;
    public int WindowOpacityPercent { get; set; } = 100;
    public int WallpaperDimPercent { get; set; } = 72;
    public bool LowGraphicsMode { get; set; } = false;
    public bool ForceSoftwareRendering { get; set; } = false;
    // Legacy About fields retained only for compatibility with existing library.json files.
    // About credits are no longer editable through the app and these values are ignored.
    public string AboutTesters { get; set; } = "";
    public string AboutContributors { get; set; } = "";
    public string AboutDiscordUrl { get; set; } = "";
    public string AboutSocialOneUrl { get; set; } = "";
    public string AboutSocialTwoUrl { get; set; } = "";
    public bool HideUnverified { get; set; }
}

public sealed class CustomThemeRecord
{
    public string Name { get; set; } = "";
    public string Hex { get; set; } = "#A448F4";
    public string BackgroundPath { get; set; } = "";
    public string BasePreset { get; set; } = "Purple";
}

public sealed class SavedGame
{
    public DateTime? LastFetchedUtc { get; set; }
    public string SourceName { get; set; } = "GamerPower";
    public int Id { get; set; }
    public string ProviderId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Platforms { get; set; } = "";
    public string Worth { get; set; } = "";
    public decimal OriginalValue { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime PublishedAtUtc { get; set; }
    public string ImageUrl { get; set; } = "";
    public string ClaimUrl { get; set; } = "";
    public string GamerPowerUrl { get; set; } = "";
    public int Users { get; set; }
    public bool IsWishlisted { get; set; }
    public bool IsClaimed { get; set; }
    public bool IsReportedNotFree { get; set; }

    public static SavedGame FromOffer(GameOffer offer) => new()
    {
        LastFetchedUtc = offer.LastFetchedUtc,
        SourceName = offer.SourceName, Id = offer.Id, ProviderId = offer.ProviderId, Title = offer.Title, Description = offer.Description,
        Platforms = offer.Platforms, Worth = offer.Worth, OriginalValue = offer.OriginalValue,
        ExpiresAtUtc = offer.ExpiresAtUtc, PublishedAtUtc = offer.PublishedAtUtc,
        ImageUrl = offer.ImageUrl, ClaimUrl = offer.ClaimUrl,
        GamerPowerUrl = offer.GamerPowerUrl, Users = offer.Users,
        IsWishlisted = offer.IsWishlisted, IsClaimed = offer.IsClaimed,
        IsReportedNotFree = offer.IsReportedNotFree
    };

    public GameOffer ToOffer(bool cached = false, string? note = null) => new()
    {
        LastFetchedUtc = LastFetchedUtc,
        SourceName = SourceName, Id = Id, ProviderId = ProviderId, Title = Title, Description = Description,
        Platforms = Platforms, Worth = Worth, OriginalValue = OriginalValue,
        ExpiresAtUtc = ExpiresAtUtc, PublishedAtUtc = PublishedAtUtc,
        ImageUrl = ImageUrl, ClaimUrl = ClaimUrl, GamerPowerUrl = GamerPowerUrl,
        Users = Users, IsWishlisted = IsWishlisted, IsClaimed = IsClaimed,
        IsReportedNotFree = IsReportedNotFree,
        IsInLiveFeed = false,
        IsCached = cached,
        AvailabilityNote = note ?? "Not in current live feed — saved for your records",
        PriceEvidence = cached ? "Cached listing · Price unverified · Refresh before opening" : "Saved record · Refresh before opening"
    };
}

