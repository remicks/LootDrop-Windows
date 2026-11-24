using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;

namespace LootDrop.Services;

public sealed record AppFontChoice(string Key, string Name, string Description, string FamilyName, string Resource)
{
    public FontFamily Family => new(Resource);
}

public static class FontCatalog
{
    // Only these bundled resources may be selected. Never load fonts from user paths or URLs.
    public static IReadOnlyList<AppFontChoice> Choices { get; } = Array.AsReadOnly(new[]
    {
        new AppFontChoice("outfit", "Outfit", "Soft, geometric and modern · default", "Outfit", "avares://LootDrop/Assets/Fonts#Outfit"),
        new AppFontChoice("space", "Space Grotesk", "Technical, distinctive and spacious", "Space Grotesk", "avares://LootDrop/Assets/Fonts#Space Grotesk"),
        new AppFontChoice("sora", "Sora", "Crisp, balanced and game-inspired", "Sora", "avares://LootDrop/Assets/Fonts#Sora"),
        new AppFontChoice("inter", "Inter", "Familiar, compact and highly readable", "Inter", "avares://Avalonia.Fonts.Inter/Assets#Inter")
    });
    public static bool IsValid(string? key) => Choices.Any(c => c.Key == key);
    public static AppFontChoice Resolve(string? key) => Choices.FirstOrDefault(c => c.Key == key) ?? Choices[0];
}
