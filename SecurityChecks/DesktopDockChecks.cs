using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using LootDrop.Services;
using LootDrop.Views;
using System.Reflection;

internal static class DesktopDockChecks
{
    public static void RunCore(Action<bool,string> check, string root)
    {
        var monitor = new PixelRect(-1920, -200, 1920, 1080);
        check(DockGeometry.Request(monitor,1,400,false) == new PixelRect(-1920,-200,400,1080), "Dock supports displays left of the primary and negative coordinates");
        check(DockGeometry.Request(monitor,1,400,true) == new PixelRect(-400,-200,400,1080), "Right docking anchors to the requested display edge");
        check(DockGeometry.Request(new PixelRect(0,0,3840,2160),2,400,true).Width == 800, "Sidebar width converts logical pixels to the selected display DPI");
        check(DockGeometry.Request(new PixelRect(0,0,1200,900),1,560,false).Width == 540, "Dock width is reduced to preserve space for other apps");
        foreach (var (bounds, scale, width) in new[] {
            (new PixelRect(0,0,800,600),1d,400), (monitor,double.NaN,400), (monitor,0d,400),
            (monitor,1d,int.MaxValue), (new PixelRect(0,0,1920,200),1d,400) })
        {
            try { DockGeometry.Request(bounds,scale,width,false); check(false,"Invalid dock geometry rejected"); }
            catch (InvalidOperationException) { check(true,"Invalid dock geometry rejected without desktop changes"); }
        }
        var library = new LocalLibraryService(Path.Combine(root,"dock-settings"));
        check(!library.Load().DesktopDockEnabled, "Desktop reservation is opt-in for existing and new installs");
        var state = new LibraryState { DesktopDockEnabled=true, DesktopDockRight=false, DesktopDockWidth=480, DesktopDockMonitor=@"\\.\DISPLAY2" };
        library.Save(state);
        var restored = library.Load();
        check(restored.DesktopDockEnabled && !restored.DesktopDockRight && restored.DesktopDockWidth==480 && restored.DesktopDockMonitor==state.DesktopDockMonitor, "Local docking preferences round-trip without credentials or network access");
        foreach(var invalid in new[] { new LibraryState { DesktopDockWidth=int.MaxValue },new LibraryState { DesktopDockMonitor=new string('X',33) }, new LibraryState { DesktopDockMonitor="bad\nmonitor" } })
        {
            try { LocalLibraryService.Validate(invalid); check(false,"Invalid dock preferences rejected"); }
            catch (InvalidDataException) { check(true,"Invalid local dock preference rejected"); }
        }
        var transfer = new LibraryTransferService(library);

        // Existing import checks exercise replacement. Export deliberately strips device preferences.
        var export = transfer.Export();
        var exported = System.Text.Json.JsonSerializer.Deserialize<LibraryExport>(export)!.Library;
        check(!exported.DesktopDockEnabled && exported.DesktopDockMonitor=="", "Collection export excludes desktop docking and monitor preferences");
        var imported = transfer.Apply(transfer.PreviewExport(export));
        check(imported.DesktopDockEnabled && imported.DesktopDockWidth==480 && imported.DesktopDockMonitor==state.DesktopDockMonitor, "Collection import preserves local docking preferences");
    }

    public static void RunWindow(Action<bool,string> check,string root,bool screenshots)
    {
        var library = new LocalLibraryService(Path.Combine(root,"dock-layout"));
        library.Save(new LibraryState { Games = new() { new SavedGame { Id=101,Title="Sidebar layout fixture",IsWishlisted=true,Platforms="PC",ClaimUrl="https://store.epicgames.com/fixture" } } });
        var window = new MainWindow(library,false);
        window.Show();
        var layout = typeof(MainWindow).GetMethod("SetDockAppearance",BindingFlags.NonPublic|BindingFlags.Instance)!;
        layout.Invoke(window,new object[]{true});
        window.Width=400; window.Height=900;
        window.FindControl<ComboBox>("DockNavigationBox")!.SelectedIndex=1;
        Dispatcher.UIThread.RunJobs();
        check(!window.FindControl<Border>("SidebarPanel")!.IsVisible && window.FindControl<Border>("DockToolbar")!.IsVisible,"Dock layout replaces wide navigation with an accessible page menu");
        check(window.FindControl<ItemsControl>("OffersList")!.Items.Count==1,"Single-pane sidebar preserves synthetic collection cards");
        check(window.FindControl<Grid>("MainArea")!.Bounds.Width<=400,"Dock content fits the narrow window");
        check(!window.FindControl<StackPanel>("DiscoveryFilters")!.IsVisible,"Sidebar filters start collapsed to preserve vertical space");
        check(window.FindControl<Border>("OfferDetailsPanel")!.HorizontalAlignment==Avalonia.Layout.HorizontalAlignment.Stretch,"Offer details use the narrow pane rather than a fixed 460 pixel drawer");
        if(screenshots) { using var frame=window.CaptureRenderedFrame(); frame?.Save(Path.Combine(AppContext.BaseDirectory,"theme-screenshots","Sidebar-Collection.png"),new Avalonia.Media.Imaging.PngBitmapEncoderOptions()); }
        window.Width=360; Dispatcher.UIThread.RunJobs();
        check(window.FindControl<Grid>("MainArea")!.Bounds.Width<=360 && (double)window.Resources["OfferGroupWidth"]! <= 316,"Minimum sidebar width keeps a single fitting card column");
        window.Width=400; Dispatcher.UIThread.RunJobs();
        window.FindControl<ComboBox>("DockNavigationBox")!.SelectedIndex=7;
        Dispatcher.UIThread.RunJobs();
        check(window.FindControl<ScrollViewer>("SettingsScroll")!.IsVisible,"Sidebar page menu keeps settings reachable");
        if(screenshots) { using var frame=window.CaptureRenderedFrame(); frame?.Save(Path.Combine(AppContext.BaseDirectory,"theme-screenshots","Sidebar-Settings.png"),new Avalonia.Media.Imaging.PngBitmapEncoderOptions()); }
        layout.Invoke(window,new object[]{false}); Dispatcher.UIThread.RunJobs();
        check(window.MinWidth==1000 && window.CanResize && window.FindControl<Border>("SidebarPanel")!.IsVisible,"Returning to normal restores resizing and navigation");
        window.AllowExit(); window.Close();
    }
}
