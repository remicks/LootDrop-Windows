using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using LootDrop.Models;
using LootDrop.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace LootDrop.Views;

public partial class MainWindow : Window
{
    private static readonly HttpClient ImageHttp = SafeHttp.CreateClient();
    private readonly GamerPowerService _gameService = new();
    private readonly AdditionalGiveawayService _extraService = new();
    private readonly ClaimLinkService _linkService = new();
    private readonly StoreVerificationService _storeVerifier = new();
    private readonly DispatcherTimer _autoRefresh = new() { Interval = TimeSpan.FromMinutes(20) };
    private bool _isLoading;
    private readonly LocalLibraryService _library;
    private readonly ObservableCollection<GameOffer> AllOffers = new();
    private readonly ObservableCollection<GameOffer> VisibleOffers = new();
    private readonly Dictionary<string, SavedGame> _saved = new(StringComparer.Ordinal);
    private readonly ObservableCollection<OfferGroup> VisibleGroups = new();
    private readonly FeedCacheService _cache;
    private readonly IReadOnlyList<FeedProvider> _providers;
    private readonly Dictionary<string, string> _sourceHealth = new();
    private bool _libraryOperation;
    private CancellationTokenSource? _checksCancellation;
    private readonly HashSet<string> selectedPlatforms = new(StringComparer.OrdinalIgnoreCase);
    private bool rememberFiltersOnLaunch;
    private bool minimizeToTray = true;
    private bool closeToTray;
    private bool startWithWindows;
    private bool allowExit;
    private readonly bool launchHidden = Environment.GetCommandLineArgs()
        .Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase));
    private readonly HashSet<string> selectedStores = new(StringComparer.OrdinalIgnoreCase);
    private bool ctrlHeldForStore;
    private string themeName = "Purple";
    private string customAccentHex = "#A448F4";
    private string customThemeBasePreset = "Purple";
    private readonly List<CustomThemeRecord> customThemes = new();
    private string backgroundImagePath = "";
    private bool updatingColorControls;
    private bool sidebarCollapsed;
    private bool sidebarRight;
    private int windowOpacityPercent = 100;
    private int wallpaperDimPercent = 72;
    private bool lowGraphicsMode;
    private bool forceSoftwareRendering;
    private bool updatingSettingsControls;
    private readonly Dictionary<Button, (string Icon, string Label)> sidebarLabels = new();
    private string currentSection = "discover";
    private bool _isReady;

    public MainWindow() : this(new LocalLibraryService(), enableBackgroundWork: true) { }

    // An isolated store and disabled background work allow safe headless UI validation.
    public MainWindow(LocalLibraryService library, bool enableBackgroundWork, IReadOnlyList<FeedProvider>? providers = null, UpdateService? updateService = null)
    {
        _library = library;
        _cache = new FeedCacheService(library.DataDirectory);
        _providers = providers ?? new FeedProvider[]
        {
            new("GamerPower", t => _gameService.GetActiveFreeGamesAsync(t)),
            new("Epic Games", t => _extraService.GetEpicGiveawaysAsync(t)),
            new("CheapShark", t => _extraService.GetCheapSharkFreeDealsAsync(t))
        };
        InitializeComponent();
        InitializeOfferInterface();
        _updateService = updateService ?? new UpdateService(); InitializeUpdates();
        OffersList.ItemsSource = VisibleGroups;
        sidebarLabels[NavDiscover] = ("⌕", "Discover Games");
        sidebarLabels[NavWishlist] = ("♡", "My Wishlist");
        sidebarLabels[NavClaimed] = ("▣", "Claimed Backlog");
        sidebarLabels[NavAll] = ("▤", "All Games");
        sidebarLabels[NavValuable] = ("★", "Highest Value");
        sidebarLabels[NavRecent] = ("◉", "Recently Added");
        sidebarLabels[NavSettings] = ("⚙", "Settings");
        sidebarLabels[NavThemes] = ("◈", "Themes");
        sidebarLabels[NavStoreSignIn] = ("↗", "Store sign-in");
        sidebarLabels[NavHelp] = ("?", "Help");
        SidebarCollapsedCheck.PropertyChanged += (_, a) =>
        {
            if (a.Property == Avalonia.Controls.Primitives.ToggleButton.IsCheckedProperty) LayoutPreferencesChanged();
        };
        SidebarPositionBox.SelectionChanged += (_, _) => LayoutPreferencesChanged();
        WindowOpacitySlider.PropertyChanged += (_, a) =>
        {
            if (a.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty) AppearancePreferencesChanged();
        };
        WallpaperDimSlider.PropertyChanged += (_, a) =>
        {
            if (a.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty) AppearancePreferencesChanged();
        };
        LowGraphicsCheck.PropertyChanged += (_, a) =>
        {
            if (a.Property == Avalonia.Controls.Primitives.ToggleButton.IsCheckedProperty) AppearancePreferencesChanged();
        };
        RendererBox.SelectionChanged += (_, _) => AppearancePreferencesChanged();
        RedSlider.PropertyChanged += SliderColorChanged;
        GreenSlider.PropertyChanged += SliderColorChanged;
        BlueSlider.PropertyChanged += SliderColorChanged;
        // Capture modifier keys during tunnel phase before Button consumes the mouse event.
        AddHandler(InputElement.PointerPressedEvent,
            (_, e) => ctrlHeldForStore = (e.KeyModifiers & KeyModifiers.Control) != 0,
            RoutingStrategies.Tunnel, handledEventsToo: true);
        RememberFiltersCheck.PropertyChanged += (_, args) =>
        {
            if (args.Property == Avalonia.Controls.Primitives.ToggleButton.IsCheckedProperty)
                RememberFilters_Changed();
        };
        MinimizeToTrayCheck.PropertyChanged += (_, args) =>
        {
            if (args.Property == Avalonia.Controls.Primitives.ToggleButton.IsCheckedProperty)
                TrayPreferences_Changed();
        };
        CloseToTrayCheck.PropertyChanged += (_, args) =>
        {
            if (args.Property == Avalonia.Controls.Primitives.ToggleButton.IsCheckedProperty)
                TrayPreferences_Changed();
        };
        StartWithWindowsCheck.PropertyChanged += (_, args) =>
        {
            if (args.Property == Avalonia.Controls.Primitives.ToggleButton.IsCheckedProperty)
                TrayPreferences_Changed();
        };
        PropertyChanged += (_, args) =>
        {
            if (args.Property == WindowStateProperty && WindowState == WindowState.Minimized && minimizeToTray)
                HideToTray();
        };
        Closing += (_, args) =>
        {
            if (!allowExit && closeToTray)
            {
                args.Cancel = true;
                HideToTray();
            }
        };
        Opened += (_, _) =>
        {
            if (launchHidden)
                Dispatcher.UIThread.Post(HideToTray, DispatcherPriority.Loaded);
        };
        try
        {
            var state = _library.Load();
            foreach (var game in state.Games.Where(g => g.Id > 0 && (g.IsWishlisted || g.IsClaimed || g.IsReportedNotFree)))
                _saved[OfferIdentity.Key(game)] = game;
            themeName = state.ThemeName; preferredIcon = state.AppIconKey;
            customAccentHex = state.CustomAccentHex;
            customThemeBasePreset = ThemeCatalog.Resolve(state.CustomThemeBasePreset).Name;
            if (state.CustomThemes != null)
                customThemes.AddRange(state.CustomThemes.Where(t => !string.IsNullOrWhiteSpace(t.Name) && TryHex(t.Hex, out _)));
            backgroundImagePath = state.ThemeBackgroundPath ?? "";
            sidebarCollapsed = state.SidebarCollapsed;
            sidebarRight = state.SidebarRight;
            windowOpacityPercent = Math.Clamp(state.WindowOpacityPercent, 65, 100);
            wallpaperDimPercent = Math.Clamp(state.WallpaperDimPercent, 0, 95);
            lowGraphicsMode = state.LowGraphicsMode;
            forceSoftwareRendering = state.ForceSoftwareRendering;
            rememberFiltersOnLaunch = state.RememberFiltersOnLaunch;
            RememberFiltersCheck.IsChecked = rememberFiltersOnLaunch;
            minimizeToTray = state.MinimizeToTray;
            closeToTray = state.CloseToTray;
            startWithWindows = enableBackgroundWork && WindowsStartupService.IsEnabled();
            MinimizeToTrayCheck.IsChecked = minimizeToTray;
            CloseToTrayCheck.IsChecked = closeToTray;
            StartWithWindowsCheck.IsChecked = startWithWindows;

            if (rememberFiltersOnLaunch)
            {
                var platforms = state.SelectedPlatforms is { Count: > 0 }
                    ? state.SelectedPlatforms
                    : state.SelectedPlatform == "All" ? new List<string>() : new List<string> { state.SelectedPlatform };
                foreach (var platform in platforms.Where(IsKnownPlatform)) selectedPlatforms.Add(platform);
                var stored = state.SelectedStores is { Count: > 0 }
                    ? state.SelectedStores
                    : IsKnownStore(state.SelectedStore) && state.SelectedStore != "All"
                        ? new List<string> { state.SelectedStore } : new List<string>();
                foreach (var store in stored.Where(s => IsKnownStore(s) && s != "All")) selectedStores.Add(store);
            }
            // Without an explicit opt-in, start with All Platforms and All Stores.
            SortBox.SelectedIndex = rememberFiltersOnLaunch ? Math.Clamp(state.SortIndex, 0, 3) : 0;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Saved library could not be read: {ex.Message}";
        }
        InitializeIconChoices();
        _isReady = true;
        ApplyTheme(themeName, customAccentHex, save: false);
        RefreshSavedThemeList();
        updatingSettingsControls = true;
        SidebarCollapsedCheck.IsChecked = sidebarCollapsed;
        SidebarPositionBox.SelectedIndex = sidebarRight ? 1 : 0;
        WindowOpacitySlider.Value = windowOpacityPercent;
        WallpaperDimSlider.Value = wallpaperDimPercent;
        LowGraphicsCheck.IsChecked = lowGraphicsMode;
        RendererBox.SelectedIndex = forceSoftwareRendering ? 1 : 0;
        updatingSettingsControls = false;
        ApplySidebarLayout();
        ApplyAppearancePreferences();
        ApplyBackgroundImage();
        UpdateSelectedButtons();
        UpdateStats();
        SeedOfflineOffers();
        ApplyFilters();
        if (!enableBackgroundWork) StatusText.Text = "Offline validation · Synthetic local records only";
        if (enableBackgroundWork)
        {
            _ = LoadGiveawaysAsync();
            _autoRefresh.Tick += async (_, _) =>
            {
                if (!_isLoading) await LoadGiveawaysAsync();
            };
            _autoRefresh.Start();
        }
        Closed += (_, _) => { _autoRefresh.Stop(); _checksCancellation?.Cancel(); };
    }

    private void SaveLibrary()
    {
        try
        {
            _library.Save(new LibraryState
            {
                Games = _saved.Values.OrderBy(g => g.Id).ToList(),
                SidebarCollapsed = sidebarCollapsed,
                SidebarRight = sidebarRight,
                WindowOpacityPercent = windowOpacityPercent,
                WallpaperDimPercent = wallpaperDimPercent,
                LowGraphicsMode = lowGraphicsMode,
                ForceSoftwareRendering = forceSoftwareRendering,
                SelectedPlatform = selectedPlatforms.Count == 1 ? selectedPlatforms.First() : "All",
                SelectedPlatforms = selectedPlatforms.OrderBy(x => x).ToList(),
                RememberFiltersOnLaunch = rememberFiltersOnLaunch,
                MinimizeToTray = minimizeToTray,
                CloseToTray = closeToTray,
                StartWithWindows = startWithWindows,
                SelectedStore = selectedStores.Count == 1 ? selectedStores.First() : "All",
                SelectedStores = selectedStores.OrderBy(x => x).ToList(),
                ThemeName = themeName, AppIconKey = preferredIcon,
                CustomAccentHex = customAccentHex,
                CustomThemeBasePreset = customThemeBasePreset,
                CustomThemes = customThemes.ToList(),
                ThemeBackgroundPath = backgroundImagePath,
                SortIndex = Math.Max(0, SortBox.SelectedIndex)
            });
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not save library: {ex.Message}";
        }
    }

    private void Remember(GameOffer game)
    {
        if (game.IsWishlisted || game.IsClaimed || game.IsReportedNotFree)
            _saved[OfferIdentity.Key(game)] = SavedGame.FromOffer(game);
        else
            _saved.Remove(OfferIdentity.Key(game));
        SaveLibrary();
        UpdateStats();
        ApplyFilters();
    }

    // Each provider runs with its own deadline. Failure is reported visibly instead
    // of making the entire discovery page wait or silently look empty.
    private static async Task<(string source, IReadOnlyList<GameOffer> games, string error)> FetchSafely(
        Func<CancellationToken, Task<IReadOnlyList<GameOffer>>> fetch, string source, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try { return (source, await fetch(deadline.Token), ""); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (OperationCanceledException)
        {
            return (source, Array.Empty<GameOffer>(), "timed out");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{source} feed unavailable: {ex}");
            return (source, Array.Empty<GameOffer>(), ex.GetType().Name);
        }
    }

    private void UpdateSelectedButtons()
    {
        Button[] nav = { NavDiscover, NavWishlist, NavClaimed, NavAll, NavValuable, NavRecent, NavSettings, NavThemes, NavStoreSignIn, NavHelp };
        Button[] platforms = { PlatformAll, PlatformPC, PlatformAndroid, PlatformPlayStation, PlatformXbox, PlatformIos };
        Button[] stores = { StoreAll, StoreEpic, StoreSteam, StoreDrmFree, StoreGog, StoreItch, StoreUbisoft, StoreCheapShark };
        foreach (var button in nav)
        {
            var active = (button.Tag as string) == currentSection;
            button.Classes.Set("selected", active);
            if (active) button.Background = Resources["AccentBrush"] as IBrush;
            else button.ClearValue(Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty);
        }
        foreach (var button in platforms)
        {
            var platform = button.Tag as string;
            var active = platform == "All" ? selectedPlatforms.Count == 0 :
                platform != null && selectedPlatforms.Contains(platform);
            button.Classes.Set("selected", active);
            if (active) button.Background = Resources["AccentBrush"] as IBrush;
            else button.ClearValue(Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty);
        }
        foreach (var button in stores)
        {
            var store = button.Tag as string;
            var active = store == "All" ? selectedStores.Count == 0 :
                store != null && selectedStores.Contains(store);
            button.Classes.Set("selected", active);
            if (active) button.Background = Resources["AccentBrush"] as IBrush;
            else button.ClearValue(Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty);
        }
    }

    private async Task LoadGiveawaysAsync()
    {
        if (_isLoading || _libraryOperation) return;
        CloseOfferDetails();
        _isLoading = true;
        _checksCancellation?.Cancel();
        _checksCancellation?.Dispose();
        _checksCancellation = new CancellationTokenSource();
        var token = _checksCancellation.Token;
        RefreshButton.IsEnabled = false;
        StatusText.Text = "Connecting to giveaway feeds…";

        try
        {
            // Preserve saved titles while replacing the previous live feed.
            foreach (var previous in AllOffers) { previous.IsInLiveFeed = false; previous.CoverImage?.Dispose(); }
            AllOffers.Clear();
            SeedOfflineOffers();
            ApplyFilters();

            var tasks = _providers.Select(p => FetchSafely(p.Fetch, p.Name, token)).ToList();
            var errors = new List<string>();
            var finished = 0;
            var received = 0;
            var liveGames = new List<GameOffer>();
            while (tasks.Count > 0)
            {
                var completed = await Task.WhenAny(tasks);
                tasks.Remove(completed);
                var (source, offers, error) = await completed;
                if (token.IsCancellationRequested) return;
                finished++;
                if (error.Length != 0) errors.Add($"{source}: {error}");
                if (error.Length == 0)
                {
                    var validOffers = offers.Where(g => g.SourceName == source).Take(500).ToArray();
                    foreach (var offer in validOffers) offer.LastFetchedUtc = DateTime.UtcNow;
                    var cacheStatus = "";
                    try { _cache.Save(source, validOffers); }
                    catch { cacheStatus = " · Cache could not be saved"; }
                    _sourceHealth[source] = $"{source}: OK · {validOffers.Length} offers · Last success {DateTime.Now:g}" + cacheStatus;
                    offers = validOffers;
                    // A successful empty feed retires its old cached listings immediately.
                    foreach (var oldCached in AllOffers.Where(g => g.SourceName == source && g.IsCached).ToArray())
                    {
                        AllOffers.Remove(oldCached);
                        if (_saved.TryGetValue(OfferIdentity.Key(oldCached), out var remembered)) AllOffers.Add(remembered.ToOffer());
                    }
                }
                else
                {
                    _sourceHealth[source] = $"{source}: Unavailable ({error}) · " + CachedAge(source);
                }
                UpdateSourceHealth();

                foreach (var game in offers)
                {
                    if (liveGames.Any(existing => OfferIdentity.Key(existing) == OfferIdentity.Key(game))) continue;
                    liveGames.Add(game);
                    if (!_saved.ContainsKey(OfferIdentity.Key(game)) && game.ProviderId.Length != 0 && offers.Count(g => g.Id == game.Id) == 1 &&
                        _saved.Remove(OfferIdentity.LegacyKey(game.SourceName, game.Id), out var legacy))
                        _saved[OfferIdentity.Key(game)] = legacy;
                    if (_saved.TryGetValue(OfferIdentity.Key(game), out var saved))
                    {
                        game.IsWishlisted = saved.IsWishlisted;
                        game.IsClaimed = saved.IsClaimed;
                        game.IsReportedNotFree = saved.IsReportedNotFree;
                        _saved[OfferIdentity.Key(game)] = SavedGame.FromOffer(game);
                    }
                    var old = AllOffers.FirstOrDefault(existing => OfferIdentity.Key(existing) == OfferIdentity.Key(game) || (game.ProviderId.Length != 0 && existing.ProviderId.Length == 0 && existing.SourceName == game.SourceName && existing.Id == game.Id && offers.Count(g => g.Id == game.Id) == 1));
                    if (old != null) AllOffers.Remove(old);
                    AllOffers.Add(game);
                    received++;
                }

                // Paint partial results immediately; never wait for the slowest API.
                ApplyFilters();
                StatusText.Text = $"{finished}/{_providers.Count} feeds checked · {received} offers loaded · " +
                    (errors.Count > 0 ? string.Join("; ", errors) : $"Latest: {source}");
            }

            SaveLibrary();
            if (received == 0)
            {
                StatusText.Text = errors.Count > 0
                    ? "No live offers received. Cached offers are unverified and cannot be opened. " + string.Join("; ", errors) + ". Saved games remain available."
                    : "All feeds returned zero active giveaways. Try Refresh later.";
                return;
            }

            StatusText.Text = $"{received} offers loaded from {_providers.Count} feeds" +
                (errors.Count > 0 ? " · Unavailable: " + string.Join("; ", errors) : "") +
                $" · {VisibleOffers.Count} match your current filters";
            _ = VerifyGiveawaysAsync(liveGames, token);
            if (!lowGraphicsMode) _ = LoadCoversAsync(liveGames, token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ApplyFilters();
            StatusText.Text = $"Could not load giveaways: {ex.GetType().Name} · {ex.Message}";
            Debug.WriteLine(ex);
        }
        finally
        {
            _isLoading = false;
            RefreshButton.IsEnabled = true;
            UpdateStats();
        }
    }

    private async Task VerifyGiveawaysAsync(IReadOnlyList<GameOffer> games, CancellationToken token)
    {
        // Limit simultaneous checks to avoid burdening storefronts.
        using var gate = new SemaphoreSlim(3);
        int broken = 0, notFree = 0, verified = 0, checkedCount = 0;
        try
        {
            var checks = games.Select(async game =>
            {
                await gate.WaitAsync(token);
                try
                {
                    if (token.IsCancellationRequested) return;
                    if (game.IsExpired)
                    {
                        game.AvailabilityNote = "Expired · Claiming disabled";
                        game.ClaimLinkBroken = true;
                        game.Verification = VerificationResult.Expired;
                        game.PriceEvidence = "Giveaway expiration has passed";
                    }
                    else
                    {
                        var (isBroken, note) = await _linkService.CheckAsync(game.ClaimUrl, token);
                        game.LastAddressCheckUtc = DateTime.UtcNow;
                        if (token.IsCancellationRequested) return;
                        game.AvailabilityNote = note;
                        game.ClaimLinkBroken = isBroken;
                        if (isBroken)
                        {
                            game.Verification = VerificationResult.BrokenLink;
                            game.PriceEvidence = "Unsafe or unsupported claim URL";
                        }
                        else
                        {
                            var (result, detail) = game.SourceName == "CheapShark"
                                ? (VerificationResult.Unverified, "CheapShark reports $0; final store checkout not verified")
                                : await _storeVerifier.VerifyAsync(game, token);
                            if (token.IsCancellationRequested) return;
                            game.Verification = result;
                            game.PriceEvidence = detail;
                            if (result == VerificationResult.VerifiedFree) Interlocked.Increment(ref verified);
                            if (result is VerificationResult.NotFree or VerificationResult.NotGiveaway)
                                Interlocked.Increment(ref notFree);
                        }
                    }
                    game.LinkChecked = true;
                    if (game.ClaimLinkBroken) Interlocked.Increment(ref broken);
                    Interlocked.Increment(ref checkedCount);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    game.Verification = VerificationResult.Unverified;
                    game.PriceEvidence = "Verification failed · Check checkout";
                    Debug.WriteLine($"Verification problem: {ex}");
                }
                finally { gate.Release(); }
            });
            await Task.WhenAll(checks);
            if (token.IsCancellationRequested) return;
            ApplyFilters();
            StatusText.Text = $"{games.Count} {(games.Count == 1 ? "offer" : "offers")} checked · Updated {DateTime.Now:t}";
            ToolTip.SetTip(StatusText, $"{verified} store-confirmed free · {notFree} no longer free/not giveaways · {broken} blocked addresses · {checkedCount} checked. Auto-refresh every 20 minutes. Store checkout still requires your confirmation.");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Debug.WriteLine($"Validation failed: {ex}"); }
    }

    private static async Task LoadCoversAsync(IEnumerable<GameOffer> games, CancellationToken token)
    {
        using var gate = new SemaphoreSlim(6);
        var tasks = games.Select(async game =>
        {
            if (!UrlSafety.TryImage(game.ImageUrl, out var uri)) return;
            await gate.WaitAsync(token);
            try
            {
                var bytes = await SafeHttp.GetBytesAsync(ImageHttp, uri.AbsoluteUri, SafeImage.MaxBytes, token);
                if (!token.IsCancellationRequested) game.CoverImage = SafeImage.Decode(bytes, 480);
            }
            catch (Exception ex) { Debug.WriteLine($"Cover unavailable: {ex.Message}"); }
            finally { gate.Release(); }
        });
        try { await Task.WhenAll(tasks); }
        catch (OperationCanceledException) { }
    }

    private void ApplyFilters()
    {
        if (!_isReady) return;
        var search = SearchBox.Text?.Trim() ?? "";
        var results = AllOffers.Where(game =>
            (currentSection == "wishlist" ? game.IsWishlisted :
             currentSection == "claimed" ? game.IsClaimed :
             currentSection == "all" ? true :
             (game.IsInLiveFeed || game.IsCached) && !game.IsExpired && !game.ClaimLinkBroken && !game.IsReportedNotFree && !game.IsVerificationRejected) &&
            (selectedPlatforms.Count == 0 || selectedPlatforms.Any(platform => PlatformMatches(game.Platforms, platform))) &&
            (selectedStores.Count == 0 || selectedStores.Any(s => StoreMatches(game, s))) &&
            (search.Length == 0 || game.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             game.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             game.Platforms.Contains(search, StringComparison.OrdinalIgnoreCase)));

        results = SortBox.SelectedIndex switch
        {
            1 => results.OrderByDescending(g => g.OriginalValue),
            2 => results.OrderBy(g => g.ExpiresAtUtc ?? DateTime.MaxValue),
            3 => results.OrderBy(g => g.Title),
            _ => results.OrderByDescending(g => g.PublishedAtUtc)
        };
        VisibleOffers.Clear();
        foreach (var game in results) VisibleOffers.Add(game);
        VisibleGroups.Clear();
        foreach (var group in OfferIdentity.Group(VisibleOffers)) VisibleGroups.Add(group);
        EmptyMessage.IsVisible = VisibleOffers.Count == 0;
        UpdateSelectedButtons();
        var platformSummary = selectedPlatforms.Count == 0 ? "All" : string.Join(", ", selectedPlatforms.OrderBy(x => x));
        ActiveFiltersText.Text = $"{VisibleGroups.Count} {(VisibleGroups.Count == 1 ? "title" : "titles")} · {VisibleOffers.Count} {(VisibleOffers.Count == 1 ? "offer" : "offers")}";
        ToolTip.SetTip(ActiveFiltersText, $"Viewing: {currentSection} · Platforms: {platformSummary} · Stores: {(selectedStores.Count == 0 ? "All" : string.Join(", ", selectedStores.OrderBy(s => s)))}");
        var eligible = AllOffers.Where(g => g.IsInLiveFeed && !g.IsExpired && !g.IsReportedNotFree && !g.ClaimLinkBroken && !g.IsVerificationRejected).ToList();
        FilterSummary.Text = $"Live platform counts: PC {eligible.Count(g => PlatformMatches(g.Platforms, "PC"))}  ·  Android {eligible.Count(g => PlatformMatches(g.Platforms, "Android"))}  ·  PlayStation {eligible.Count(g => PlatformMatches(g.Platforms, "PlayStation"))}  ·  Xbox {eligible.Count(g => PlatformMatches(g.Platforms, "Xbox"))}  ·  iOS {eligible.Count(g => PlatformMatches(g.Platforms, "iOS"))}  ·  GOG {eligible.Count(g => StoreMatches(g, "GOG"))}";
        ToolTip.SetTip(PlatformAll, FilterSummary.Text + "\nSelect one or more platform buttons; All clears this category.");
        EmptyMessage.Text = currentSection switch
        {
            "wishlist" => "Your wishlist is empty or does not match your filters.",
            "claimed" => "No claimed games yet, or none match your filters.",
            "all" => "No games have been loaded or saved yet.",
            _ => AllOffers.Any(g => g.IsInLiveFeed)
                ? "No games match the selected filters. Choose All Platforms and All Stores to see everything."
                : "No live giveaways have loaded. Check the feed status above, or try Refresh."
        };
        UpdateStats();
    }

    private static bool PlatformMatches(string platforms, string selected)
    {
        if (selected == "All") return true;
        var tokens = platforms.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return selected switch
        {
            "PC" => tokens.Any(p => p.Equals("PC", StringComparison.OrdinalIgnoreCase)),
            "PlayStation" => tokens.Any(p => p.Contains("PlayStation", StringComparison.OrdinalIgnoreCase) || p.Equals("PS4", StringComparison.OrdinalIgnoreCase) || p.Equals("PS5", StringComparison.OrdinalIgnoreCase)),
            "Xbox" => tokens.Any(p => p.Contains("Xbox", StringComparison.OrdinalIgnoreCase)),
            "Android" => tokens.Any(p => p.Equals("Android", StringComparison.OrdinalIgnoreCase)),
            "iOS" => tokens.Any(p => p.Equals("iOS", StringComparison.OrdinalIgnoreCase)),
            _ => tokens.Any(p => p.Contains(selected, StringComparison.OrdinalIgnoreCase))
        };
    }

    private static bool StoreMatches(GameOffer game, string selected)
    {
        if (selected == "All") return true;

        if (selected == "Epic Games")
            return game.StoreName.Equals("Epic Games", StringComparison.OrdinalIgnoreCase);

        if (selected == "GOG")
            return game.StoreName.Equals("GOG", StringComparison.OrdinalIgnoreCase);

        if (selected == "CheapShark")
            return game.SourceName.Equals("CheapShark", StringComparison.OrdinalIgnoreCase);

        if (selected == "DRM-Free")
            return game.StoreName.Equals("DRM-Free", StringComparison.OrdinalIgnoreCase) ||
                   game.Platforms.Contains("DRM-Free", StringComparison.OrdinalIgnoreCase);

        return game.StoreName.Equals(selected, StringComparison.OrdinalIgnoreCase) ||
               game.Platforms.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                   .Any(p => p.Contains(selected, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateStats()
    {
        AvailableCount.Text = AllOffers.Count(g => g.IsInLiveFeed && !g.IsExpired && !g.ClaimLinkBroken && !g.IsReportedNotFree).ToString(CultureInfo.CurrentCulture);
        WishlistCount.Text = _saved.Values.Count(g => g.IsWishlisted).ToString(CultureInfo.CurrentCulture);
        // Only one advertised value per distinct game title, even across API sources.
        // Keep the largest available advertised retail price, not a sum of duplicate offers.
        var uniqueSavings = AllOffers
            .Where(g => g.IsInLiveFeed && !g.IsExpired && !g.ClaimLinkBroken &&
                        !g.IsReportedNotFree && !g.IsVerificationRejected && g.OriginalValue > 0)
            .GroupBy(g => SavingsTitleKey(g.Title))
            .Sum(group => group.Max(g => g.OriginalValue));
        SavingsCount.Text = uniqueSavings.ToString("C2", CultureInfo.CurrentCulture);
    }

    private static string SavingsTitleKey(string title)
    {
        // Strip only a trailing storefront/key tag, never edition/DLC identifiers.
        var cleaned = Regex.Replace(title.Trim(),
            @"\s*(?:\((?:Steam|Epic Games|GOG)(?: Key)?\)|\[(?:Steam|Epic Games|GOG)(?: Key)?\]|[-–]\s*(?:Steam|Epic Games|GOG)\s+Key)\s*$",
            "", RegexOptions.IgnoreCase);
        var normalized = Regex.Replace(cleaned.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();
        return normalized.Length == 0 ? title.ToLowerInvariant() : normalized;
    }

    private static bool IsKnownPlatform(string platform) =>
        platform is "PC" or "Android" or "PlayStation" or "Xbox" or "iOS";

    private static bool IsKnownStore(string store) =>
        store is "All" or "Epic Games" or "Steam" or "DRM-Free" or "GOG" or "Itch.io" or "Ubisoft" or "CheapShark";

    private void Platform_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string platform }) return;
        if (platform == "All") selectedPlatforms.Clear();
        else if (IsKnownPlatform(platform))
        {
            if (!selectedPlatforms.Add(platform)) selectedPlatforms.Remove(platform);
        }
        SaveLibrary();
        ApplyFilters();
    }
    private void Store_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string store }) return;
        if (store == "All") selectedStores.Clear();
        else if (IsKnownStore(store))
        {
            if (!ctrlHeldForStore)
            {
                selectedStores.Clear();
                selectedStores.Add(store);
            }
            else if (!selectedStores.Add(store)) selectedStores.Remove(store);
        }
        ctrlHeldForStore = false;
        SaveLibrary();
        ApplyFilters();
    }
    private void RememberFilters_Changed()
    {
        if (!_isReady) return;
        rememberFiltersOnLaunch = RememberFiltersCheck.IsChecked == true;
        SaveLibrary();
        ApplyFilters();
    }

    private void TrayPreferences_Changed()
    {
        if (!_isReady) return;
        var newMinimizeToTray = MinimizeToTrayCheck.IsChecked == true;
        var newCloseToTray = CloseToTrayCheck.IsChecked == true;
        var newStartWithWindows = StartWithWindowsCheck.IsChecked == true;

        if (startWithWindows != newStartWithWindows)
        {
            try
            {
                WindowsStartupService.SetEnabled(newStartWithWindows);
                startWithWindows = newStartWithWindows;
                StartupStatusText.Text = startWithWindows
                    ? "Enabled: LootDrop will launch hidden in the tray when you sign in."
                    : "Disabled: LootDrop will not start automatically with Windows.";
            }
            catch (Exception ex)
            {
                StartupStatusText.Text = $"Windows startup setting could not be changed: {ex.Message}";
                StartWithWindowsCheck.IsChecked = startWithWindows;
            }
        }
        minimizeToTray = newMinimizeToTray;
        closeToTray = newCloseToTray;
        SaveLibrary();
    }

    private void HideToTray()
    {
        ShowInTaskbar = false;
        Hide();
    }

    public void RestoreFromTray()
    {
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Show();
        Activate();
    }

    public void OpenSettingsFromTray()
    {
        RestoreFromTray();
        NavigateTo("settings");
    }

    public void AllowExit() => allowExit = true;

    private static readonly IReadOnlyDictionary<string, ThemePalette> ThemePresets = ThemeCatalog.Presets;

    private static bool TryHex(string? value, out Color color)
    {
        color = default;
        if (value is null || !System.Text.RegularExpressions.Regex.IsMatch(value.Trim(), "^#[0-9A-Fa-f]{6}$")) return false;
        color = Color.Parse(value.Trim());
        return true;
    }

    private void ApplyTheme(string name, string customHex, bool save)
    {
        var isCustom = name.Equals("Custom", StringComparison.OrdinalIgnoreCase);
        if (!isCustom && !ThemePresets.ContainsKey(name)) name = "Purple";
        var hex = isCustom ? customHex : ThemePresets[name].Hex;
        if (!TryHex(hex, out var color))
        {
            name = "Purple";
            hex = ThemePresets[name].Hex;
            color = Color.Parse(hex);
        }
        themeName = name;
        customAccentHex = customHex;
        if (!isCustom) customThemeBasePreset = name;
        var palette = ThemeCatalog.Resolve(isCustom ? customThemeBasePreset : name) with { Hex = hex };
        RequestedThemeVariant = palette.IsLight ? Avalonia.Styling.ThemeVariant.Light : Avalonia.Styling.ThemeVariant.Dark;
        foreach (var brush in ThemeCatalog.Brushes(palette))
            Resources[brush.Key] = new SolidColorBrush(Color.Parse(brush.Value));
        UpdateWallpaperOverlay();

        var iconKey = ApplyResolvedIcon(isCustom ? ClosestIcon(color) : ThemePresets[name].Icon);
        ThemeStatusText.Text = $"Active: {name} · Accent {hex.ToUpperInvariant()} · {iconKey} icon";
        ThemeErrorText.Text = "";
        CustomColorSwatch.Background = new SolidColorBrush(color);
        CustomHexBox.Text = hex.ToUpperInvariant();
        updatingColorControls = true;
        try
        {
            RedBox.Text = color.R.ToString();
            GreenBox.Text = color.G.ToString();
            BlueBox.Text = color.B.ToString();
            RedSlider.Value = color.R;
            GreenSlider.Value = color.G;
            BlueSlider.Value = color.B;
        }
        finally { updatingColorControls = false; }
        UpdateSelectedButtons();
        if (save) SaveLibrary();
    }

    public void ApplySavedIcon()
    {
        var iconKey = themeName.Equals("Custom", StringComparison.OrdinalIgnoreCase) && TryHex(customAccentHex, out var col)
            ? ClosestIcon(col)
            : ThemePresets.TryGetValue(themeName, out var preset) ? preset.Icon : "purple";
        ApplyResolvedIcon(iconKey);
    }

    private static string ClosestIcon(Color chosen)
    {
        var best = "purple";
        var distance = double.MaxValue;
        foreach (var entry in ThemePresets)
        {
            var c = Color.Parse(entry.Value.Hex);
            var d = Math.Pow(chosen.R - c.R, 2) + Math.Pow(chosen.G - c.G, 2) + Math.Pow(chosen.B - c.B, 2);
            if (d < distance) { distance = d; best = entry.Value.Icon; }
        }
        return best;
    }

    private void ThemePreset_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string name }) ApplyTheme(name, customAccentHex, save: true);
    }

    private void ApplyCustomHex_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryHex(CustomHexBox.Text, out _))
        { ThemeErrorText.Text = "Enter a valid six-digit HEX code like #42BFFF."; return; }
        ApplyTheme("Custom", CustomHexBox.Text!.Trim(), save: true);
    }

    private void ApplyCustomRgb_Click(object? sender, RoutedEventArgs e)
    {
        if (!byte.TryParse(RedBox.Text, out var r) || !byte.TryParse(GreenBox.Text, out var g) ||
            !byte.TryParse(BlueBox.Text, out var b))
        { ThemeErrorText.Text = "RGB values must each be whole numbers from 0 to 255."; return; }
        ApplyTheme("Custom", $"#{r:X2}{g:X2}{b:X2}", save: true);
    }

    private void SliderColorChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property != Avalonia.Controls.Primitives.RangeBase.ValueProperty || updatingColorControls || !_isReady)
            return;
        var r = (byte)Math.Clamp((int)Math.Round(RedSlider.Value), 0, 255);
        var g = (byte)Math.Clamp((int)Math.Round(GreenSlider.Value), 0, 255);
        var b = (byte)Math.Clamp((int)Math.Round(BlueSlider.Value), 0, 255);
        ApplyTheme("Custom", $"#{r:X2}{g:X2}{b:X2}", save: false);
    }

    private void RefreshSavedThemeList()
    {
        var current = SavedThemesBox.SelectedItem as string;
        SavedThemesBox.ItemsSource = customThemes.Select(t => t.Name).OrderBy(t => t).ToList();
        if (current != null && customThemes.Any(t => t.Name == current)) SavedThemesBox.SelectedItem = current;
    }

    private void SaveCustomTheme_Click(object? sender, RoutedEventArgs e)
    {
        var name = CustomThemeNameBox.Text?.Trim() ?? "";
        if (name.Length is < 1 or > 45)
        { CustomThemeStatusText.Text = "Give your theme a name between 1 and 45 characters."; return; }
        var hex = themeName == "Custom" ? customAccentHex : ThemePresets[themeName].Hex;
        var record = customThemes.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (record == null) customThemes.Add(new CustomThemeRecord { Name = name, Hex = hex, BackgroundPath = backgroundImagePath, BasePreset = customThemeBasePreset });
        else { record.Hex = hex; record.BackgroundPath = backgroundImagePath; record.BasePreset = customThemeBasePreset; }
        RefreshSavedThemeList();
        SavedThemesBox.SelectedItem = name;
        CustomThemeStatusText.Text = $"Saved {name} ({hex}).";
        SaveLibrary();
    }

    private void LoadSavedTheme_Click(object? sender, RoutedEventArgs e)
    {
        var record = customThemes.FirstOrDefault(t => t.Name == SavedThemesBox.SelectedItem as string);
        if (record == null) { CustomThemeStatusText.Text = "Select a saved theme first."; return; }
        customThemeBasePreset = ThemeCatalog.Resolve(record.BasePreset).Name;
        ApplyTheme("Custom", record.Hex, save: false);
        backgroundImagePath = record.BackgroundPath;
        ApplyBackgroundImage();
        SaveLibrary();
        CustomThemeStatusText.Text = $"Loaded {record.Name} and its background.";
    }

    private void DeleteSavedTheme_Click(object? sender, RoutedEventArgs e)
    {
        var record = customThemes.FirstOrDefault(t => t.Name == SavedThemesBox.SelectedItem as string);
        if (record == null) { CustomThemeStatusText.Text = "Select a saved theme first."; return; }
        customThemes.Remove(record);
        RefreshSavedThemeList();
        CustomThemeStatusText.Text = $"Deleted saved theme {record.Name}.";
        SaveLibrary();
    }

    private void ApplyBackgroundImage()
    {
        if (string.IsNullOrWhiteSpace(backgroundImagePath))
        {
            ThemeBackgroundImage.Source = null;
            ThemeBackgroundImage.IsVisible = false;
            ThemeBackgroundOverlay.IsVisible = false;
            BackgroundStatusText.Text = "No background image selected.";
            return;
        }
        try
        {
            var fullPath = SafeImage.BackgroundPath(backgroundImagePath);
            using var imageFile = File.OpenRead(fullPath);
            if (imageFile.Length > SafeImage.MaxBytes) throw new IOException("Image exceeds 4 MiB.");
            using var imageBytes = new MemoryStream();
            imageFile.CopyTo(imageBytes);
            (ThemeBackgroundImage.Source as IDisposable)?.Dispose();
            ThemeBackgroundImage.Source = SafeImage.Decode(imageBytes.ToArray(), 1920);
            ThemeBackgroundImage.IsVisible = !lowGraphicsMode;
            ThemeBackgroundOverlay.IsVisible = !lowGraphicsMode;
            BackgroundStatusText.Text = lowGraphicsMode ? "Wallpaper disabled by low graphics mode." : $"Using: {Path.GetFileName(backgroundImagePath)}";
            UpdateWallpaperOverlay();
        }
        catch (Exception ex)
        {
            ThemeBackgroundImage.Source = null;
            ThemeBackgroundImage.IsVisible = false;
            ThemeBackgroundOverlay.IsVisible = false;
            BackgroundStatusText.Text = $"Could not load image: {ex.Message}";
        }
    }

    private async void ChooseBackground_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose LootDrop background",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("Image files") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp" } } }
            });
            if (files.Count == 0) return;
            var source = files[0];
            var ext = Path.GetExtension(source.Name).ToLowerInvariant();
            if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp"))
            { BackgroundStatusText.Text = "Choose a PNG, JPG or WebP image."; return; }
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LootDrop", "Backgrounds");
            var destination = SafeImage.BackgroundPath(Path.Combine(folder, $"theme-background-{Guid.NewGuid():N}{ext}"));
            Directory.CreateDirectory(folder);
            await using (var input = await source.OpenReadAsync())
            {
                var bytes = await SafeHttp.ReadLimitedAsync(input, SafeImage.MaxBytes, CancellationToken.None);
                using var validated = SafeImage.Decode(bytes, 1920);
                await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await output.WriteAsync(bytes);
            }
            backgroundImagePath = destination;
            ApplyBackgroundImage();
            SaveLibrary();
        }
        catch (Exception ex) { BackgroundStatusText.Text = $"Image selection failed: {ex.Message}"; }
    }

    private void RemoveBackground_Click(object? sender, RoutedEventArgs e)
    {
        backgroundImagePath = "";
        ApplyBackgroundImage();
        SaveLibrary();
    }

    private void ToggleSidebar_Click(object? sender, RoutedEventArgs e)
    {
        sidebarCollapsed = !sidebarCollapsed;
        updatingSettingsControls = true;
        SidebarCollapsedCheck.IsChecked = sidebarCollapsed;
        updatingSettingsControls = false;
        ApplySidebarLayout();
        SaveLibrary();
    }

    private void LayoutPreferencesChanged()
    {
        if (!_isReady || updatingSettingsControls) return;
        sidebarCollapsed = SidebarCollapsedCheck.IsChecked == true;
        sidebarRight = SidebarPositionBox.SelectedIndex == 1;
        ApplySidebarLayout();
        SaveLibrary();
    }

    private void ApplySidebarLayout()
    {
        const int compactWidth = 76;
        const int expandedWidth = 220;
        var width = sidebarCollapsed ? compactWidth : expandedWidth;
        ShellGrid.ColumnDefinitions = sidebarRight
            ? new ColumnDefinitions($"*,{width}")
            : new ColumnDefinitions($"{width},*");
        Grid.SetColumn(SidebarPanel, sidebarRight ? 1 : 0);
        Grid.SetColumn(MainArea, sidebarRight ? 0 : 1);

        SidebarLogo.IsVisible = !sidebarCollapsed; DeveloperBrand.IsVisible = !sidebarCollapsed;
        SidebarTagline.IsVisible = !sidebarCollapsed;
        SidebarMainHeader.IsVisible = !sidebarCollapsed;
        SidebarCollectionHeader.IsVisible = !sidebarCollapsed;
        SidebarPreferencesHeader.IsVisible = !sidebarCollapsed;
        SidebarStatus.IsVisible = !sidebarCollapsed;

        // Brand and menu use identical horizontal insets so their buttons line up.
        SidebarBrand.Margin = sidebarCollapsed
            ? new Thickness(8, 12, 8, 6)
            : new Thickness(12, 16, 12, 8);
        SidebarNavigation.Margin = sidebarCollapsed
            ? new Thickness(8, 0, 8, 0)
            : new Thickness(12, 0, 12, 0);

        ConfigureSidebarButton(SidebarToggleButton, "☰", "Collapse", sidebarCollapsed);
        ToolTip.SetTip(SidebarToggleButton, sidebarCollapsed ? "Expand sidebar" : "Collapse sidebar");
        foreach (var entry in sidebarLabels)
            ConfigureSidebarButton(entry.Key, entry.Value.Icon, entry.Value.Label, sidebarCollapsed);
    }

    private static void ConfigureSidebarButton(Button button, string icon, string label, bool collapsed)
    {
        button.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = collapsed
            ? Avalonia.Layout.HorizontalAlignment.Center
            : Avalonia.Layout.HorizontalAlignment.Left;
        button.VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center;
        button.Height = 44;
        button.Padding = new Thickness(collapsed ? 3 : 9, 0);
        button.Margin = new Thickness(3, 2);

        var line = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = collapsed ? 0 : 9,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = collapsed
                ? Avalonia.Layout.HorizontalAlignment.Center
                : Avalonia.Layout.HorizontalAlignment.Left
        };
        line.Children.Add(new TextBlock
        {
            Text = icon,
            FontSize = collapsed ? 23 : 19,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Width = collapsed ? 28 : 25,
            TextAlignment = Avalonia.Media.TextAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        });
        if (!collapsed)
        {
            line.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 13,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            });
        }
        button.Content = line;
    }

    private void AppearancePreferencesChanged()
    {
        if (!_isReady || updatingSettingsControls) return;
        windowOpacityPercent = Math.Clamp((int)Math.Round(WindowOpacitySlider.Value), 65, 100);
        wallpaperDimPercent = Math.Clamp((int)Math.Round(WallpaperDimSlider.Value), 0, 95);
        lowGraphicsMode = LowGraphicsCheck.IsChecked == true;
        forceSoftwareRendering = RendererBox.SelectedIndex == 1;
        ApplyAppearancePreferences();
        // Reload the wallpaper only when the low-graphics visibility setting changes.
        ThemeBackgroundImage.IsVisible = !lowGraphicsMode && !string.IsNullOrWhiteSpace(backgroundImagePath) && ThemeBackgroundImage.Source != null;
        ThemeBackgroundOverlay.IsVisible = ThemeBackgroundImage.IsVisible;
        if (lowGraphicsMode)
        {
            foreach (var offer in AllOffers) offer.CoverImage = null;
        }
        SaveLibrary();
    }

    private void ApplyAppearancePreferences()
    {
        Opacity = windowOpacityPercent / 100.0;
        WindowOpacityLabel.Text = $"{windowOpacityPercent}%";
        WallpaperDimLabel.Text = $"{wallpaperDimPercent}% dimmed";
        UpdateWallpaperOverlay();
    }

    private void UpdateWallpaperOverlay()
    {
        var alpha = (byte)Math.Clamp((int)Math.Round(wallpaperDimPercent * 255.0 / 100), 0, 255);
        var baseColor = Color.Parse(ThemeCatalog.Resolve(customThemeBasePreset).Background);
        ThemeBackgroundOverlay.Background = new SolidColorBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));
    }

    private void ClearFilters_Click(object? sender, RoutedEventArgs e)
    {
        selectedPlatforms.Clear();
        selectedStores.Clear();
        SearchBox.Text = "";
        SortBox.SelectedIndex = 0;
        SaveLibrary();
        ApplyFilters();
    }

    private void Search_TextChanged(object? sender, TextChangedEventArgs e) => ApplyFilters();
    private void Sort_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_isReady) { SaveLibrary(); ApplyFilters(); }
    }
    private void Wishlist_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: GameOffer game }) { game.IsWishlisted = !game.IsWishlisted; Remember(game); }
    }
    private void MarkClaimed_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: GameOffer game }) { game.IsClaimed = !game.IsClaimed; Remember(game); }
    }
    private void ReportNotFree_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: GameOffer game })
        {
            game.IsReportedNotFree = !game.IsReportedNotFree;
            game.Verification = game.IsReportedNotFree ? VerificationResult.ReportedNotFree : VerificationResult.Unverified;
            game.PriceEvidence = game.IsReportedNotFree ? "Flagged on this computer" : "Manual flag cleared · Recheck with Refresh";
            Remember(game);
            StatusText.Text = game.IsReportedNotFree
                ? "Flagged locally as no longer free; hidden from Discovery."
                : "Not-free flag removed; verify the price on the store before claiming.";
        }
    }
    private async void Refresh_Click(object? sender, RoutedEventArgs e) => await LoadGiveawaysAsync();

    private void Navigation_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string section }) NavigateTo(section);
    }

    private void NavigateTo(string section)
    {
        CloseOfferDetails();
        currentSection = section;
        var settingsPage = section == "settings";
        var themesPage = section == "themes";
        var helpPage = section == "help"; var signInPage = section == "signin"; StoreSignInScroll.IsVisible = signInPage;
        DiscoveryScroll.IsVisible = !settingsPage && !themesPage && !helpPage && !signInPage;
        SettingsScroll.IsVisible = settingsPage;
        ThemesScroll.IsVisible = themesPage;
        HelpScroll.IsVisible = helpPage;
        SearchBox.IsEnabled = !settingsPage && !themesPage && !helpPage && !signInPage;
        PageTitle.Text = section switch
        {
            "wishlist" => "My Wishlist", "claimed" => "Claimed Backlog", "valuable" => "Highest Value Games",
            "recent" => "Recently Added", "all" => "All Games", _ => "Discover Free Games"
        };
        if (section == "valuable") SortBox.SelectedIndex = 1;
        if (section == "recent") SortBox.SelectedIndex = 0;
        ApplyFilters();
        UpdateSelectedButtons();
    }

    private async void Claim_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: GameOffer game }) return;
        if (!game.ClaimButtonEnabled)
        {
            StatusText.Text = "Claim link disabled: expired, broken, manually flagged, or store shows it is not a giveaway.";
            return;
        }
        if (!UrlSafety.TryHttps(game.ClaimUrl, out var uri))
        { StatusText.Text = "Unsafe or unsupported claim URL blocked."; return; }
        if (!await ConfirmClaimAsync(uri)) return;
        if (!game.ClaimButtonEnabled || !AllOffers.Contains(game)) return;
        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            StatusText.Text = "Check the final store price yourself. Opening a claim page does not mark a game as claimed.";
        }
        catch (Exception ex) { StatusText.Text = $"Could not open claim page: {ex.Message}"; }
    }
    private async Task<bool> ConfirmClaimAsync(Uri uri)
    {
        var dialog = new Window { Title = "Open claim website?", Width = 560, Height = 320,
            CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            RequestedThemeVariant = RequestedThemeVariant };
        var open = new Button { Content = "Open website" };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        open.Click += (_, _) => dialog.Close(true);
        cancel.Click += (_, _) => dialog.Close(false);
        dialog.Content = new StackPanel { Margin = new Thickness(20), Spacing = 14, Children =
        {
            new TextBlock { Text = "Destination: " + uri.IdnHost, FontWeight = FontWeight.Bold },
            new TextBox { Text = uri.AbsoluteUri, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 110 },
            new TextBlock { Text = "Check the address and final price. Redirects may lead to another site. Never share passwords with LootDrop.", TextWrapping = TextWrapping.Wrap },
            new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 12, Children = { cancel, open } }
        }};
        return await dialog.ShowDialog<bool>(this);
    }

    private void CheapShark_Click(object? sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("https://www.cheapshark.com/") { UseShellExecute = true }); }
        catch (Exception ex) { StatusText.Text = $"Could not open CheapShark.com: {ex.Message}"; }
    }

    private void GamerPower_Click(object? sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("https://www.gamerpower.com/") { UseShellExecute = true }); }
        catch (Exception ex) { StatusText.Text = $"Could not open GamerPower.com: {ex.Message}"; }
    }
}





