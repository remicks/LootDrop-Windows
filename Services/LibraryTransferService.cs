using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LootDrop.Services;

public sealed record LibraryBackup(string Name, DateTime CreatedUtc, long Size);
public sealed class LibraryImportPreview
{
    internal byte[] Data { get; }
    internal string BeforeHash { get; }
    public int GameCount { get; }
    public int ThemeCount { get; }
    public string Summary { get; }
    internal LibraryImportPreview(byte[] data, string hash, LibraryState state, string oldSummary)
    {
        Data = data.ToArray(); BeforeHash = hash; GameCount = state.Games.Count; ThemeCount = state.CustomThemes.Count;
        var sample = string.Join("\n", state.Games.Take(5).Select(g => "• " + g.Title.Replace('\r', ' ').Replace('\n', ' ')[..Math.Min(g.Title.Length, 72)]));
        Summary = $"Incoming: {GameCount} saved offers, {ThemeCount} named themes.\n{oldSummary}\n\nFirst incoming offers:\n{sample}\n\nThis replaces your saved collection and named themes. Valid current appearance, startup and device settings stay unchanged. Imported wallpaper paths are cleared. A recovery backup is saved first. No links will open.";
    }
}
public sealed class LibraryExport
{
    public string Format { get; set; } = "LootDrop-Version2-Library";
    public int Schema { get; set; } = 1;
    public LibraryState Library { get; set; } = new();
}

