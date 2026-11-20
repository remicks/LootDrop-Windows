using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LootDrop.Models;
using LootDrop.Services;

namespace LootDrop.Views;

public partial class MainWindow
{
    public Task RefreshAsync() => LoadGiveawaysAsync();
    private void SeedOfflineOffers()
    {
        foreach (var source in FeedCacheService.Sources)
        {
            try
            {
                var snapshot = _cache.Load(source);
                if (snapshot != null)
                {
                    foreach (var game in FeedCacheService.OfflineOffers(snapshot)) AllOffers.Add(game);
                    _sourceHealth[source] = $"{source}: Cached · Last success {snapshot.FetchedAtUtc.ToLocalTime():g}";
                }
                else _sourceHealth[source] = source + ": Not refreshed · No cache";
            }
            catch { _sourceHealth[source] = source + ": Cache unreadable · Live refresh required"; }
        }
        foreach (var saved in _saved.Values)
        {
            var cached = AllOffers.FirstOrDefault(g => OfferIdentity.Key(g) == OfferIdentity.Key(saved));
            if (cached == null) AllOffers.Add(saved.ToOffer());
            else { cached.IsWishlisted = saved.IsWishlisted; cached.IsClaimed = saved.IsClaimed; cached.IsReportedNotFree = saved.IsReportedNotFree; }
        }
        UpdateSourceHealth();
    }
    private string CachedAge(string source)
    {
        try { var snapshot = _cache.Load(source); return snapshot == null ? "No cached results" : $"Offline cache from {snapshot.FetchedAtUtc.ToLocalTime():g}"; }
        catch { return "No usable cache"; }
    }
    private void UpdateSourceHealth()
    {
        SourceHealthText.Text = string.Join(Environment.NewLine,
            FeedCacheService.Sources.Select(source => _sourceHealth.TryGetValue(source, out var text) ? text : source + ": Not checked"));
        var warning = _sourceHealth.Values.Any(v => v.Contains("Unavailable") || v.Contains("unreadable") || v.Contains("could not"));
        FeedsButton.Content = warning ? "⚠ Feeds" : "ⓘ Feeds";
        ToolTip.SetTip(FeedsButton, new TextBlock { Text = SourceHealthText.Text + "\nClick to pin or hide provider details.", TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 420 });
        Avalonia.Automation.AutomationProperties.SetHelpText(FeedsButton, SourceHealthText.Text);
        SourceStateText.Text = warning ? "! FEED ISSUE" : _sourceHealth.Values.Any(v => v.Contains(": OK")) ? "● FEEDS READY" :
            _sourceHealth.Values.Any(v => v.Contains(": Cached")) ? "◷ OFFLINE CACHE" : "○ NOT REFRESHED";
    }

