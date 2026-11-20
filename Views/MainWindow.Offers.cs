using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LootDrop.Models;
using LootDrop.Services;

namespace LootDrop.Views;

public partial class MainWindow
{
    private Control? _detailsReturnFocus;
    private void InitializeOfferInterface()
    {
        HelpTopics.ItemsSource = HelpGuide.Topics; StoreSignInList.ItemsSource = StoreSignInShortcuts.All; StorePrivacyText.Text = StoreSignInShortcuts.PrivacyNotice; StoreSessionText.Text = StoreSignInShortcuts.SessionNotice;
        AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Escape && OfferDetailsOverlay.IsVisible) { CloseOfferDetails(); e.Handled = true; }
            else if (e.Key == Key.F1) { NavigateTo("help"); e.Handled = true; }
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
    }
    private void Details_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: GameOffer game } || !AllOffers.Contains(game)) return;
        _detailsReturnFocus = sender as Control;
        game.RefreshPresentation();
        OfferDetailsOverlay.DataContext = game;
        OfferDetailsOverlay.IsVisible = true;
        SidebarPanel.IsEnabled = false;
        MainArea.IsEnabled = false;
        CloseOfferDetailsButton.Focus();
    }
    private void CloseDetails_Click(object? sender, RoutedEventArgs e) => CloseOfferDetails();
    private void DetailsBackdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch || e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        { CloseOfferDetails(); e.Handled = true; }
    }
    private void CloseOfferDetails()
    {
        if (!OfferDetailsOverlay.IsVisible) return;
        OfferDetailsOverlay.IsVisible = false;
        OfferDetailsOverlay.DataContext = null;
        SidebarPanel.IsEnabled = true;
        MainArea.IsEnabled = true;
        if (_detailsReturnFocus?.IsVisible == true) _detailsReturnFocus.Focus();
        _detailsReturnFocus = null;
    }
    private void OfferHelp_Click(object? sender, RoutedEventArgs e)
    { NavigateTo("help"); HelpSearchBox.Text = "status"; }
    private void HelpSearch_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (HelpTopics == null) return;
        var found = HelpGuide.Search(HelpSearchBox.Text);
        HelpTopics.ItemsSource = found;
        if (HelpEmptyText != null) HelpEmptyText.IsVisible = found.Count == 0;
    }
    private void FeedDetails_Click(object? sender, RoutedEventArgs e) => SourceDetailsPanel.IsVisible = !SourceDetailsPanel.IsVisible;
}

