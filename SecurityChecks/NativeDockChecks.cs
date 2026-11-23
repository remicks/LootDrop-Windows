using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using LootDrop.Services;
using LootDrop.Views;
using System.Runtime.InteropServices;

// Explicit opt-in integration checks. A synthetic app window briefly reserves space;
// finally always closes it and independently verifies restoration. No network or real app data.
internal static class NativeDockChecks
{
    public static void Run()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        Exception? failure=null;
        var thread=new Thread(() => { try { RunSta(); } catch(Exception ex) { failure=ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if(failure!=null) throw failure;
    }
    private static void RunSta()
    {
        if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        AppBuilder.Configure<LootDrop.App>().UsePlatformDetect().WithInterFont()
            .With(new Win32PlatformOptions { RenderingMode=new[]{Win32RenderingMode.Software} }).SetupWithoutStarting();
        var root=Path.Combine(AppContext.BaseDirectory,"native-dock-"+Guid.NewGuid().ToString("N"));
        var library=new LocalLibraryService(root);
        library.Save(new LibraryState { Games=new(){new SavedGame {Id=101,Title="Desktop sidebar validation",IsWishlisted=true,ClaimUrl="https://store.epicgames.com/fixture"}} });
        var window=new MainWindow(library,false);
        Window? other=null;
        using var done=new CancellationTokenSource();
        Exception? error=null;
        var monitors=DesktopDockService.Monitors(window);
        var before=monitors.ToDictionary(m=>m.Id,m=>WorkArea(m.Bounds));
        var count=0;
        void Check(bool valid,string name) { if(!valid) throw new Exception("FAILED: "+name); count++; Console.WriteLine("PASS NATIVE "+name); }
        async Task Settle() { await Task.Delay(450); }
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                window.Show(); await Settle();
                foreach(var monitor in monitors)
                {
                    // Skip displays where a useful narrow sidebar cannot fit at this DPI.
                    try { DockGeometry.Request(monitor.Bounds,monitor.Scale,400,true); }
                    catch(InvalidOperationException) { Console.WriteLine("SKIP NATIVE display too small: "+monitor.Label); continue; }
                    var monitorBox=window.FindControl<ComboBox>("DockMonitorBox")!;
                    monitorBox.SelectedIndex=Array.FindIndex(monitors,m=>m.Id==monitor.Id)+1;
                    var edge=window.FindControl<ComboBox>("DockEdgeBox")!;
                    edge.SelectedIndex=1;
                    window.FindControl<CheckBox>("DockEnableCheck")!.IsChecked=true;
                    await Settle();
                    Check(library.Load().DesktopDockEnabled,"Dock registration succeeds on "+monitor.Label);
                    var work=WorkArea(monitor.Bounds);
                    Check(work.Right<before[monitor.Id].Right && work.X==before[monitor.Id].X,"Windows reserves the right strip on "+monitor.Label);
                    foreach(var untouched in monitors.Where(m=>m.Id!=monitor.Id)) Check(WorkArea(untouched.Bounds)==before[untouched.Id],"Other display work area stays unchanged");
                    var hwnd=window.TryGetPlatformHandle()!.Handle;
                    var reserved=WorkArea(monitor.Bounds);
                    var callback=RegisterWindowMessageW("LootDrop.WindowsV2.AppBar.Callback.1");
                    for(var notification=0;notification<12;notification++) SendMessageW(hwnd,callback,new IntPtr(1),IntPtr.Zero);
                    await Settle();
                    Check(WorkArea(monitor.Bounds)==reserved,"Repeated position notifications do not accumulate reservations");
                    SendMessageW(hwnd,callback,new IntPtr(2),new IntPtr(1)); await Settle();
                    Check(!window.Topmost,"Fullscreen-start notification yields the sidebar z-order");
                    SendMessageW(hwnd,callback,new IntPtr(2),IntPtr.Zero); await Settle();
                    Check(window.Topmost,"Fullscreen-end notification restores sidebar z-order");
                    SendMessageW(hwnd,RegisterWindowMessageW("TaskbarCreated"),IntPtr.Zero,IntPtr.Zero); await Settle();
                    Check(library.Load().DesktopDockEnabled && WorkArea(monitor.Bounds)==reserved,"Simulated shell restart re-registers without leaking or growing reserved space");
                    Check(GetWindowRect(hwnd,out var appRect) && appRect.Left>=work.Right && appRect.Top>=monitor.Bounds.Y && appRect.Bottom<=monitor.Bounds.Bottom,"Native sidebar occupies the approved reserved strip");
                    other=new Window {Title="LootDrop isolated maximize test",Width=700,Height=500,Position=new PixelPoint(work.X+10,work.Y+10),ShowActivated=false};
                    other.Show(); other.WindowState=WindowState.Maximized; await Settle();
                    Check(GetWindowRect(other.TryGetPlatformHandle()!.Handle,out var otherRect) && otherRect.Right<=work.Right+16,"A real maximized window respects the sidebar reservation");
                    other.Close(); other=null;
                    edge.SelectedIndex=0; await Settle();
                    work=WorkArea(monitor.Bounds);
                    Check(work.X>before[monitor.Id].X && work.Right==before[monitor.Id].Right,"Changing to the left edge releases the old edge");
                    window.Hide(); await Settle();
                    Check(WorkArea(monitor.Bounds)==before[monitor.Id],"Hiding releases all reserved desktop space");
                    window.RestoreFromTray(); await Settle();
                    Check(WorkArea(monitor.Bounds).X>before[monitor.Id].X,"Tray restoration reserves space again");
                    window.WindowState=WindowState.Minimized; await Settle();
                    Check(WorkArea(monitor.Bounds)==before[monitor.Id],"Minimizing releases reserved space");
                    window.RestoreFromTray(); await Settle();
                    window.FindControl<CheckBox>("DockEnableCheck")!.IsChecked=false; await Settle();
                    Check(WorkArea(monitor.Bounds)==before[monitor.Id],"Disabling docking restores the original work area");
                    Check(window.MinWidth==1000 && window.CanResize,"Disabling restores normal window behavior");
                }
                // Missing-display and failure recovery without changing physical display configuration.
                window.FindControl<CheckBox>("DockEnableCheck")!.IsChecked=true; await Settle();
                typeof(MainWindow).GetField("_dockMonitor",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(window,"missing-display");
                typeof(MainWindow).GetMethod("ApplyDesktopDock",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(window,null); await Settle();
                var primary=monitors.Single(m=>m.Bounds==window.Screens.Primary!.Bounds);
                Check(WorkArea(primary.Bounds)!=before[primary.Id],"Missing selected display falls back to the primary display");
                typeof(MainWindow).GetField("_dockWidth",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(window,0);
                typeof(MainWindow).GetMethod("ApplyDesktopDock",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(window,null); await Settle();
                Check(!library.Load().DesktopDockEnabled && window.CanResize,"Invalid geometry disables docking and restores a usable normal window");
                foreach(var display in monitors) Check(WorkArea(display.Bounds)==before[display.Id],"Failure path releases reserved work area");
                typeof(MainWindow).GetField("_dockWidth",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(window,400);
                // Saved-enabled state must resume only when visible, including hidden startup.
                var hiddenLibrary=new LocalLibraryService(Path.Combine(root,"hidden-startup"));
                hiddenLibrary.Save(new LibraryState {DesktopDockEnabled=true});
                var hidden=new MainWindow(hiddenLibrary,false);
                try
                {
                    typeof(MainWindow).GetField("launchHidden",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(hidden,true);
                    hidden.Show(); await Settle();
                    Check(!hidden.IsVisible,"Hidden-startup fixture hides rather than remaining docked");
                    foreach(var display in monitors) Check(WorkArea(display.Bounds)==before[display.Id],"Hidden startup leaves the work area unchanged");
                    hidden.RestoreFromTray(); await Settle();
                    Check(hidden.IsVisible && monitors.Any(m=>WorkArea(m.Bounds)!=before[m.Id]),"Restoring saved-enabled hidden startup acquires a reservation");
                }
                finally {hidden.AllowExit();hidden.Close();}
                await Settle();
                // Close while registered: do not disable first.
                window.FindControl<CheckBox>("DockEnableCheck")!.IsChecked=true; await Settle();
                Check(library.Load().DesktopDockEnabled,"Closing test starts with an active reservation");
            }
            catch(Exception ex) { error=ex; }
            finally
            {
                other?.Close(); window.AllowExit(); window.Close();
                await Settle();
                try { foreach(var monitor in monitors) Check(WorkArea(monitor.Bounds)==before[monitor.Id],"Final close restores "+monitor.Label); }
                catch(Exception ex) { error??=ex; }
                done.Cancel();
            }
        });
        Dispatcher.UIThread.MainLoop(done.Token);
        if(error!=null) throw error;
        Console.WriteLine($"Completed {count} native desktop docking checks. Original work areas restored; real user data untouched.");
    }
    private static PixelRect WorkArea(PixelRect bounds)
    {
        var rect=new Rect {Left=bounds.X,Top=bounds.Y,Right=bounds.Right,Bottom=bounds.Bottom};
        var info=new Info {Size=(uint)Marshal.SizeOf<Info>()};
        if(!GetMonitorInfoW(MonitorFromRect(ref rect,2),ref info)) throw new Exception("Cannot read desktop work area");
        return new PixelRect(info.Work.Left,info.Work.Top,info.Work.Right-info.Work.Left,info.Work.Bottom-info.Work.Top);
    }
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll",ExactSpelling=true,CharSet=CharSet.Unicode)] private static extern uint RegisterWindowMessageW(string name);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll",ExactSpelling=true)] private static extern IntPtr SendMessageW(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] private struct Rect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] private struct Info {public uint Size;public Rect Monitor,Work;public uint Flags;}
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll",ExactSpelling=true)] private static extern IntPtr MonitorFromRect(ref Rect rect,uint flags);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll",ExactSpelling=true,CharSet=CharSet.Unicode)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfoW(IntPtr monitor,ref Info info);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll",ExactSpelling=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr window,out Rect rect);
}
