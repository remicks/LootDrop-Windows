using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using LootDrop.Services;

namespace LootDrop.Views;

public partial class MainWindow
{
    private DesktopDockService? _desktopDock;
    private bool _dockEnabled, _dockRight = true, _updatingDock, _dockAppearance, _dockOpened;
    private string _dockMonitor = "";
    private int _dockWidth = 400;
    private PixelPoint _normalPosition;
    private Size _normalSize;
    private WindowState _normalState;
    private bool _normalTopmost;
    private DockMonitor[] _dockMonitors = Array.Empty<DockMonitor>();

    private void InitializeDesktopDock()
    {
        UpdateDockCardWidths();
        SizeChanged += (_, _) => UpdateDockCardWidths();
        DockEnableCheck.IsCheckedChanged += (_, _) => ChangeDockPreferences();
        DockEdgeBox.SelectionChanged += (_, _) => ChangeDockPreferences();
        DockMonitorBox.SelectionChanged += (_, _) => ChangeDockPreferences();
        DockWidthBox.SelectionChanged += (_, _) => ChangeDockPreferences();
        DockNavigationBox.SelectionChanged += (_, _) =>
        {
            if (!_updatingDock && _dockAppearance && DockNavigationBox.SelectedItem is ComboBoxItem { Tag: string section }) NavigateTo(section);
        };
        Opened += (_, _) =>
        {
            _dockOpened = true;
            RefreshDockControls();
            if (_dockEnabled && !launchHidden) Dispatcher.UIThread.Post(ApplyDesktopDock, DispatcherPriority.Loaded);
        };
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty || e.Property == WindowStateProperty)
            {
                if (!IsVisible || WindowState == WindowState.Minimized) _desktopDock?.Suspend();
                else if (_dockOpened && _dockEnabled) Dispatcher.UIThread.Post(ApplyDesktopDock, DispatcherPriority.Loaded);
            }
        };
        Closed += (_, _) => { _desktopDock?.Dispose(); _desktopDock = null; };
    }

    private void RefreshDockControls()
    {
        _updatingDock = true;
        try
        {
            var supported = OperatingSystem.IsWindows() && TryGetPlatformHandle()?.HandleDescriptor == "HWND";
            DockEnableCheck.IsEnabled = supported;
            if (supported) _dockMonitors = DesktopDockService.Monitors(this);
            DockMonitorBox.ItemsSource = new[] { "Current display when docking" }.Concat(_dockMonitors.Select(m => m.Label)).ToArray();
            DockMonitorBox.SelectedIndex = string.IsNullOrEmpty(_dockMonitor) ? 0 : Math.Max(0, Array.FindIndex(_dockMonitors, m => m.Id == _dockMonitor) + 1);
            DockEdgeBox.SelectedIndex = _dockRight ? 1 : 0;
            DockWidthBox.SelectedIndex = _dockWidth switch { 360 => 0, 480 => 2, 560 => 3, _ => 1 };
            DockEnableCheck.IsChecked = _dockEnabled;
            DockStatusText.Text = supported ? "Uses Windows desktop docking. Space is reserved only while the sidebar is visible." : "Desktop docking is available in the Windows desktop build.";
        }
        catch (InvalidOperationException) { DockStatusText.Text = "Display settings are unavailable. Docking is disabled."; DockEnableCheck.IsEnabled = false; }
        finally { _updatingDock = false; }
    }

    private void ChangeDockPreferences()
    {
        if (!_isReady || _updatingDock) return;
        _dockEnabled = DockEnableCheck.IsChecked == true;
        _dockRight = DockEdgeBox.SelectedIndex == 1;
        _dockWidth = DockWidthBox.SelectedIndex switch { 0 => 360, 2 => 480, 3 => 560, _ => 400 };
        _dockMonitor = DockMonitorBox.SelectedIndex > 0 && DockMonitorBox.SelectedIndex <= _dockMonitors.Length
            ? _dockMonitors[DockMonitorBox.SelectedIndex - 1].Id : "";
        ApplyDesktopDock();
        SaveLibrary();
    }

    private void ApplyDesktopDock()
    {
        if (!_dockEnabled)
        {
            _desktopDock?.Dispose(); _desktopDock = null;
            SetDockAppearance(false);
            DockStatusText.Text = "Sidebar mode is off. Desktop space is released.";
            return;
        }
        if (!IsVisible || WindowState == WindowState.Minimized) return;
        try
        {
            if (!OperatingSystem.IsWindows() || TryGetPlatformHandle()?.HandleDescriptor != "HWND")
                throw new PlatformNotSupportedException("Desktop docking requires the Windows desktop build.");
            var monitors = DesktopDockService.Monitors(this);
            var id = _dockMonitor;
            if (string.IsNullOrEmpty(id))
            {
                // Capture the current display once, not again after docking changes the work area.
                id = _desktopDock?.MonitorId ?? "";
                if (id.Length == 0)
                {
                    var current = Screens.ScreenFromWindow(this) ?? Screens.Primary;
                    id = monitors.FirstOrDefault(m => m.Bounds == current?.Bounds)?.Id ?? monitors.First().Id;
                }
            }
            _desktopDock ??= new DesktopDockService(this, DockFailed);
            SetDockAppearance(true);
            _desktopDock.Configure(id, _dockWidth, _dockRight);
            if (_desktopDock?.IsRegistered == true) DockStatusText.Text = "Desktop space is reserved. Other maximized apps use the remaining area.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException or System.Runtime.InteropServices.ExternalException)
        { DockFailed("Sidebar mode could not start safely. " + ex.Message); }
    }

    private void DockFailed(string message)
    {
        _dockEnabled = false;
        if (_dockWidth is not (360 or 400 or 480 or 560)) _dockWidth = 400;
        _desktopDock?.Dispose(); _desktopDock = null;
        SetDockAppearance(false);
        _updatingDock = true; DockEnableCheck.IsChecked = false; _updatingDock = false;
        DockStatusText.Text = message;
        StatusText.Text = message;
        SaveLibrary();
    }

    private void SetDockAppearance(bool enabled)
    {
        if (_dockAppearance == enabled) return;
        _dockAppearance = enabled;
        Classes.Set("docked", enabled);
        UpdateSettingsNavigation();
        if (enabled)
        {
            _normalPosition = Position; _normalSize = ClientSize; _normalState = WindowState; _normalTopmost = Topmost;
            WindowState = WindowState.Normal;
            MinWidth = 340; MinHeight = 380;
            CanResize = false; WindowDecorations = WindowDecorations.None;
        }
        else
        {
            Topmost = _normalTopmost;
            WindowDecorations = WindowDecorations.Full; CanResize = true;
            MinWidth = 1000; MinHeight = 700;
            Width = Math.Max(1000, _normalSize.Width); Height = Math.Max(700, _normalSize.Height);
            // A saved position can become off-screen after a display is unplugged.
            var visible = Screens.All.Any(s => s.WorkingArea.Contains(_normalPosition));
            Position = visible ? _normalPosition : Screens.Primary?.WorkingArea.Position ?? new PixelPoint(0, 0);
            WindowState = _normalState;
        }
        DockToolbar.IsVisible = enabled; SidebarPanel.IsVisible = !enabled;
        DockFiltersButton.IsVisible = enabled; DiscoveryFilters.IsVisible = !enabled;
        ShellGrid.ColumnDefinitions = enabled ? new ColumnDefinitions("*") : new ColumnDefinitions("220,*");
        Grid.SetColumn(MainArea, enabled ? 0 : 1);
        HeaderGrid.RowDefinitions = new RowDefinitions(enabled ? "Auto,Auto" : "Auto");
        Grid.SetRow(HeaderActions, enabled ? 1 : 0); Grid.SetColumn(HeaderActions, 1);
        Grid.SetColumnSpan(SearchBox, enabled ? 2 : 1);
        SearchBox.HorizontalAlignment = enabled ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        RefreshButton.Content = enabled ? "↻" : "↻ Refresh / Recheck";
        ToolTip.SetTip(RefreshButton, "Refresh offers and recheck addresses and prices");
        HeaderBar.Padding = new Thickness(enabled ? 12 : 20, enabled ? 8 : 14);
        foreach (var scroll in new[] { DiscoveryScroll, SettingsScroll, ThemesScroll, StoreSignInScroll, HelpScroll })
        {
            if (scroll.Content is Control panel)
            { panel.Margin = new Thickness(enabled ? 12 : scroll == StoreSignInScroll || scroll == DiscoveryScroll ? 22 : 25); panel.HorizontalAlignment = enabled || scroll == DiscoveryScroll || scroll == SettingsScroll || scroll == HelpScroll ? HorizontalAlignment.Stretch : HorizontalAlignment.Left; }
        }
        AvailableLabel.Text = enabled ? "DROPS" : "AVAILABLE DROPS";
        SavingsLabel.Text = enabled ? "VALUE" : "POTENTIAL SAVINGS";
        OfferDetailsPanel.Width = enabled ? double.NaN : 460;
        OfferDetailsPanel.HorizontalAlignment = enabled ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        if (!enabled) ApplySidebarLayout();
        UpdateDockCardWidths();
        UpdateDockNavigation();
    }

    private void UpdateDockCardWidths()
    {
        var width = _dockAppearance ? Math.Clamp(ClientSize.Width - 44, 302, 560) : 302;
        Resources["OfferGroupWidth"] = width;
        Resources["OfferCardWidth"] = width - 32;
        Resources["OfferCoverWidth"] = width - 60;
        Resources["OfferHeaderWidth"] = width - 76;
    }

    private void UpdateDockNavigation()
    {
        if (!_dockAppearance) return;
        _updatingDock = true;
        DockNavigationBox.SelectedItem = DockNavigationBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Tag as string == currentSection);
        _updatingDock = false;
    }
    private void Undock_Click(object? sender, RoutedEventArgs e)
    { _dockEnabled = false; _updatingDock = true; DockEnableCheck.IsChecked = false; _updatingDock = false; ApplyDesktopDock(); SaveLibrary(); }
    private void DockHide_Click(object? sender, RoutedEventArgs e) => HideToTray();
    private void DockClose_Click(object? sender, RoutedEventArgs e) => Close();
    private void DockFilters_Click(object? sender, RoutedEventArgs e) => DiscoveryFilters.IsVisible = !DiscoveryFilters.IsVisible;
}
