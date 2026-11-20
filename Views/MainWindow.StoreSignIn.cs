using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LootDrop.Services;
namespace LootDrop.Views;
public partial class MainWindow
{
    private void StoreSignIn_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id } || !StoreSignInShortcuts.TryCreateLaunch(id, out var start)) return;
        try
        {
            Process.Start(start);
            StoreSignInStatus.Text = "Browser handoff requested. LootDrop cannot check your sign-in status. Return to Discover after signing in.";
        }
        catch (Exception) { StoreSignInStatus.Text = "Could not open your default browser. Open the displayed official address manually."; }
    }
}
