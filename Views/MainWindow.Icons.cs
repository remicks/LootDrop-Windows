using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LootDrop.Services;
namespace LootDrop.Views;
public partial class MainWindow
{
    private string preferredIcon="theme";
    private void InitializeIconChoices()
    {
        AppIconBox.ItemsSource=AppIconCatalog.Choices;
        foreach(var choice in AppIconCatalog.Choices)if(choice.Key==preferredIcon)AppIconBox.SelectedItem=choice;
        CreateIconShortcutButton.IsEnabled=DesktopIconShortcut.CurrentExecutable!=null;
    }
    private void AppIcon_Changed(object? sender,SelectionChangedEventArgs e)
    {
        if(!_isReady || AppIconBox.SelectedItem is not AppIconChoice choice)return;
        preferredIcon=choice.Key;ApplySavedIcon();SaveLibrary();
    }
    private string ApplyResolvedIcon(string matchingKey)
    {
        var choice=AppIconCatalog.Resolve(preferredIcon,matchingKey);
        if(Application.Current is App app)app.SetThemeIcon(choice.Key,this);else Icon=AppIconCatalog.Load(choice.Key);
        SelectedIconPreview.Source=choice.Preview;
        AppIconStatus.Text="Window and tray: "+choice.Label+". Existing desktop/pinned shortcuts keep their own icon.";
        return choice.Key;
    }
    private void CreateIconShortcut_Click(object? sender,RoutedEventArgs e)
    {
        try
        {
            var executable=DesktopIconShortcut.CurrentExecutable ?? throw new InvalidOperationException("Use the installed or portable build to create shortcuts.");
            var matching=themeName.Equals("Custom",StringComparison.OrdinalIgnoreCase) && TryHex(customAccentHex,out var color) ? ClosestIcon(color) : ThemePresets.TryGetValue(themeName,out var preset)?preset.Icon:"purple";
            var choice=AppIconCatalog.Resolve(preferredIcon,matching);
            var path=DesktopIconShortcut.Create(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),executable,choice);
            AppIconStatus.Text="Created: "+path+". Windows controls taskbar pinning; use this shortcut if you want the selected pinned icon.";
        }
        catch(Exception ex){AppIconStatus.Text="Shortcut not created: "+ex.Message;}
    }
}
