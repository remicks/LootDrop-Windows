using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;

namespace LootDrop.Services;

public sealed record ThemePalette(string Name, string Hex, string Icon, bool IsLight,
    string Background, string Sidebar, string Panel, string Card, string Input,
    string Text, string Muted);

public static class ThemeCatalog
{
    public static IReadOnlyDictionary<string, ThemePalette> Presets { get; } = BuildPresets();

    private static IReadOnlyDictionary<string, ThemePalette> BuildPresets()
    {
        var items = new Dictionary<string, ThemePalette>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, hex, icon) in new[]
        {
            ("Purple", "#A448F4", "purple"), ("Blue", "#367BFA", "blue"),
            ("Cyan", "#0ABED4", "cyan"), ("Green", "#28B875", "green"),
            ("Red", "#E34B65", "red"), ("Orange", "#F18C36", "orange"),
            ("Pink", "#E04ABE", "pink"), ("Gold", "#DAA53B", "gold")
        })
            items[name] = new(name, hex, icon, false, "#111019", "#171522", "#211B32", "#292337", "#181422", "#F4F0FF", "#B9AFCB");
        Add("Obsidian Ice", "#72DDF7", "cyan", false, "#090D14", "#101722", "#152131", "#1A293B", "#0D1723", "#F0F7FF", "#B5C5DA");
        Add("Crimson Noir", "#FF647C", "red", false, "#120C12", "#1C111A", "#281823", "#321D29", "#1A111A", "#FFF1F5", "#D7B7C4");
        Add("Forest Night", "#68E0B0", "green", false, "#0B1512", "#102019", "#172C23", "#1D372B", "#10231B", "#EFFCF5", "#B4D0C1");
        Add("Arctic Day", "#1969C9", "blue", true, "#EFF5FB", "#E4EDF8", "#FFFFFF", "#FFFFFF", "#F5F8FC", "#15263C", "#405774");
        Add("Peach Cream", "#AC4C39", "orange", true, "#FFF3E9", "#F9E6D8", "#FFFCF8", "#FFF9F2", "#F6E8DC", "#3B2926", "#705149");
        Add("Lavender Cloud", "#7353B6", "purple", true, "#F3EFFB", "#E7DFF6", "#FFFCFF", "#FBF8FF", "#EEE8F7", "#302742", "#655575");
        Add("Mint Sorbet", "#23795F", "green", true, "#EDF9F2", "#DCEFE5", "#FBFFFC", "#F5FDF8", "#E5F3EA", "#1C382D", "#486454");
        Add("Rosewater", "#A24D76", "pink", true, "#FDF0F6", "#F5DFEB", "#FFFCFE", "#FFF7FB", "#F8E9F1", "#412536", "#735368");
        Add("Cyber Lime", "#CAFF42", "green", false, "#10130C", "#171D10", "#1B2413", "#253018", "#11190C", "#F3FFE4", "#BACDA6");
        Add("Neon Arcade", "#FF5CE1", "pink", false, "#110D20", "#1C1431", "#271B40", "#30204D", "#181024", "#FFF0FF", "#CDB7E9");
        return new ReadOnlyDictionary<string, ThemePalette>(items);

        void Add(string name, string hex, string icon, bool light, string bg, string side, string panel, string card, string input, string text, string muted)
            => items[name] = new(name, hex, icon, light, bg, side, panel, card, input, text, muted);
    }

    public static ThemePalette Resolve(string? name) => name != null && Presets.TryGetValue(name, out var palette) ? palette : Presets["Purple"];

    public static IReadOnlyDictionary<string, string> Brushes(ThemePalette palette)
    {
        var surfaces = new[] { palette.Background, palette.Sidebar, palette.Panel, palette.Card, palette.Input };
        var accent = Color.Parse(palette.Hex);
        var onAccent = Contrast(palette.Hex, "#000000") >= Contrast(palette.Hex, "#FFFFFF") ? "#000000" : "#FFFFFF";
        var textAccent = Readable(palette.Hex, surfaces, palette.IsLight);
        return new Dictionary<string, string>
        {
            ["AppBackgroundBrush"] = palette.Background, ["SidebarBrush"] = palette.Sidebar,
            ["PanelBrush"] = palette.Panel, ["CardBrush"] = palette.Card,
            ["InputBrush"] = palette.Input, ["ButtonBrush"] = palette.Input,
            ["BadgeBrush"] = palette.Input, ["AccentBrush"] = palette.Hex,
            ["OnAccentBrush"] = onAccent,
            ["AccentHoverBrush"] = Hex(Mix(accent, Color.Parse(onAccent == "#000000" ? "#FFFFFF" : "#000000"), .12)),
            ["AccentBrightBrush"] = textAccent, ["TextAccentBrush"] = textAccent,
            ["PrimaryTextBrush"] = Readable(palette.Text, surfaces, palette.IsLight),
            ["MutedTextBrush"] = Readable(palette.Muted, surfaces, palette.IsLight),
            ["SubtleTextBrush"] = Readable(palette.Muted, surfaces, palette.IsLight),
            ["SuccessBrush"] = Readable(palette.IsLight ? "#146A42" : "#64E6BA", surfaces, palette.IsLight),
            ["WarningBrush"] = Readable(palette.IsLight ? "#8F4400" : "#FFB66B", surfaces, palette.IsLight),
            ["BorderBrush"] = Hex(Mix(Color.Parse(palette.Panel), Color.Parse(palette.Text), .23))
        };
    }

    private static string Readable(string start, string[] surfaces, bool light)
    {
        var color = Color.Parse(start);
        var end = light ? Colors.Black : Colors.White;
        for (var step = 0; step <= 100; step++)
        {
            var result = Hex(Mix(color, end, step / 100.0));
            if (surfaces.All(bg => Contrast(result, bg) >= 4.5)) return result;
        }
        return Hex(end);
    }

    public static double Contrast(string first, string second)
    {
        var a = Luminance(Color.Parse(first)); var b = Luminance(Color.Parse(second));
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }
    private static double Luminance(Color color)
    {
        static double Linear(byte channel) { var n = channel / 255.0; return n <= .04045 ? n / 12.92 : Math.Pow((n + .055) / 1.055, 2.4); }
        return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
    }
    private static Color Mix(Color a, Color b, double amount) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * amount), (byte)Math.Round(a.G + (b.G - a.G) * amount), (byte)Math.Round(a.B + (b.B - a.B) * amount));
    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
