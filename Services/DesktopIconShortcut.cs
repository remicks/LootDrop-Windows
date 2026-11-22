using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
namespace LootDrop.Services;
public static class DesktopIconShortcut
{
    public static string? CurrentExecutable => OperatingSystem.IsWindows() &&
        string.Equals(Path.GetFileName(Environment.ProcessPath),"LootDrop.exe",StringComparison.OrdinalIgnoreCase) ? Environment.ProcessPath : null;
    public static string Create(string folder,string executable,AppIconChoice choice)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        if(!AppIconCatalog.Choices.AnyChoice(choice))throw new InvalidDataException("Unsupported shortcut icon.");
        if(!Path.IsPathFullyQualified(executable) || !File.Exists(executable) || !string.Equals(Path.GetFileName(executable),"LootDrop.exe",StringComparison.OrdinalIgnoreCase))throw new IOException("Use the installed or portable LootDrop.exe.");
        if(!Directory.Exists(folder))throw new IOException("Shortcut folder missing.");
        var target=Path.Combine(folder,"LootDrop - "+choice.Label+".lnk");
        var temporary=Path.Combine(folder,"LootDrop-"+Guid.NewGuid().ToString("N")+".lnk");
        SafeLocalFiles.Check(folder,executable,target);
        if(File.Exists(target))throw new IOException("That themed shortcut already exists; it was not replaced.");
        object? instance=null;
        try
        {
            instance=new ShellLink();var link=(IShellLinkW)instance;
            link.SetPath(executable);link.SetArguments("");link.SetWorkingDirectory(Path.GetDirectoryName(executable)!);
            link.SetDescription("LootDrop by Degenerates Inc. — "+choice.Label);link.SetIconLocation(executable,choice.ResourceIndex);link.SetShowCmd(1);
            ((IPersistFile)instance).Save(temporary,true);SafeLocalFiles.Check(folder,target);File.Move(temporary,target,false);return target;
        }
        finally {if(instance!=null)Marshal.FinalReleaseComObject(instance);if(File.Exists(temporary))File.Delete(temporary);}
    }
    private static bool AnyChoice(this System.Collections.Generic.IReadOnlyList<AppIconChoice> choices,AppIconChoice choice)
    {foreach(var known in choices)if(known.Key!="theme" && known==choice)return true;return false;}
    [ComImport,Guid("00021401-0000-0000-C000-000000000046")] private class ShellLink { }
    [ComImport,InterfaceType(ComInterfaceType.InterfaceIsIUnknown),Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder path,int count,IntPtr data,uint flags);
        void GetIDList(out IntPtr id);void SetIDList(IntPtr id);
        void GetDescription([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder text,int count);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)]string text);
        void GetWorkingDirectory([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder path,int count);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)]string path);
        void GetArguments([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder arguments,int count);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)]string arguments);
        void GetHotkey(out short hotkey);void SetHotkey(short hotkey);void GetShowCmd(out int show);void SetShowCmd(int show);
        void GetIconLocation([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder file,int count,out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)]string file,int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)]string path,uint reserved);void Resolve(IntPtr window,uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)]string path);
    }
}
