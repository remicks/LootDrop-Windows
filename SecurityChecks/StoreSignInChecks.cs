using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using LootDrop.Models;
using LootDrop.Services;
using LootDrop.Views;
using System.Text;
using System.Text.Json;

internal static class StoreSignInChecks
{
    public static void Run(Action<bool,string> check, string root, bool screenshots)
    {
        foreach (var item in StoreSignInShortcuts.All)
        {
            check(StoreSignInShortcuts.TryCreateLaunch(item.Id, out var launch) && launch.FileName == item.Url &&
                launch.UseShellExecute && launch.Arguments == "" && launch.ArgumentList.Count == 0 && launch.UserName == "",
                item.Name + " shortcut launches only its fixed HTTPS URL without credentials or arguments");
        }
        foreach (var bad in new string?[] { null, "", "Steam", "https://evil.com/", "steam?redirect=https://evil.com", "../steam", "steam\r\n", "steam.exe", "steam\u202E" })
            check(!StoreSignInShortcuts.TryCreateLaunch(bad, out _), "Unrecognized shortcut identifier rejected: " + (bad?.Replace("\r", "").Replace("\n", "") ?? "null"));
        foreach (var suffix in new[] { "\u202E", "\u200B", "\u2066", "\uFEFF" })
            check(!UrlSafety.TryHttps("https://store.steampowered.com/" + suffix + "login", out _), "Invisible URL formatting character rejected");
        foreach (var rejected in new[] { VerificationResult.Expired, VerificationResult.ReportedNotFree })
            check(!new GameOffer { ClaimUrl="https://store.steampowered.com/app/1", Verification=rejected }.ClaimButtonEnabled, "Rejected verification enum cannot enable a claim: " + rejected);
        foreach (var json in new[] { "{\"SelectedPlatforms\":null}", "{\"SelectedStores\":[null]}", "{\"SelectedStore\":null}",
            JsonSerializer.Serialize(new LibraryState { SelectedStores=Enumerable.Repeat("Steam",33).ToList() }),
            JsonSerializer.Serialize(new LibraryState { SelectedPlatforms=new() { new string('x',129) } }) })
        {
            var rejected=false;
            try { LibraryTransferService.Parse(Encoding.UTF8.GetBytes(json)); } catch (Exception ex) when (ex is IOException or InvalidDataException) { rejected=true; }
            check(rejected,"Malformed/oversized filter settings rejected at the shared import/load boundary");
        }
        var random=new Random(78); var safe=true;
        for(var i=0;i<2000;i++)
        {
            var raw=new string(Enumerable.Range(0,random.Next(0,256)).Select(_=>(char)random.Next(0,65536)).ToArray());
            try { if(UrlSafety.TryHttps(raw,out var uri)) safe &= uri.Scheme=="https" && uri.Port==443 && uri.UserInfo.Length==0; }
            catch { safe=false; }
        }
        check(safe,"2000 deterministic malformed URL inputs do not crash or escape the HTTPS boundary");
        var window=new MainWindow(new LocalLibraryService(Path.Combine(root,"signin-ui")),false);
        window.Show();
        window.FindControl<Button>("NavStoreSignIn")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        check(window.FindControl<ScrollViewer>("StoreSignInScroll")!.IsVisible && !window.FindControl<ScrollViewer>("DiscoveryScroll")!.IsVisible &&
            !window.FindControl<TextBox>("SearchBox")!.IsEnabled,"Store sign-in has a distinct navigable section");
        var list=window.FindControl<ItemsControl>("StoreSignInList")!;
        var buttons=list.GetLogicalDescendants().OfType<Button>().ToArray();
        check(buttons.Length==5 && buttons.All(b=>StoreSignInShortcuts.TryCreateLaunch(b.Tag as string,out _)),"All five real shortcut buttons bind fixed store identifiers");
        var privacy=window.FindControl<TextBlock>("StorePrivacyText")!.Text!;
        check(privacy.Contains("never collects") && privacy.Contains("over the internet") && privacy.Contains("sync"),"Visible privacy text accurately explains store transmission and optional browser sync");
        check(!window.FindControl<ScrollViewer>("StoreSignInScroll")!.GetLogicalDescendants().OfType<TextBox>().Any(),"Sign-in section has no credential input fields");
        // Exercise the real handler rejection path without launching a browser.
        var tag=buttons[0].Tag; buttons[0].Tag="https://evil.com/";
        buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); buttons[0].Tag=tag;
        check(string.IsNullOrEmpty(window.FindControl<TextBlock>("StoreSignInStatus")!.Text),"Tampered shortcut tag is ignored by the real click handler");
        foreach(var width in new[]{1000,1280})
        {
            window.Width=width;window.Height=850;Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            check(list.Bounds.Width>0 && list.Bounds.Width<=window.FindControl<ScrollViewer>("StoreSignInScroll")!.Bounds.Width,"Sign-in section fits window width " + width);
            if(screenshots && width==1280)
            {using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(AppContext.BaseDirectory,"theme-screenshots","Store-Sign-In.png"),new Avalonia.Media.Imaging.PngBitmapEncoderOptions());}
        }
        window.FindControl<Button>("NavDiscover")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(!window.FindControl<ScrollViewer>("StoreSignInScroll")!.IsVisible && window.FindControl<ScrollViewer>("DiscoveryScroll")!.IsVisible,"Leaving sign-in restores Discovery without overlapping pages");
        check(!Directory.Exists(Path.Combine(root,"signin-ui")),"Visiting sign-in does not write credentials or create a local library");
        if(screenshots)
        {
            window.FindControl<Button>("NavSettings")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs(); var settingsScroll = window.FindControl<ScrollViewer>("SettingsScroll")!; settingsScroll.Offset = new Avalonia.Vector(0, settingsScroll.Extent.Height);
            Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using(var frame=window.CaptureRenderedFrame()) frame!.Save(Path.Combine(AppContext.BaseDirectory,"theme-screenshots","Branding-About.png"),new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            window.FindControl<Button>("NavDiscover")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using(var frame=window.CaptureRenderedFrame()) frame!.Save(Path.Combine(AppContext.BaseDirectory,"theme-screenshots","Branding-Discover.png"),new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }
        window.AllowExit();window.Close();
    }
}



