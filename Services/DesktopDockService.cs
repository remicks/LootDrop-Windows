using System;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;

namespace LootDrop.Services;

public sealed record DockMonitor(string Id, string Label, PixelRect Bounds, double Scale);

// All geometry is in physical pixels; only the preference is in logical pixels.
public static class DockGeometry
{
    public static PixelRect Request(PixelRect monitor, double scale, int width, bool right)
    {
        if (!double.IsFinite(scale) || scale is < 0.5 or > 8 || width is < 360 or > 600 ||
            monitor.Height < 480 * scale || monitor.Width > 100000 || monitor.Height > 100000)
            throw new InvalidOperationException("This display cannot safely fit the sidebar.");
        var maximum = Math.Min(monitor.Width * .45, monitor.Width - 640 * scale);
        if (maximum < 360 * scale)
            throw new InvalidOperationException("This display needs more space for a sidebar and other apps.");
        var pixels = (int)Math.Round(Math.Min(width * scale, maximum));
        return new PixelRect(right ? monitor.Right - pixels : monitor.X, monitor.Y, pixels, monitor.Height);
    }
}

// This process owns exactly one shell registration for its own HWND. No global work-area writes,
// injected hooks, registry edits, background polling, or privileged operations are used.
public sealed class DesktopDockService : IDisposable
{
    private readonly Window _window;
    private readonly Action<string> _failed;
    private readonly Win32Properties.CustomWndProcHookCallback _hook;
    private readonly uint _callback;
    private readonly uint _taskbarCreated;
    private readonly IntPtr _hwnd;
    private bool _registered, _disposed, _queued, _positioning, _fullscreen;
    private PixelRect? _approved;
    private string _monitorId = "";
    private bool _right;
    private int _width;
    public bool IsRegistered => _registered;
    public PixelRect? ReservedBounds => _approved;
    public string MonitorId => _monitorId;

    public DesktopDockService(Window window, Action<string> failed)
    {
        _window = window; _failed = failed;
        var handle = window.TryGetPlatformHandle();
        if (!OperatingSystem.IsWindows() || handle?.HandleDescriptor != "HWND" || handle.Handle == IntPtr.Zero)
            throw new PlatformNotSupportedException("Desktop docking requires a Windows desktop window.");
        _hwnd = handle.Handle;
        _callback = RegisterWindowMessageW("LootDrop.WindowsV2.AppBar.Callback.1");
        _taskbarCreated = RegisterWindowMessageW("TaskbarCreated");
        if (_callback == 0 || _taskbarCreated == 0) throw new InvalidOperationException("Windows docking messages are unavailable.");
        _hook = WindowMessage;
        Win32Properties.AddWndProcHookCallback(window, _hook);
    }

    public static DockMonitor[] Monitors(Window window) => window.Screens.All.Select((screen, index) =>
    {
        var rect = NativeRect.From(screen.Bounds);
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        var monitor = MonitorFromRect(ref rect, 2);
        if (!GetMonitorInfoW(monitor, ref info)) throw new InvalidOperationException("Windows display information is unavailable.");
        return new DockMonitor(info.Device, $"Display {index + 1}{(screen.IsPrimary ? " (primary)" : "")} · {screen.Bounds.Width} × {screen.Bounds.Height}", screen.Bounds, screen.Scaling);
    }).ToArray();

