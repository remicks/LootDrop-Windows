using Microsoft.Win32;
using System;
using System.IO;

namespace LootDrop.Services;

/// <summary>
/// Registers the current user's LootDrop executable in HKCU Run (no elevation required).
/// Startup is opt-in and always passes --tray. Updates the path when an upgraded build is launched.
/// </summary>
public static class WindowsStartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LootDrop";

    public static bool IsEnabled()
    {
        if (!OperatingSystem.IsWindows()) return false;
        using var registry = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        var process = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(process)) return false;
        var expected = Path.GetFileName(process).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase)
            ? $"\"{process}\" \"{Path.Combine(AppContext.BaseDirectory, "LootDrop.dll")}\" --tray"
            : $"\"{process}\" --tray";
        return string.Equals(registry?.GetValue(ValueName) as string, expected, StringComparison.OrdinalIgnoreCase);
    }

    public static void SetEnabled(bool enabled)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Start with Windows is only available on Windows.");

        using var registry = Registry.CurrentUser.CreateSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException("Could not open the Windows startup key.");

        if (!enabled)
        {
            registry.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var process = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(process))
            throw new InvalidOperationException("Could not determine the program location.");

        string command;
        if (Path.GetFileName(process).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            var assemblyPath = Path.Combine(AppContext.BaseDirectory, "LootDrop.dll");
            if (string.IsNullOrWhiteSpace(assemblyPath))
                throw new InvalidOperationException("Could not find the LootDrop assembly.");
            command = $"\"{process}\" \"{assemblyPath}\" --tray";
        }
        else
        {
            command = $"\"{process}\" --tray";
        }

        registry.SetValue(ValueName, command, RegistryValueKind.String);
    }
}
