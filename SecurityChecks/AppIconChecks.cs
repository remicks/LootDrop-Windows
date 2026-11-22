using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using LootDrop.Services;
using LootDrop.Views;

internal static class AppIconChecks
{
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern uint ExtractIconEx(string file,int index,IntPtr large,IntPtr small,uint count);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr LoadLibraryEx(string file,IntPtr unused,uint flags);
    [DllImport("kernel32.dll")]private static extern bool FreeLibrary(IntPtr module);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr FindResource(IntPtr module,IntPtr name,IntPtr type);
    [DllImport("kernel32.dll")]private static extern IntPtr LoadResource(IntPtr module,IntPtr resource);
    [DllImport("kernel32.dll")]private static extern IntPtr LockResource(IntPtr resource);
    [DllImport("kernel32.dll")]private static extern uint SizeofResource(IntPtr module,IntPtr resource);
    private static byte[] Read(IntPtr module,int type,int id)
    {
        var resource=FindResource(module,(IntPtr)id,(IntPtr)type);if(resource==IntPtr.Zero)throw new Exception("Native resource missing");
        var bytes=new byte[SizeofResource(module,resource)];Marshal.Copy(LockResource(LoadResource(module,resource)),bytes,0,bytes.Length);return bytes;
    }
    public static void VerifyNative(Action<bool,string> check,string executable)
    {
        check(ExtractIconEx(executable,-1,IntPtr.Zero,IntPtr.Zero,0)==8,"Windows icon picker enumerates exactly eight icons from EXE");
        var module=LoadLibraryEx(executable,IntPtr.Zero,0x22);if(module==IntPtr.Zero)throw new Exception("Cannot inspect native icons as data");
        try
        {
            foreach(var choice in AppIconCatalog.Choices.Where(c=>c.Key!="theme"))
            {
                var ico=AppIconCatalog.Bytes(choice.Key);var group=Read(module,14,32512+choice.ResourceIndex);var count=BitConverter.ToUInt16(group,4);
                check(count==7 && BitConverter.ToUInt16(ico,4)==count,choice.Label+": seven native icon resolutions retained");
                for(var i=0;i<count;i++)
                {
                    var size=(int)BitConverter.ToUInt32(ico,6+16*i+8);var offset=(int)BitConverter.ToUInt32(ico,6+16*i+12);var id=BitConverter.ToUInt16(group,6+14*i+12);
                    check(Read(module,3,id).SequenceEqual(ico.AsSpan(offset,size).ToArray()),choice.Label+": native frame bytes match embedded artwork at index "+i);
                }
            }
            check(System.Text.Encoding.UTF8.GetString(Read(module,24,1)).Contains("asInvoker"),"Native EXE retains asInvoker privilege manifest");
        }
        finally{FreeLibrary(module);}
    }
    public static void Run(Action<bool,string> check,string root,bool screenshots)
    {
        check(AppIconCatalog.Choices.Count==9 && AppIconCatalog.Choices.Skip(1).Select(c=>c.ResourceIndex).SequenceEqual(Enumerable.Range(0,8)),"Eight fixed icon choices plus theme matching have stable Windows indices");
        check(AppIconCatalog.Resolve("theme","gold").Key=="gold" && AppIconCatalog.Resolve("red","blue").Key=="red","Manual icon preference overrides theme while automatic mode follows it");
        check(AppIconCatalog.Resolve("../../outside","cyan").Key=="purple" && !AppIconCatalog.IsValid("../../outside"),"Icon resource lookup is limited to fixed embedded names");
        var store=new LocalLibraryService(Path.Combine(root,"app-icon-choice"));
        var window=new MainWindow(store,false);window.Show();
        window.FindControl<Button>("NavThemes")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
        var selector=window.FindControl<ComboBox>("AppIconBox")!;
        check(selector.Items.Count==9 && selector.SelectedItem is AppIconChoice {Key:"theme"},"New libraries start with theme-matched icons");
        foreach(var choice in AppIconCatalog.Choices.Skip(1))
        {
            selector.SelectedItem=choice;Dispatcher.UIThread.RunJobs();
            check(window.Icon!=null && window.FindControl<TextBlock>("AppIconStatus")!.Text!.Contains(choice.Label) && store.Load().AppIconKey==choice.Key,choice.Label+": actual selection changes window icon and persists preference");
            var trays=TrayIcon.GetIcons(Application.Current!);
            check(trays!=null && trays.Count>0 && trays.All(t=>ReferenceEquals(t.Icon,window.Icon)),choice.Label+": system tray retains an assigned icon");
        }
        window.GetLogicalDescendants().OfType<Button>().Single(b=>b.Tag as string=="Blue").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
        check(store.Load().AppIconKey=="gold" && window.FindControl<TextBlock>("AppIconStatus")!.Text!.Contains("Royal Gold"),"Changing app theme preserves an explicitly chosen icon");
        selector.SelectedItem=AppIconCatalog.Choices[0];Dispatcher.UIThread.RunJobs();
        check(window.FindControl<TextBlock>("AppIconStatus")!.Text!.Contains("Electric Blue"),"Theme matching immediately restores the current theme’s icon");
        selector.SelectedItem=AppIconCatalog.Choices.Single(c=>c.Key=="cyan");Dispatcher.UIThread.RunJobs();
        if(screenshots){using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(AppContext.BaseDirectory,"theme-screenshots","Eight-App-Icons.png"),new Avalonia.Media.Imaging.PngBitmapEncoderOptions());}
        window.AllowExit();window.Close();
        var restored=new MainWindow(store,false);check(restored.FindControl<ComboBox>("AppIconBox")!.SelectedItem is AppIconChoice {Key:"cyan"},"Independent icon preference survives a fresh window");restored.AllowExit();restored.Close();
        VerifyNative(check,Path.Combine(AppContext.BaseDirectory,"LootDrop.exe"));
        // ShellLink persistence is exercised on an inert file and temporary folder, never the real desktop.
        var shortcutFolder=Path.Combine(root,"icon-shortcuts");Directory.CreateDirectory(shortcutFolder);var exe=Path.Combine(shortcutFolder,"LootDrop.exe");File.WriteAllBytes(exe,new byte[]{77,90});
        Exception? failure=null;
        var thread=new Thread(()=>
        {
            try
            {
                foreach(var choice in AppIconCatalog.Choices.Skip(1))
                {
                    var shortcut=DesktopIconShortcut.Create(shortcutFolder,exe,choice);var bytes=File.ReadAllBytes(shortcut);
                    check(BitConverter.ToInt32(bytes,0)==0x4c && BitConverter.ToInt32(bytes,56)==choice.ResourceIndex,choice.Label+": real ShellLink stores expected embedded-icon index");
                    var before=File.ReadAllBytes(shortcut);var blocked=false;try{DesktopIconShortcut.Create(shortcutFolder,exe,choice);}catch(IOException){blocked=true;}
                    check(blocked && before.SequenceEqual(File.ReadAllBytes(shortcut)),choice.Label+": existing shortcut cannot be overwritten");
                }
                var bad=false;try{DesktopIconShortcut.Create(shortcutFolder,exe,new AppIconChoice("gold","../bad",99));}catch(InvalidDataException){bad=true;}
                check(bad,"Forged shortcut choices cannot write arbitrary names or resource indices");
            }
            catch(Exception ex){failure=ex;}
        });thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(failure!=null)throw failure;
    }
}