public sealed class LibraryTransferService(LocalLibraryService library)
{
    public const int MaxBytes = 8 * 1024 * 1024;
    private string Root => library.DataDirectory;
    private string LibraryPath => Path.Combine(Root, "library.json");
    private string BackupDirectory => Path.Combine(Root, "Backups");
    private static bool BackupName(string name) => Regex.IsMatch(name, @"^(daily-[0-9]{8}|before-restore-[0-9]{8}T[0-9]{9}-[a-f0-9]{32})\.json$", RegexOptions.CultureInvariant);
    private void Check() => SafeLocalFiles.Check(Root, LibraryPath, BackupDirectory);
    private byte[]? Current()
    { Check(); return File.Exists(LibraryPath) ? SafeLocalFiles.Read(LibraryPath, MaxBytes) : null; }
    private static string Hash(byte[]? bytes) => bytes == null ? "missing" : Convert.ToHexString(SHA256.HashData(bytes));
    public static LibraryState Parse(byte[] bytes)
    {
        if (bytes.Length > MaxBytes) throw new IOException("Library exceeds 8 MiB.");
        var state = JsonSerializer.Deserialize<LibraryState>(bytes) ?? throw new InvalidDataException("Invalid library.");
        LocalLibraryService.Validate(state);
        if (state.Games.Select(OfferIdentity.Key).Distinct().Count() != state.Games.Count)
            throw new InvalidDataException("Duplicate provider offer identities in library.");
        return state;
    }
    public byte[] Export()
    {
        var current = Current();
        var state = current == null ? new LibraryState() : Parse(current);
        // Device settings and absolute wallpaper paths are not portable data.
        var portable = new LibraryState { Games = state.Games, CustomThemes = state.CustomThemes
            .Select(t => new CustomThemeRecord { Name = t.Name, Hex = t.Hex, BasePreset = t.BasePreset }).ToList() };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new LibraryExport { Library = portable }, new JsonSerializerOptions { WriteIndented = true });
        if (bytes.Length > MaxBytes) throw new IOException("Export exceeds 8 MiB.");
        return bytes;
    }
    public LibraryImportPreview PreviewExport(byte[] bytes)
    {
        if (bytes.Length > MaxBytes) throw new IOException("Import exceeds 8 MiB.");
        var envelope = JsonSerializer.Deserialize<LibraryExport>(bytes) ?? throw new InvalidDataException("Invalid export.");
        // Missing format/schema must not be accepted through property initializer defaults.
        using var document = JsonDocument.Parse(bytes);
        if (!document.RootElement.TryGetProperty("Format", out var format) || format.GetString() != "LootDrop-Version2-Library" ||
            !document.RootElement.TryGetProperty("Schema", out var schema) || !schema.TryGetInt32(out var version) || version != 1 ||
            !document.RootElement.TryGetProperty("Library", out _) || envelope.Library == null)
            throw new InvalidDataException("Only supported LootDrop version 2 exports can be imported.");
        return PreviewLibrary(JsonSerializer.SerializeToUtf8Bytes(envelope.Library));
    }
    public LibraryImportPreview PreviewBackup(string name)
    {
        Check();
        if (!BackupName(name)) throw new InvalidDataException("Invalid backup name.");
        return PreviewLibrary(SafeLocalFiles.Read(Path.Combine(BackupDirectory, name), MaxBytes));
    }
    private LibraryImportPreview PreviewLibrary(byte[] bytes)
    {
        var state = Parse(bytes);
        var before = Current();
        var summary = "Current library is empty.";
        if (before != null)
        {
            try { var old = Parse(before); summary = $"Current: {old.Games.Count} saved offers, {old.CustomThemes.Count} named themes."; }
            catch (Exception ex) when (ex is JsonException or InvalidDataException)
            { summary = "Current library is unreadable; its original bytes will be backed up. Unreadable device settings cannot be recovered and will use defaults."; }
        }
        return new LibraryImportPreview(bytes, Hash(before), state, summary);
    }
    public LibraryState Apply(LibraryImportPreview preview)
    {
        var incoming = Parse(preview.Data);
        var before = Current();
        if (Hash(before) != preview.BeforeHash) throw new IOException("Library changed since preview. Review the import again.");
        var state = new LibraryState();
        if (before != null)
        {
            try { state = Parse(before); }
            catch (Exception ex) when (ex is JsonException or InvalidDataException) { }
            Backup(before, "before-restore-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N"));
        }
        state.Games = incoming.Games;
        state.CustomThemes = incoming.CustomThemes.Select(t => new CustomThemeRecord { Name = t.Name, Hex = t.Hex, BasePreset = t.BasePreset }).ToList();
        // Never copy imported startup, renderer, wallpaper or other device preferences.
        library.ReplaceReviewed(state);
        return state;
    }
    public void DailyBackup(byte[] current)
    {
        Parse(current); // Never turn a corrupt library into the only normal recovery snapshot.
        Backup(current, "daily-" + DateTime.UtcNow.ToString("yyyyMMdd"), skipExisting: true);
    }
    private void Backup(byte[] bytes, string stem, bool skipExisting = false)
    {
        Check(); Directory.CreateDirectory(BackupDirectory);
        var path = Path.Combine(BackupDirectory, stem + ".json");
        SafeLocalFiles.Check(path);
        if (skipExisting && File.Exists(path))
        {
            Parse(SafeLocalFiles.Read(path, MaxBytes));
            return;
        }
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { output.Write(bytes); output.Flush(true); }
            File.Move(temporary, path, overwrite: false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        // Only direct, controlled backup names in this managed folder are eligible for retention cleanup.
        foreach (var old in Backups().Skip(20))
        {
            var oldPath = Path.Combine(BackupDirectory, old.Name);
            SafeLocalFiles.Check(oldPath); File.Delete(oldPath);
        }
    }
    public IReadOnlyList<LibraryBackup> Backups()
    {
        Check(); if (!Directory.Exists(BackupDirectory)) return Array.Empty<LibraryBackup>();
        return new DirectoryInfo(BackupDirectory).EnumerateFiles("*.json", SearchOption.TopDirectoryOnly)
            .Where(f => BackupName(f.Name) && (f.Attributes & FileAttributes.ReparsePoint) == 0)
            .OrderByDescending(f => f.CreationTimeUtc).ThenByDescending(f => f.Name)
            .Select(f => new LibraryBackup(f.Name, f.CreationTimeUtc, f.Length)).ToArray();
    }
}
