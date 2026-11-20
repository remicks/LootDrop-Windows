using Avalonia.Media.Imaging;
using System;
using System.ComponentModel;

namespace LootDrop.Models;

public enum VerificationResult { Unverified, VerifiedFree, NotFree, NotGiveaway, Expired, ReportedNotFree, BrokenLink }

public sealed partial class GameOffer : INotifyPropertyChanged
{
    private Bitmap? _coverImage;
    private bool _isWishlisted;
    private bool _isClaimed;
    private bool _reportedNotFree;
    private string _availabilityNote = "Checking claim link…";
    private bool _claimLinkBroken;
    private bool _linkChecked;
    private VerificationResult _verification = VerificationResult.Unverified;
    private string _priceEvidence = "Price check pending";

    public string SourceName { get; init; } = "GamerPower";
    public int Id { get; init; }
    public string ProviderId { get; init; } = "";
    public bool IsCached { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Platforms { get; init; } = "";
    public string Worth { get; init; } = "N/A";
    public decimal OriginalValue { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public DateTime PublishedAtUtc { get; init; }
    public string ImageUrl { get; init; } = "";
    public string ClaimUrl { get; init; } = "";
    public string GamerPowerUrl { get; init; } = "";
    public int Users { get; init; }
    private bool _isInLiveFeed = true;
    public bool IsInLiveFeed
    {
        get => _isInLiveFeed;
        set { if (_isInLiveFeed == value) return; _isInLiveFeed = value; OnPropertyChanged(nameof(IsInLiveFeed)); OnPropertyChanged(nameof(ClaimButtonEnabled)); OnPropertyChanged(nameof(VerificationLabel)); }
    }

    public string StoreName => DetectStore(ClaimUrl, Platforms);
    // The advertised platform and the website used to obtain a key are not necessarily the same.
    public string PlatformStore => $"Platform: {Platforms}  •  Listed by: {SourceName}";
    public string ClaimVia => GetClaimHostLabel(ClaimUrl);
    public string ClaimRouteNotice
    {
        get
        {
            if (ClaimVia == "Unknown destination") return "Claim website unknown — check the link before opening";
            if (SourceName == "CheapShark") return "Deal link via CheapShark; final store may differ — confirm at checkout";
            var platform = Platforms;
            bool direct = (ClaimVia == "Steam" && platform.Contains("Steam", StringComparison.OrdinalIgnoreCase)) ||
                          (ClaimVia == "Epic Games" && platform.Contains("Epic Games", StringComparison.OrdinalIgnoreCase)) ||
                          (ClaimVia == "GOG" && platform.Contains("GOG", StringComparison.OrdinalIgnoreCase)) ||
                          (ClaimVia == "Itch.io" && platform.Contains("Itch.io", StringComparison.OrdinalIgnoreCase)) ||
                          (ClaimVia == "Ubisoft" && platform.Contains("Ubisoft", StringComparison.OrdinalIgnoreCase)) ||
                          (ClaimVia == "PlayStation" && platform.Contains("PlayStation", StringComparison.OrdinalIgnoreCase)) ||
                          (ClaimVia == "Xbox" && platform.Contains("Xbox", StringComparison.OrdinalIgnoreCase));
            return direct ? "Direct store claim page" : "Third-party claim page — may provide a platform key or require an account";
        }
    }
    private static string GetClaimHostLabel(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && uri.Scheme != "http")) return "Unknown destination";
        var host = uri.Host.ToLowerInvariant();
        if (host == "store.steampowered.com" || host.EndsWith(".steampowered.com") || host == "steamcommunity.com") return "Steam";
        if (host == "store.epicgames.com" || host.EndsWith(".epicgames.com")) return "Epic Games";
        if (host == "gog.com" || host.EndsWith(".gog.com")) return "GOG";
        if (host == "itch.io" || host.EndsWith(".itch.io")) return "Itch.io";
        if (host == "cheapshark.com" || host.EndsWith(".cheapshark.com")) return "CheapShark";
        if (host == "gamerpower.com" || host.EndsWith(".gamerpower.com")) return "GamerPower";
        if (host == "ubisoft.com" || host.EndsWith(".ubisoft.com") || host == "ubi.com" || host.EndsWith(".ubi.com")) return "Ubisoft";
        if (host == "playstation.com" || host.EndsWith(".playstation.com")) return "PlayStation";
        if (host == "xbox.com" || host.EndsWith(".xbox.com")) return "Xbox";
        return host.StartsWith("www.") ? host[4..] : host;
    }
    public string OriginalValueText => OriginalValue > 0 ? $"{OriginalValue:C2} → advertised free" : "Advertised free";
    public bool IsExpired => ExpiresAtUtc.HasValue && ExpiresAtUtc.Value <= DateTime.UtcNow;
    public bool IsLikelyUnavailable => IsExpired || _claimLinkBroken || !IsInLiveFeed || _reportedNotFree || _verification is VerificationResult.NotFree or VerificationResult.NotGiveaway;
    public bool IsVerificationRejected => _verification is VerificationResult.NotFree or VerificationResult.NotGiveaway or VerificationResult.BrokenLink or VerificationResult.Expired or VerificationResult.ReportedNotFree;
    public VerificationResult Verification
    {
        get => _verification;
        set { if (_verification == value) return; _verification = value; OnPropertyChanged(nameof(Verification)); OnPropertyChanged(nameof(VerificationLabel)); OnPropertyChanged(nameof(ClaimButtonEnabled)); OnPropertyChanged(nameof(IsVerificationRejected)); }
    }
    public string VerificationLabel => IsCached ? "OFFLINE CACHE · REFRESH REQUIRED" : !IsInLiveFeed ? "SAVED RECORD · REFRESH REQUIRED" : IsExpired ? "EXPIRED" : IsReportedNotFree ? "FLAGGED NOT FREE" : Verification switch
    {
        VerificationResult.VerifiedFree => "✓ VERIFIED FREE (US)",
        VerificationResult.NotFree => "✕ NOT FREE (US)",
        VerificationResult.NotGiveaway => "! FREE-TO-PLAY / NOT GIVEAWAY",
        VerificationResult.Expired => "✕ EXPIRED",
        VerificationResult.ReportedNotFree => "! FLAGGED NOT FREE",
        VerificationResult.BrokenLink => "✕ BROKEN CLAIM LINK",
        _ => "? PRICE UNVERIFIED"
    };
    public string PriceEvidence
    {
        get => _priceEvidence;
        set { if (_priceEvidence == value) return; _priceEvidence = value; OnPropertyChanged(nameof(PriceEvidence)); }
    }
    public string ExpirationText => ExpiresAtUtc is null ? "No listed expiration" : IsExpired ? "Listed expiration has passed" : $"Expires: {ExpiresAtUtc.Value.ToLocalTime():g}";

