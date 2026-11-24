using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using LootDrop.Services;
using LootDrop.Views;

internal static class ThemeChecks
{
    public static void Run(Action<bool, string> check, string temporaryRoot, bool screenshots)
    {
        check(ThemeCatalog.Presets.Count == 18, "Eight existing plus ten new theme presets available");
        check(ThemeCatalog.Presets.Values.Count(p => p.IsLight) == 5, "Five genuinely light/pastel palettes available");
        check(ThemeCatalog.Resolve("unknown").Name == "Purple", "Unknown theme safely falls back to original Purple");
        foreach (var palette in ThemeCatalog.Presets.Values)
        {
            var brushes = ThemeCatalog.Brushes(palette);
            var surfaces = new[] { palette.Background, palette.Sidebar, palette.Panel, palette.Card, palette.Input };
            foreach (var role in new[] { "PrimaryTextBrush", "MutedTextBrush", "TextAccentBrush", "SuccessBrush", "WarningBrush" })
                check(surfaces.All(bg => ThemeCatalog.Contrast(brushes[role], bg) >= 4.5), $"{palette.Name}: {role} contrast >= 4.5 on all surfaces");
            check(ThemeCatalog.Contrast(brushes["OnAccentBrush"], brushes["AccentBrush"]) >= 4.5 &&
                ThemeCatalog.Contrast(brushes["OnAccentBrush"], brushes["AccentHoverBrush"]) >= 4.5,
                $"{palette.Name}: selected and hovered button text remains readable");
            var custom = ThemeCatalog.Brushes(palette with { Hex = palette.IsLight ? "#FEFFAA" : "#121015" });
            check(surfaces.All(bg => ThemeCatalog.Contrast(custom["TextAccentBrush"], bg) >= 4.5), $"{palette.Name}: extreme custom accent text remains readable");
        }

        AppBuilder.Configure<LootDrop.App>().UseSkia().WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
        foreach (var icon in ThemeCatalog.Presets.Values.Select(p => p.Icon).Distinct())
        {
            using var stream = Avalonia.Platform.AssetLoader.Open(new Uri($"avares://LootDrop/Assets/lootdrop-{icon}.ico"));
            var decoded = new WindowIcon(stream);
            check(decoded != null, $"Runtime {icon} window/tray icon remains embedded and decodes");
        }
        check(!Avalonia.Platform.AssetLoader.Exists(new Uri("avares://LootDrop/Assets/lootdrop-blue.png")), "Unused PNG previews are excluded from runtime assets");
        var folder = Path.Combine(temporaryRoot, "theme-window");
        new LocalLibraryService(folder).Save(new LibraryState
        {
            Games = new() { new SavedGame { Id = 101, Title = "Security-check fixture game", Description = "Synthetic saved game for palette rendering. No provider requests are made.",
                Platforms = "PC, Steam", ClaimUrl = "https://store.steampowered.com/app/101/", IsWishlisted = true, OriginalValue = 19.99m } }
        });
        var window = new MainWindow(new LocalLibraryService(folder), enableBackgroundWork: false);
        window.Show();
        window.FindControl<Button>("NavThemes")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        var themeButtons = window.GetLogicalDescendants().OfType<Button>()
            .Where(b => b.Tag is string name && ThemeCatalog.Presets.ContainsKey(name)).ToArray();
        check(themeButtons.Length == 18, "All eighteen presets have selectable UI buttons");
        var output = Path.Combine(AppContext.BaseDirectory, "theme-screenshots");
        if (screenshots) Directory.CreateDirectory(output);
        foreach (var palette in ThemeCatalog.Presets.Values)
        {
            themeButtons.Single(b => (string)b.Tag! == palette.Name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            var saved = new LocalLibraryService(folder).Load();
            check(saved.ThemeName == palette.Name, $"{palette.Name}: selection persists");
            check(window.Resources["AppBackgroundBrush"] is SolidColorBrush brush && brush.Color == Color.Parse(palette.Background), $"{palette.Name}: palette reaches actual window resources");
            var input = window.FindControl<TextBox>("CustomHexBox")!;
            check(input.Foreground is SolidColorBrush text && input.Background is SolidColorBrush background &&
                ThemeCatalog.Contrast(text.Color.ToString(), background.Color.ToString()) >= 4.5,
                $"{palette.Name}: actual HEX input text remains readable");
            if (screenshots)
            {
                using var frame = window.CaptureRenderedFrame();
                if (frame == null) throw new Exception("Theme did not render: " + palette.Name);
                frame.Save(Path.Combine(output, palette.Name.Replace(' ', '-') + ".png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                if (palette.Name is "Arctic Day" or "Neon Arcade")
                {
                    window.FindControl<Button>("NavWishlist")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Dispatcher.UIThread.RunJobs();
                    check(window.FindControl<ItemsControl>("OffersList")!.Items.Count == 1, $"{palette.Name}: synthetic collection card renders without network access");
                    using var cards = window.CaptureRenderedFrame();
                    cards?.Save(Path.Combine(output, palette.Name.Replace(' ', '-') + "-Collection.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                    window.FindControl<Button>("NavSettings")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Dispatcher.UIThread.RunJobs();
                    using var settings = window.CaptureRenderedFrame();
                    settings?.Save(Path.Combine(output, palette.Name.Replace(' ', '-') + "-Settings.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                    window.FindControl<Button>("NavThemes")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
            }
        }
        // A custom color based on a light palette must preserve light surfaces across save/load.
        themeButtons.Single(b => (string)b.Tag! == "Mint Sorbet").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.FindControl<TextBox>("CustomHexBox")!.Text = "#315B97";
        window.GetLogicalDescendants().OfType<Button>().Single(b => b.Content as string == "Apply HEX").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var customState = new LocalLibraryService(folder).Load();
        check(customState.ThemeName == "Custom" && customState.CustomThemeBasePreset == "Mint Sorbet", "Custom accent preserves its light/pastel base palette");
        window.FindControl<TextBox>("CustomThemeNameBox")!.Text = "Mint custom";
        window.GetLogicalDescendants().OfType<Button>().Single(b => b.Content as string == "Save theme").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(new LocalLibraryService(folder).Load().CustomThemes.Single().BasePreset == "Mint Sorbet", "Named custom theme stores the full base palette");
        themeButtons.Single(b => (string)b.Tag! == "Neon Arcade").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.FindControl<ComboBox>("SavedThemesBox")!.SelectedItem = "Mint custom";
        window.GetLogicalDescendants().OfType<Button>().Single(b => b.Content as string == "Load selected").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(new LocalLibraryService(folder).Load().CustomThemeBasePreset == "Mint Sorbet", "Named custom theme restores its pastel palette after a neon selection");
        window.AllowExit(); window.Close();
        var restored = new MainWindow(new LocalLibraryService(folder), enableBackgroundWork: false);
        check(restored.Resources["AppBackgroundBrush"] is SolidColorBrush restoredBrush && restoredBrush.Color == Color.Parse(ThemeCatalog.Presets["Mint Sorbet"].Background), "Custom palette survives a fresh window load");
        restored.AllowExit(); restored.Close();
        InterfaceChecks.RunWindow(check, temporaryRoot, screenshots);
        DesktopDockChecks.RunWindow(check, temporaryRoot, screenshots);
        ReliabilityChecks.RunWindow(check, temporaryRoot, screenshots);
        OfferInterfaceChecks.RunWindow(check, temporaryRoot, screenshots); StoreSignInChecks.Run(check, temporaryRoot, screenshots); UpdateChecks.RunWindow(check, temporaryRoot, screenshots); AppIconChecks.Run(check, temporaryRoot, screenshots);
    }
}



