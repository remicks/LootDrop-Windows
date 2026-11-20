using System;
using System.ComponentModel;
using LootDrop.Services;

namespace LootDrop.Models;

public sealed partial class GameOffer
{
    private DateTime? _lastFetchedUtc;
    private DateTime? _lastPriceCheckUtc;
    private DateTime? _lastAddressCheckUtc;
    public DateTime? LastFetchedUtc { get => _lastFetchedUtc; set { if (_lastFetchedUtc == value) return; _lastFetchedUtc = value; OnPropertyChanged(nameof(LastFetchedUtc)); } }
    public DateTime? LastPriceCheckUtc { get => _lastPriceCheckUtc; set { if (_lastPriceCheckUtc == value) return; _lastPriceCheckUtc = value; OnPropertyChanged(nameof(LastPriceCheckUtc)); } }
    public DateTime? LastAddressCheckUtc { get => _lastAddressCheckUtc; set { if (_lastAddressCheckUtc == value) return; _lastAddressCheckUtc = value; OnPropertyChanged(nameof(LastAddressCheckUtc)); } }

    public string DestinationHost => UrlSafety.TryHttps(ClaimUrl, out var uri) ? uri.IdnHost : "Unsupported address";
    public string DisplayClaimUrl => UrlSafety.TryHttps(ClaimUrl, out var uri) ? uri.AbsoluteUri : "Address blocked: a supported HTTPS destination is required.";
    public string ValueSummary => OriginalValue > 0 ? $"{OriginalValue:C2} value" : "Advertised free";
    public string DescriptionPreview => Description.Length > 220 ? Description[..220] + "…" : Description;
    public string TitleTooltip => (Title.Length > 160 ? Title[..160] + "…" : Title) + "\n\n" + DescriptionPreview + "\n\nClick Details for the full offer.";
    public string OriginTooltip => (PlatformStore.Length > 240 ? PlatformStore[..240] + "…" : PlatformStore) + "\n" + ClaimRouteNotice;
    public string WishlistGlyph => IsWishlisted ? "♥" : "♡";
    public string ClaimedGlyph => IsClaimed ? "✓" : "□";
    public string CompactState => IsCached ? "Cached" : !IsInLiveFeed ? "Saved" : IsExpired ? "Expired" : IsVerificationRejected || IsReportedNotFree ? "Unavailable" : "Live";
    public bool HasAddressWarning => !UrlSafety.TryHttps(ClaimUrl, out _) || ClaimLinkBroken;
    public bool HasPriceWarning => IsVerificationRejected || IsReportedNotFree || IsExpired;
    public bool HasVerifiedPrice => IsInLiveFeed && !IsCached && !HasPriceWarning && Verification == VerificationResult.VerifiedFree && LastPriceCheckUtc.HasValue;
    public string LinkStatusGlyph => HasAddressWarning ? "⊘" : IsCached || !IsInLiveFeed ? "◷" : !LinkChecked ? "…" : "↗";
    public string PriceStatusGlyph => IsCached || !IsInLiveFeed ? "◷" : HasPriceWarning ? "!" : HasVerifiedPrice ? "✓" : LastPriceCheckUtc.HasValue || LinkChecked ? "?" : "…";
    public string FreshnessGlyph => IsExpired ? "!" : IsCached || !IsInLiveFeed ? "◷" : "◴";
    public string LinkTooltip => (HasAddressWarning ? "Address blocked or unavailable." : "HTTPS address accepted; page reachability and redirect safety are NOT verified.") +
        "\nDestination: " + DestinationHost + "\n" + ClaimRouteNotice + "\n" + DisabledReason + "\nClick for offer details.";
    public string PriceTooltip => VerificationLabel + "\n" + PriceEvidence + "\nLast price check: " + DateLabel(LastPriceCheckUtc) +
        "\nA reachable address does not prove a game is free. Click for details.";
    public string FreshnessTooltip => ExpirationText + "\nLast provider refresh: " + DateLabel(LastFetchedUtc) +
        (IsCached ? "\nOffline cache: refresh required before opening." : !IsInLiveFeed ? "\nSaved record: not in the current live feed." : "\nPresent in the current live feed.") + "\nClick for details.";
    public string DisabledReason => ClaimButtonEnabled ? "Opening requires a destination preview; confirm the final price yourself." :
        IsCached ? "Offline cached listing. Refresh successfully before opening." :
        !IsInLiveFeed ? "Saved record is not in the current live feed. Refresh first." :
        IsExpired ? "The listed expiration has passed." :
        HasAddressWarning ? "The address is unsafe, unsupported or unavailable." :
        IsReportedNotFree ? "You flagged this offer as no longer free. Clear the flag only after checking it." :
        IsVerificationRejected ? "The store check did not establish an active paid-game giveaway." : "This offer cannot currently be opened.";
    public string FeedTimeText => "Last provider refresh: " + DateLabel(LastFetchedUtc);
    public string PriceTimeText => "Last price check: " + DateLabel(LastPriceCheckUtc);
    public string AddressTimeText => "Last address-format check: " + DateLabel(LastAddressCheckUtc) + " (not a page download)";
    public string RegionalNotice => "Supported Steam/Epic price checks use the US catalog. Your region, account, age, prior ownership or other store eligibility rules can change availability. Confirm the final price and eligibility at checkout.";
    private static string DateLabel(DateTime? value) => value.HasValue ? value.Value.ToLocalTime().ToString("g") + " (local time)" : "Not checked / not recorded";
    public void RefreshPresentation() => NotifyPresentation();
    private static readonly string[] PresentationProperties = { nameof(WishlistGlyph), nameof(ClaimedGlyph), nameof(CompactState), nameof(HasAddressWarning), nameof(HasPriceWarning), nameof(HasVerifiedPrice),
        nameof(LinkStatusGlyph), nameof(PriceStatusGlyph), nameof(FreshnessGlyph), nameof(LinkTooltip), nameof(PriceTooltip), nameof(FreshnessTooltip),
        nameof(DisabledReason), nameof(FeedTimeText), nameof(PriceTimeText), nameof(AddressTimeText) };
    private void NotifyPresentation()
    {
        var changed = PropertyChanged;
        if (changed == null) return;
        foreach (var name in PresentationProperties) changed(this, new PropertyChangedEventArgs(name));
    }
}
