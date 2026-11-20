using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace LootDrop.Services;

public sealed record StoreSignInShortcut(string Id, string Name, string Url, string Note);
public static class StoreSignInShortcuts
{
    public const string PrivacyNotice = "LootDrop never collects, stores or transmits your store passwords, cookies or sign-in tokens. Sign-in happens in your default browser, which sends your credentials securely over the internet to the store or its chosen identity provider. Remembered sessions and saved passwords are managed by your browser/store, not LootDrop; browser sync may copy them to your browser account if enabled.";
    public const string SessionNotice = "Use the same browser profile when opening offers. Choose the store's Remember me option on a trusted personal computer. Private browsing, session expiry and security checks can require another login. LootDrop cannot detect whether you are signed in, bypass two-factor authentication or claim games automatically. Check the final address before entering credentials. To sign out, use the store website; manage saved passwords and sync in your browser settings.";
    public static IReadOnlyList<StoreSignInShortcut> All { get; } = Array.AsReadOnly(new[] {
        new StoreSignInShortcut("steam", "Steam", "https://store.steampowered.com/login/", "Official Steam sign-in"),
        new StoreSignInShortcut("epic", "Epic Games", "https://www.epicgames.com/id/login", "Official Epic sign-in"),
        new StoreSignInShortcut("gog", "GOG", "https://www.gog.com/", "Choose Sign in on the official GOG website"),
        new StoreSignInShortcut("ubisoft", "Ubisoft", "https://account.ubisoft.com/", "Official Ubisoft account page"),
        new StoreSignInShortcut("itch", "itch.io", "https://itch.io/login", "Official itch.io sign-in")
    });
    // Only fixed identifiers can produce a launch target: never accept a URL from a feed, import or control Tag.
    public static bool TryCreateLaunch(string? id, out ProcessStartInfo start)
    {
        start = null!;
        var shortcut = All.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));
        if (shortcut == null || !UrlSafety.TryHttps(shortcut.Url, out var uri)) return false;
        start = new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true };
        return true;
    }
}
