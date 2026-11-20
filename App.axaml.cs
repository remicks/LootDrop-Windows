using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using System.IO;
using Avalonia.Controls.ApplicationLifetimes;
using System;
using Avalonia.Markup.Xaml;
using LootDrop.ViewModels;
using LootDrop.Views;

namespace LootDrop;

public partial class App : Application
{
    private MainWindow? _window;

    public void SetThemeIcon(string themeKey)
    {
        var normalized = themeKey.ToLowerInvariant();
        if (normalized is not ("purple" or "blue" or "cyan" or "green" or "red" or "orange" or "pink" or "gold"))
            normalized = "purple";
        try
        {
            var iconUri = new Uri($"avares://LootDrop/Assets/lootdrop-{normalized}.ico");
            using var stream = AssetLoader.Open(iconUri);
            var icon = new WindowIcon(stream);
            if (_window != null) _window.Icon = icon;
            var trayIcons = TrayIcon.GetIcons(this);
            if (trayIcons != null)
                foreach (var tray in trayIcons) tray.Icon = icon;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Could not load theme icon: {ex}"); }
    }


    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _window = new MainWindow { DataContext = new MainViewModel() };
            desktop.MainWindow = _window;
            _window.ApplySavedIcon();
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void TrayShow_Click(object? sender, EventArgs e) => _window?.RestoreFromTray();
    private void TraySettings_Click(object? sender, EventArgs e) => _window?.OpenSettingsFromTray();
    private void TrayExit_Click(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _window?.AllowExit();
            desktop.Shutdown();
        }
    }
}