    public string AvailabilityNote
    {
        get => _availabilityNote;
        set { if (_availabilityNote == value) return; _availabilityNote = value; OnPropertyChanged(nameof(AvailabilityNote)); }
    }
    public bool ClaimLinkBroken
    {
        get => _claimLinkBroken;
        set { if (_claimLinkBroken == value) return; _claimLinkBroken = value; OnPropertyChanged(nameof(ClaimLinkBroken)); OnPropertyChanged(nameof(ClaimButtonEnabled)); }
    }
    public bool LinkChecked
    {
        get => _linkChecked;
        set { if (_linkChecked == value) return; _linkChecked = value; OnPropertyChanged(nameof(LinkChecked)); }
    }
    public bool ClaimButtonEnabled => !IsCached && !IsExpired && !ClaimLinkBroken && IsInLiveFeed && !IsReportedNotFree && !IsVerificationRejected && LootDrop.Services.UrlSafety.TryHttps(ClaimUrl, out _);
    public bool IsReportedNotFree
    {
        get => _reportedNotFree;
        set { if (_reportedNotFree == value) return; _reportedNotFree = value; OnPropertyChanged(nameof(IsReportedNotFree)); OnPropertyChanged(nameof(ReportLabel)); OnPropertyChanged(nameof(ClaimButtonEnabled)); }
    }
    public string ReportLabel => IsReportedNotFree ? "↶ Undo not-free flag" : "⚑ No longer free";
    public Bitmap? CoverImage
    {
        get => _coverImage;
        set { if (ReferenceEquals(_coverImage, value)) return; _coverImage = value; OnPropertyChanged(nameof(CoverImage)); }
    }
    public bool IsWishlisted
    {
        get => _isWishlisted;
        set { if (_isWishlisted == value) return; _isWishlisted = value; OnPropertyChanged(nameof(IsWishlisted)); OnPropertyChanged(nameof(WishlistLabel)); }
    }
    public bool IsClaimed
    {
        get => _isClaimed;
        set { if (_isClaimed == value) return; _isClaimed = value; OnPropertyChanged(nameof(IsClaimed)); OnPropertyChanged(nameof(ClaimedLabel)); }
    }
    public string WishlistLabel => IsWishlisted ? "♥ Remove from Wishlist" : "♡ Add to Wishlist";
    public string ClaimedLabel => IsClaimed ? "✓ Claimed (undo)" : "✓ Mark as Claimed";
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        if (propertyName is nameof(Verification) or nameof(PriceEvidence) or nameof(AvailabilityNote) or
            nameof(ClaimLinkBroken) or nameof(LinkChecked) or nameof(IsReportedNotFree) or nameof(IsWishlisted) or
            nameof(IsClaimed) or nameof(IsInLiveFeed) or nameof(LastFetchedUtc) or nameof(LastPriceCheckUtc) or nameof(LastAddressCheckUtc))
            NotifyPresentation();
    }

    private static string DetectStore(string claimUrl, string platforms)
    {
        if (Uri.TryCreate(claimUrl, UriKind.Absolute, out var uri))
        {
            var host = uri.Host.ToLowerInvariant();
            if (LootDrop.Services.UrlSafety.IsDomain(host, "cheapshark.com")) return "CheapShark";
            if (LootDrop.Services.UrlSafety.IsDomain(host, "epicgames.com")) return "Epic Games";
            if (LootDrop.Services.UrlSafety.IsDomain(host, "steampowered.com") || LootDrop.Services.UrlSafety.IsDomain(host, "steamcommunity.com")) return "Steam";
            if (LootDrop.Services.UrlSafety.IsDomain(host, "gog.com")) return "GOG";
            if (LootDrop.Services.UrlSafety.IsDomain(host, "itch.io")) return "Itch.io";
            if (LootDrop.Services.UrlSafety.IsDomain(host, "ubi.com") || LootDrop.Services.UrlSafety.IsDomain(host, "ubisoft.com")) return "Ubisoft";
            if (LootDrop.Services.UrlSafety.IsDomain(host, "play.google.com")) return "Google Play";
            if (LootDrop.Services.UrlSafety.IsDomain(host, "apps.apple.com") || LootDrop.Services.UrlSafety.IsDomain(host, "apple.com")) return "App Store";
            if (LootDrop.Services.UrlSafety.IsDomain(host, "microsoft.com") || LootDrop.Services.UrlSafety.IsDomain(host, "xbox.com")) return "Xbox";
            if (LootDrop.Services.UrlSafety.IsDomain(host, "playstation.com")) return "PlayStation";
        }

        var text = platforms ?? string.Empty;
        if (text.Contains("Epic Games", StringComparison.OrdinalIgnoreCase)) return "Epic Games";
        if (text.Contains("Steam", StringComparison.OrdinalIgnoreCase)) return "Steam";
        if (text.Contains("GOG", StringComparison.OrdinalIgnoreCase)) return "GOG";
        if (text.Contains("DRM-Free", StringComparison.OrdinalIgnoreCase)) return "DRM-Free";
        if (text.Contains("Itch.io", StringComparison.OrdinalIgnoreCase)) return "Itch.io";
        if (text.Contains("Ubisoft", StringComparison.OrdinalIgnoreCase)) return "Ubisoft";
        if (text.Contains("Google Play", StringComparison.OrdinalIgnoreCase)) return "Google Play";
        return "Store not identified";
    }
}
