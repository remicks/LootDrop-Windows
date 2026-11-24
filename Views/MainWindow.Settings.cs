using System;
using System.Linq;
using System.IO;
using Avalonia.Platform;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using LootDrop.Services;

namespace LootDrop.Views;

public partial class MainWindow
{
    private string _fontKey = "outfit";
    private string _settingsCategory = "general";
    private bool _updatingFont, _updatingCategory;
    private static readonly string[] SettingsCategories = { "general", "startup", "layout", "appearance", "library", "updates", "about" };
    private static readonly string[] SettingsTitles = { "General", "Startup & tray", "Window & layout", "Appearance & fonts", "Library & backups", "Updates", "About LootDrop" };
    private void InitializeSettingsInterface()
    {
        _updatingFont = true;
        FontChoiceBox.ItemsSource = FontCatalog.Choices;
        FontChoiceBox.SelectedItem = FontCatalog.Resolve(_fontKey);
        _updatingFont = false;
        FontLicenseText.Text = string.Join("\n\n", new[] { "Outfit", "SpaceGrotesk", "Sora" }.Select(name => {
            using var stream = AssetLoader.Open(new Uri("avares://LootDrop/Assets/Fonts/" + name + "-OFL.txt"));
            using var reader = new StreamReader(stream); return name + "\n" + reader.ReadToEnd(); }));
        ApplyAppFont();
        SelectSettingsCategory("general");
    }
    private void FontChoice_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isReady || _updatingFont || FontChoiceBox.SelectedItem is not AppFontChoice choice) return;
        _fontKey = choice.Key;
        ApplyAppFont();
        SaveLibrary();
    }
    private void ApplyAppFont()
    {
        var choice = FontCatalog.Resolve(_fontKey);
        _fontKey = choice.Key;
        FontFamily = choice.Family;
        if (Application.Current != null) Application.Current.Resources["InterfaceFont"] = choice.Family;
        FontDescriptionText.Text = choice.Description + ". Embedded in LootDrop; nothing is installed in Windows.";
        FontPreviewText.FontFamily = choice.Family;
    }
    private void SettingsCategory_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string key })
        { if (currentSection != "settings") NavigateTo("settings"); SelectSettingsCategory(key); }
    }
    private void SettingsCategory_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (!_updatingCategory && SettingsCategoryBox.SelectedIndex >= 0)
            SelectSettingsCategory(SettingsCategories[SettingsCategoryBox.SelectedIndex]);
    }
    private void SelectSettingsCategory(string key)
    {
        var index = Array.IndexOf(SettingsCategories, key);
        if (index < 0) index = 0;
        _settingsCategory = SettingsCategories[index];
        var panels = new Control[] { SettingsGeneralPanel, SettingsStartupPanel, SettingsDockPanel, SettingsNavigationPanel,
            SettingsFontPanel, SettingsAppearancePanel, SettingsLibraryPanel, UpdatesPanel, SettingsAboutPanel };
        var categories = new[] { "general", "startup", "layout", "layout", "appearance", "appearance", "library", "updates", "about" };
        for (var i = 0; i < panels.Length; i++) panels[i].IsVisible = categories[i] == _settingsCategory;
        SettingsCategoryTitle.Text = SettingsTitles[index];
        SettingsCategoryDescription.Text = index switch
        {
            0 => "Keep discovery simple and choose which filters to remember.",
            1 => "Choose what happens when LootDrop starts, minimizes or closes.",
            2 => "Arrange navigation and reserve desktop space for a compact sidebar.",
            3 => "Make LootDrop feel like yours. Preview fonts instantly, then tune appearance.",
            4 => "Keep your collection backed up and move it between computers.",
            5 => "Check and download releases when you choose. Installation stays under your control.",
            _ => "An independent app by Degenerates Inc."
        };
        _updatingCategory = true;
        SettingsCategoryBox.SelectedIndex = index;
        _updatingCategory = false;
        foreach (var button in SettingsSubmenu.Children.OfType<Button>())
            button.Classes.Set("selected", button.Tag as string == _settingsCategory);
        SettingsScroll.Offset = default;
        SectionHeaderText.Text = "Settings / " + SettingsTitles[index];
        if (SettingsSubmenu.IsVisible) Dispatcher.UIThread.Post(() => { if (SettingsSubmenu.IsVisible) SettingsSubmenu.BringIntoView(new Rect(0, -44, SettingsSubmenu.Bounds.Width, SettingsSubmenu.Bounds.Height + 44)); }, DispatcherPriority.Loaded);
    }
    private void UpdateSettingsNavigation()
    {
        SettingsSubmenu.IsVisible = currentSection == "settings" && !sidebarCollapsed && !_dockAppearance;
        SectionHeaderText.IsVisible = currentSection is "settings" or "themes" or "signin" or "help";
        SearchBox.IsVisible = !SectionHeaderText.IsVisible;
        HeaderActions.IsVisible = !SectionHeaderText.IsVisible;
        if (currentSection == "settings") SelectSettingsCategory(_settingsCategory);
        else SectionHeaderText.Text = currentSection switch { "themes" => "Appearance / Themes & icons", "signin" => "Store sign-in shortcuts", "help" => "Help & feature guide", _ => "Discover" };
        SettingsCategoryBox.IsVisible = _dockAppearance || sidebarCollapsed;
    }
}