    private bool StartLibraryOperation()
    {
        if (_isLoading || _libraryOperation) { LibraryTransferStatus.Text = "Wait for the current refresh or library operation to finish."; return false; }
        _libraryOperation = true;
        CloseOfferDetails();
        _checksCancellation?.Cancel();
        return true;
    }
    private async void ExportLibrary_Click(object? sender, RoutedEventArgs e)
    {
        if (!StartLibraryOperation()) return;
        try
        {
            var bytes = new LibraryTransferService(_library).Export();
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export version 2 collection and named themes", SuggestedFileName = "LootDrop-v2-library.json",
                FileTypeChoices = new[] { new FilePickerFileType("LootDrop library JSON") { Patterns = new[] { "*.json" } } }
            });
            if (file == null) return;
            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0); await stream.WriteAsync(bytes);
            LibraryTransferStatus.Text = "Export saved. It contains collection information and links; share it only deliberately.";
        }
        catch (Exception ex) { LibraryTransferStatus.Text = "Export failed: " + ex.Message; }
        finally { _libraryOperation = false; }
    }
    private async void ImportLibrary_Click(object? sender, RoutedEventArgs e)
    {
        if (!StartLibraryOperation()) return;
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Review a LootDrop version 2 export", AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("LootDrop library JSON") { Patterns = new[] { "*.json" } } }
            });
            if (files.Count == 0) return;
            await using var input = await files[0].OpenReadAsync();
            var bytes = await SafeHttp.ReadLimitedAsync(input, LibraryTransferService.MaxBytes, CancellationToken.None);
            var transfer = new LibraryTransferService(_library);
            await ReviewImportAsync(transfer, transfer.PreviewExport(bytes));
        }
        catch (Exception ex) { LibraryTransferStatus.Text = "Import was not applied: " + ex.Message; }
        finally { _libraryOperation = false; }
    }
    private async void RestoreBackup_Click(object? sender, RoutedEventArgs e)
    {
        if (!StartLibraryOperation()) return;
        try
        {
            var transfer = new LibraryTransferService(_library);
            var backups = transfer.Backups();
            if (backups.Count == 0) { LibraryTransferStatus.Text = "No automatic snapshots yet. The first change to an existing library creates today's snapshot."; return; }
            var dialog = new Window { Title = "Choose a local backup", Width = 620, Height = 270, CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, RequestedThemeVariant = RequestedThemeVariant };
            var choices = new ComboBox { ItemsSource = backups.Select(b => $"{b.CreatedUtc.ToLocalTime():g} · {b.Size / 1024.0:F1} KiB · {b.Name}").ToArray(), SelectedIndex = 0,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
            var review = new Button { Content = "Review selected backup" };
            var cancel = new Button { Content = "Cancel", IsCancel = true };
            review.Click += (_, _) => dialog.Close(choices.SelectedIndex);
            cancel.Click += (_, _) => dialog.Close(-1);
            dialog.Content = new StackPanel { Margin = new Thickness(20), Spacing = 16, Children =
            { new TextBlock { Text = "Choose a snapshot. Nothing is replaced until you approve the next preview.", TextWrapping = Avalonia.Media.TextWrapping.Wrap }, choices,
                new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 12, Children = { cancel, review } } } };
            // Nullable result: closing with X must never select backup index zero.
            var selected = await dialog.ShowDialog<int?>(this);
            if (selected is int index && index >= 0 && index < backups.Count)
                await ReviewImportAsync(transfer, transfer.PreviewBackup(backups[index].Name));
        }
        catch (Exception ex) { LibraryTransferStatus.Text = "Restore was not applied: " + ex.Message; }
        finally { _libraryOperation = false; }
    }
    private async Task ReviewImportAsync(LibraryTransferService transfer, LibraryImportPreview preview)
    {
        var dialog = new Window { Title = "Review library replacement", Width = 600, Height = 500, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, RequestedThemeVariant = RequestedThemeVariant };
        var apply = new Button { Content = "Replace collection and named themes" };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        apply.Click += (_, _) => dialog.Close(true); cancel.Click += (_, _) => dialog.Close(false);
        dialog.Content = new StackPanel { Margin = new Thickness(22), Spacing = 18, Children =
        { new ScrollViewer { MaxHeight = 350, Content = new TextBlock { Text = preview.Summary, TextWrapping = Avalonia.Media.TextWrapping.Wrap } },
            new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 12, Children = { cancel, apply } } } };
        if (!await dialog.ShowDialog<bool>(this)) { LibraryTransferStatus.Text = "Cancelled. Your library was not replaced."; return; }
        var state = transfer.Apply(preview);
        _saved.Clear();
        foreach (var game in state.Games.Where(g => g.IsWishlisted || g.IsClaimed || g.IsReportedNotFree)) _saved[OfferIdentity.Key(game)] = game;
        customThemes.Clear(); customThemes.AddRange(state.CustomThemes); RefreshSavedThemeList();
        foreach (var game in AllOffers) { game.IsInLiveFeed = false; game.CoverImage?.Dispose(); }
        AllOffers.Clear(); SeedOfflineOffers(); ApplyFilters();
        LibraryTransferStatus.Text = "Collection and named themes restored. Any previous library was backed up. Refresh before opening any claim link.";
    }
}
