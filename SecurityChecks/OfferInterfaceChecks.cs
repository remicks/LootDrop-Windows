using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.VisualTree;
using LootDrop.Models;
using LootDrop.Services;
using LootDrop.Views;

internal static class OfferInterfaceChecks
{
    public static void RunCore(Action<bool, string> check)
    {
        var game = new GameOffer { Id = 7, Title = "Offer details fixture", SourceName = "GamerPower", Platforms = "PC",
            ClaimUrl = "https://independent-store.com/offer", Description = "Full test description", LinkChecked = true };
        check(game.PriceStatusGlyph == "?" && !game.HasVerifiedPrice, "Accepted address never implies verified price");
        check(game.LinkTooltip.Contains("NOT verified") && game.PriceTooltip.Contains("does not prove"), "Hover text distinguishes address format, price and destination trust");
        check(game.PriceTimeText.Contains("Not checked") && !game.LastPriceCheckUtc.HasValue, "Missing price timestamp is not invented");
        var stamp = DateTime.UtcNow.AddMinutes(-12);
        var notified = new List<string>(); game.PropertyChanged += (_, e) => notified.Add(e.PropertyName ?? "");
        game.LastPriceCheckUtc = stamp; game.Verification = VerificationResult.VerifiedFree;
        check(game.PriceStatusGlyph == "✓" && game.HasVerifiedPrice, "Live verified price has a distinct positive icon");
        check(game.PriceTimeText.Contains(stamp.ToLocalTime().ToString("g")) && notified.Contains(nameof(GameOffer.PriceTooltip)), "Price time and hover bindings update with real check metadata");
        game.Verification = VerificationResult.NotFree;
        check(game.PriceStatusGlyph == "!" && !game.ClaimButtonEnabled, "Rejected price has a warning icon and cannot open");
        game.IsInLiveFeed = false;
        check(game.CompactState == "Saved" && !game.HasVerifiedPrice && notified.Contains(nameof(GameOffer.ClaimButtonEnabled)), "Live-feed invalidation updates claim bindings immediately");
        var cached = new GameOffer { IsCached = true, IsInLiveFeed = true, Verification = VerificationResult.VerifiedFree,
            LastPriceCheckUtc = stamp, ClaimUrl = "https://independent-store.com/offer" };
        check(cached.CompactState == "Cached" && cached.PriceStatusGlyph == "◷" && !cached.HasVerifiedPrice && !cached.ClaimButtonEnabled, "Cached evidence never gets a live verified icon or enabled claim");
        var blocked = new GameOffer { ClaimUrl = "file:///C:/malicious.exe" };
        check(blocked.LinkStatusGlyph == "⊘" && blocked.DisplayClaimUrl.Contains("blocked") && !blocked.ClaimButtonEnabled, "Unsupported destinations are blocked even in the details presentation");
        var expired = new GameOffer { ClaimUrl = "https://independent-store.com/offer", ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1) };
        check(expired.FreshnessGlyph == "!" && expired.DisabledReason.Contains("expiration"), "Expiry icon and explanation retain the opening restriction");
        game.LastFetchedUtc = stamp;
        var saved = SavedGame.FromOffer(game).ToOffer();
        check(saved.LastFetchedUtc == stamp && !saved.LastPriceCheckUtc.HasValue && !saved.ClaimButtonEnabled, "Saved records retain provider age but never resurrect price verification");
        check(HelpGuide.Search("STATUS").Count > 0 && HelpGuide.Search("unmatchable-xyz-777").Count == 0, "Plain-language help is searchable without network access");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "Help-Guide.md"), HelpGuide.ToMarkdown());
    }

    public static void RunWindow(Action<bool, string> check, string root, bool screenshots)
    {
        var fetchCount = 0;
        var current = new GameOffer { Id = 72, SourceName = "GamerPower", Title = "Readable compact offer", Platforms = "PC",
            ClaimUrl = "https://independent-store.com/game", Description = "A full description that belongs in the details drawer, rather than on every card.",
            OriginalValue = 29.99m, ExpiresAtUtc = DateTime.UtcNow.AddDays(2) };
        var library = new LocalLibraryService(Path.Combine(root, "offer-interface"));
        var window = new MainWindow(library, false, new[] { new FeedProvider("GamerPower", _ =>
        { fetchCount++; return Task.FromResult<IReadOnlyList<GameOffer>>(new[] { current }); }) });
        window.Show(); window.RefreshAsync().GetAwaiter().GetResult(); Dispatcher.UIThread.RunJobs();
        var details = window.GetLogicalDescendants().OfType<Button>().Single(b => b.Content as string == "Details" && ReferenceEquals(b.Tag, current));
        var icons = window.GetLogicalDescendants().OfType<Button>().Where(b => b.Classes.Contains("offer-icon") && ReferenceEquals(b.Tag, current)).ToArray();
        check(icons.Length == 5 && icons.All(b => !string.IsNullOrWhiteSpace(AutomationProperties.GetName(b))), "Compact status/tracking buttons all have accessible action names");
        var address = icons.Single(b => AutomationProperties.GetName(b)!.StartsWith("Address status"));
        var pointer = address.TranslatePoint(new Avalonia.Point(16, 16), window)!.Value;
        window.MouseMove(pointer, RawInputModifiers.None); Dispatcher.UIThread.RunJobs();
        check(address.IsPointerOver, "Headless pointer reaches the compact hover target");
        var glyph = address.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
        var hoverBackground = window.Resources["AccentHoverBrush"] as SolidColorBrush;
        check(glyph?.Foreground is SolidColorBrush glyphColor && hoverBackground != null &&
            ThemeCatalog.Contrast(glyphColor.Color.ToString(), hoverBackground.Color.ToString()) >= 4.5,
            "Rendered hover glyph keeps readable contrast against its accent background");
        ToolTip.SetIsOpen(address, true); Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        check(ToolTip.GetTip(address) is TextBlock tip && tip.Text?.Contains("independent-store.com") == true, "Actual address hover binding shows the destination");
        ToolTip.SetIsOpen(address, false);
        var price = icons.Single(b => AutomationProperties.GetName(b)!.StartsWith("Price status"));
        current.LastPriceCheckUtc = DateTime.UtcNow; current.PriceEvidence = "Fixture catalog evidence";
        current.Verification = VerificationResult.VerifiedFree; Dispatcher.UIThread.RunJobs();
        check(price.Content as string == "✓" && price.Classes.Contains("status-good"), "Actual price icon updates when verified evidence arrives");
        ToolTip.SetIsOpen(price, true); Dispatcher.UIThread.RunJobs();
        current.PriceEvidence = "Fixture nonzero price"; current.Verification = VerificationResult.NotFree; Dispatcher.UIThread.RunJobs();
        check(price.Content as string == "!" && price.Classes.Contains("status-warning") && !price.Classes.Contains("status-good") &&
            ToolTip.GetTip(price) is TextBlock priceTip && priceTip.Text?.Contains("nonzero") == true && !current.ClaimButtonEnabled,
            "Open price hover popup and warning icon update together when evidence rejects the offer");
        ToolTip.SetIsOpen(price, false);
        current.Verification = VerificationResult.Unverified; current.LastPriceCheckUtc = null;
        current.PriceEvidence = "No supported store price verification · Check checkout";
        check(!window.FindControl<Border>("SourceDetailsPanel")!.IsVisible, "Verbose provider health starts collapsed");
        window.FindControl<Button>("FeedsButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(window.FindControl<Border>("SourceDetailsPanel")!.IsVisible, "Provider health can be pinned with a keyboard-accessible button");
        address.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
        var overlay = window.FindControl<Grid>("OfferDetailsOverlay")!;
        check(overlay.IsVisible && ReferenceEquals(overlay.DataContext, current) && fetchCount == 1, "Clicking an icon opens local details without another feed/browser request");
        check(!window.FindControl<Grid>("MainArea")!.IsEnabled && !window.FindControl<Border>("SidebarPanel")!.IsEnabled, "Open details prevents keyboard interaction with covered controls");
        check(current.RegionalNotice.Contains("US") && current.DisplayClaimUrl.Contains("https://") && current.LastFetchedUtc.HasValue && current.LastAddressCheckUtc.HasValue, "Details contains region, full destination and actual provider/address times");
        window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        check(!overlay.IsVisible && overlay.DataContext == null && window.FindControl<Grid>("MainArea")!.IsEnabled, "Escape closes details and restores background interaction");
        details.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
        if (screenshots) Save(window, "Offer-Details-Drawer.png");
        window.FindControl<Button>("CloseOfferDetailsButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(!overlay.IsVisible, "Visible Details and Close buttons work without hover");
        details.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F1 }); Dispatcher.UIThread.RunJobs();
        check(window.FindControl<ScrollViewer>("HelpScroll")!.IsVisible && !overlay.IsVisible && window.FindControl<Button>("NavHelp")!.Classes.Contains("selected"), "F1 opens highlighted Help and closes offer details");
        var search = window.FindControl<TextBox>("HelpSearchBox")!;
        search.Text = "backups"; Dispatcher.UIThread.RunJobs();
        check(window.FindControl<ItemsControl>("HelpTopics")!.Items.Count > 0 && window.FindControl<ItemsControl>("HelpTopics")!.Items.Count < HelpGuide.Topics.Count, "Help search filters real topic controls");
        search.Text = "unmatchable-xyz-777"; Dispatcher.UIThread.RunJobs();
        check(window.FindControl<TextBlock>("HelpEmptyText")!.IsVisible, "Unmatched help queries show a useful empty state");
        search.Text = ""; Dispatcher.UIThread.RunJobs();
        if (screenshots) Save(window, "Help-Guide.png");
        window.FindControl<Button>("NavDiscover")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
        window.FindControl<Border>("SourceDetailsPanel")!.IsVisible = false;
        window.FindControl<ScrollViewer>("DiscoveryScroll")!.Offset = new Avalonia.Vector(0, 0);
        Dispatcher.UIThread.RunJobs();
        if (screenshots) Save(window, "Compact-Offer-Cards.png");
        details = window.GetLogicalDescendants().OfType<Button>().Single(b => b.Content as string == "Details" && ReferenceEquals(b.Tag, current));
        details.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.RefreshAsync().GetAwaiter().GetResult(); Dispatcher.UIThread.RunJobs();
        check(!overlay.IsVisible, "Refresh closes details before replacing offers");
        window.Width = 1000; window.Height = 700; Dispatcher.UIThread.RunJobs();
        window.FindControl<Button>("NavHelp")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
        check(window.FindControl<ScrollViewer>("HelpScroll")!.IsVisible && window.FindControl<Button>("NavHelp")!.Bounds.Height > 0, "Help remains available at the minimum supported window size");
        if (screenshots) Save(window, "Help-Minimum-Window.png");
        window.AllowExit(); window.Close();
        RunGridLayout(check, root, screenshots);
    }
    private static void RunGridLayout(Action<bool, string> check, string root, bool screenshots)
    {
        var window = new MainWindow(new LocalLibraryService(Path.Combine(root, "grid-layout")), false);
        window.Show();
        var groups = Enumerable.Range(0, 7).Select(i => new OfferGroup(
            i == 0 ? "A deliberately long game title that must remain inside its compact group header" : $"Game {i + 1}",
            Enumerable.Range(0, i == 6 ? 2 : 1).Select(route => new GameOffer {
                Id = i * 10 + route, Title = $"Game {i + 1}", SourceName = route == 0 ? "GamerPower" : "CheapShark",
                ClaimUrl = "https://independent-store.com/game", Platforms = "PC"
            }).ToArray())).ToArray();
        var list = window.FindControl<ItemsControl>("OffersList")!;
        list.ItemsSource = groups;
        foreach (var width in new[] { 1000, 1280, 1600 })
        foreach (var compact in new[] { false, true })
        {
            window.Width = width; window.Height = 900;
            window.FindControl<CheckBox>("SidebarCollapsedCheck")!.IsChecked = compact;
            Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var expanders = list.GetVisualDescendants().OfType<Expander>().Where(e => e.DataContext is OfferGroup).ToArray();
            check(expanders.Length == 7 && expanders.All(e => e.Bounds.Width <= 303), $"{width}px / compact={compact}: offer groups keep compact widths");
            var first = expanders[0].TranslatePoint(default, list)!.Value;
            var second = expanders[1].TranslatePoint(default, list)!.Value;
            var last = expanders[^1].TranslatePoint(default, list)!.Value;
            check(second.X > first.X && Math.Abs(second.Y - first.Y) < 1 && last.Y > first.Y,
                $"{width}px / compact={compact}: independent games share a row and wrap");
            check(expanders.All(e => e.TranslatePoint(default, list)!.Value.X + e.Bounds.Width <= list.Bounds.Width + 1),
                $"{width}px / compact={compact}: groups fit without horizontal overflow");
            check(expanders[^1].GetLogicalDescendants().OfType<Button>().Count(b => b.Content as string == "Details") == 2,
                $"{width}px / compact={compact}: both duplicate routes remain available together");
            if (screenshots && width == 1280 && compact) Save(window, "Hotfix-Compact-Grid.png");
        }
        window.AllowExit(); window.Close();
    }
    private static void Save(Window window, string name)
    {
        using var frame = window.CaptureRenderedFrame();
        if (frame == null) throw new Exception("Headless screen did not render.");
        frame.Save(Path.Combine(AppContext.BaseDirectory, "theme-screenshots", name), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }
}