    public void Configure(string monitorId, int width, bool right)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _monitorId = monitorId; _width = width; _right = right;
        Resume();
    }

    public void Resume()
    {
        if (_disposed || !_window.IsVisible || _window.WindowState == WindowState.Minimized) return;
        try
        {
            if (!_registered)
            {
                var data = Data(); data.Callback = _callback;
                if (SHAppBarMessage(0, ref data) == UIntPtr.Zero) throw new InvalidOperationException("Windows could not reserve a sidebar.");
                _registered = true;
            }
            Position();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ExternalException)
        {
            Suspend(); _failed(ex.Message);
        }
    }

    public void Suspend()
    {
        if (_registered)
        {
            // Clear first: ABM_REMOVE synchronously notifies other appbars.
            _registered = false;
            var data = Data(); SHAppBarMessage(1, ref data);
        }
        _approved = null;
        _fullscreen = false;
        _window.Topmost = false;
    }

    private void Position()
    {
        if (_positioning || !_registered) return;
        _positioning = true;
        try
        {
            var monitors = Monitors(_window);
            // Keep the captured display while docked. If unplugged, use the primary display.
            var monitor = monitors.FirstOrDefault(m => m.Id == _monitorId) ??
                monitors.FirstOrDefault(m => _window.Screens.Primary?.Bounds == m.Bounds) ?? monitors.FirstOrDefault()
                ?? throw new InvalidOperationException("No display is available for docking.");
            _monitorId = monitor.Id;
            var requested = DockGeometry.Request(monitor.Bounds, monitor.Scale, _width, _right);
            var data = Data(); data.Edge = _right ? 2u : 0u; data.Rect = NativeRect.From(requested);
            if (SHAppBarMessage(2, ref data) == UIntPtr.Zero) throw new InvalidOperationException("Windows could not negotiate the sidebar position.");
            if (_right) data.Rect.Left = data.Rect.Right - requested.Width;
            else data.Rect.Right = data.Rect.Left + requested.Width;
            var approved = data.Rect.ToPixelRect();
            if (approved.Width != requested.Width || approved.Height < 400 * monitor.Scale ||
                !monitor.Bounds.Contains(approved)) throw new InvalidOperationException("The available sidebar space is too small.");
            // A shell broadcast caused by our own ABM_SETPOS must not create a feedback loop.
            if (_approved != approved)
            {
                SHAppBarMessage(3, ref data);
                approved = data.Rect.ToPixelRect();
                if (!monitor.Bounds.Contains(approved) || approved.Width != requested.Width || approved.Height < 400 * monitor.Scale)
                    throw new InvalidOperationException("Windows returned an unsupported sidebar position.");
                _approved = approved;
            }
            _window.Topmost = !_fullscreen;
            if (!GetWindowRect(_hwnd, out var actual) || actual.ToPixelRect() != approved)
            {
                if (!SetWindowPos(_hwnd, IntPtr.Zero, approved.X, approved.Y, approved.Width, approved.Height, 0x14))
                    throw new InvalidOperationException("Windows could not position the sidebar.");
            }
        }
        finally { _positioning = false; }
    }

    private void QueuePosition()
    {
        if (_queued || _disposed || _positioning) return;
        _queued = true;
        Dispatcher.UIThread.Post(() => { _queued = false; if (!_disposed) Resume(); }, DispatcherPriority.Loaded);
    }

    private IntPtr WindowMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Notification values are never treated as pointers. Leave Avalonia's message processing intact.
        try
        {
            if (_disposed) return IntPtr.Zero;
            if (message == _taskbarCreated)
            { Suspend(); QueuePosition(); }
            else if (message == _callback && _registered)
            {
                if (wParam.ToInt64() == 1) QueuePosition(); // ABN_POSCHANGED
                else if (wParam.ToInt64() == 2)
                {
                    _fullscreen = lParam != IntPtr.Zero;
                    _window.Topmost = !_fullscreen;
                    if (_fullscreen) SetWindowPos(_hwnd, new IntPtr(1), 0, 0, 0, 0, 0x13); // HWND_BOTTOM
                }
            }
            else if (message is 0x007E or 0x02E0 or 0x001A) QueuePosition(); // display, DPI, settings
            else if (_registered && message is 0x0006 or 0x0047)
            {
                var data = Data(); SHAppBarMessage(message == 0x0006 ? 6u : 9u, ref data);
            }
            else if (message == 0x0082) Suspend(); // WM_NCDESTROY, before the HWND is gone
        }
        catch (Exception ex) when (ex is InvalidOperationException or ExternalException)
        {
            // Do not allow a managed exception to cross a native window callback.
            Suspend(); Dispatcher.UIThread.Post(() => { if (!_disposed) _failed("Desktop docking was stopped safely."); });
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Suspend();
        Win32Properties.RemoveWndProcHookCallback(_window, _hook);
    }

    private AppBarData Data() => new() { Size = (uint)Marshal.SizeOf<AppBarData>(), Window = _hwnd };
    [StructLayout(LayoutKind.Sequential)] private struct AppBarData
    { public uint Size; public IntPtr Window; public uint Callback, Edge; public NativeRect Rect; public IntPtr Parameter; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
        public static NativeRect From(PixelRect rect) => new() { Left = rect.X, Top = rect.Y, Right = rect.Right, Bottom = rect.Bottom };
        public readonly PixelRect ToPixelRect() => new(Left, Top, Math.Max(0, Right - Left), Math.Max(0, Bottom - Top));
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct MonitorInfo
    {
        public uint Size; public NativeRect Monitor, Work; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("shell32.dll", ExactSpelling = true)] private static extern UIntPtr SHAppBarMessage(uint message, ref AppBarData data);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessageW(string name);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", ExactSpelling = true)] private static extern IntPtr MonitorFromRect(ref NativeRect rect, uint flags);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfoW(IntPtr monitor, ref MonitorInfo info);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
}
