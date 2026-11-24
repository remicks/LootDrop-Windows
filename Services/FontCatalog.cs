using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;

namespace LootDrop.Services;

public sealed record AppFontChoice(string Key, string Name, string Description, string FamilyName, string Resource, string? LicenseStem = null)
{
    public FontFamily Family => new(Resource);
}

public static class FontCatalog
{
    // Only these bundled resources may be selected. Never load fonts from user paths or URLs.
    public static IReadOnlyList<AppFontChoice> Choices { get; } = Array.AsReadOnly(new[]
    {
        new AppFontChoice("outfit", "Outfit", "Soft, geometric and modern · default", "Outfit", "avares://LootDrop/Assets/Fonts#Outfit", "Outfit"),
        new AppFontChoice("space", "Space Grotesk", "Technical, distinctive and spacious", "Space Grotesk", "avares://LootDrop/Assets/Fonts#Space Grotesk", "SpaceGrotesk"),
        new AppFontChoice("sora", "Sora", "Crisp, balanced and game-inspired", "Sora", "avares://LootDrop/Assets/Fonts#Sora", "Sora"),
        new AppFontChoice("lato", "Lato", "Warm, balanced and easy to read", "Lato", "avares://LootDrop/Assets/Fonts#Lato", "Lato"),
        new AppFontChoice("montserrat", "Montserrat", "Clean geometric forms with a confident voice", "Montserrat", "avares://LootDrop/Assets/Fonts#Montserrat", "Montserrat"),
        new AppFontChoice("source", "Source Sans 3", "Neutral, open and comfortable for longer reading", "Source Sans 3", "avares://LootDrop/Assets/Fonts#Source Sans 3", "SourceSans3"),
        new AppFontChoice("plex", "IBM Plex Sans", "Precise, structured and technical", "IBM Plex Sans", "avares://LootDrop/Assets/Fonts#IBM Plex Sans", "IBMPlexSans"),
        new AppFontChoice("atkinson", "Atkinson Hyperlegible", "Distinctive letterforms with clear character shapes", "Atkinson Hyperlegible", "avares://LootDrop/Assets/Fonts#Atkinson Hyperlegible", "AtkinsonHyperlegible"),
        new AppFontChoice("inter", "Inter", "Familiar, compact and highly readable", "Inter", "avares://Avalonia.Fonts.Inter/Assets#Inter")
    });
    public static bool IsValid(string? key) => Choices.Any(c => c.Key == key);
    public static AppFontChoice Resolve(string? key) => Choices.FirstOrDefault(c => c.Key == key) ?? Choices[0];
}
