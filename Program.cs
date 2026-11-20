using Avalonia;
using System;
using System.IO;
using System.Text.Json;

namespace LootDrop;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static bool UseSoftwareRenderer()
    {
        try
        {
            return new Services.LocalLibraryService().Load().ForceSoftwareRendering;
        }
        catch { return false; }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
        if (OperatingSystem.IsWindows() && UseSoftwareRenderer())
            builder = builder.With(new Win32PlatformOptions { RenderingMode = new[] { Win32RenderingMode.Software } });
        return builder;
    }
}
