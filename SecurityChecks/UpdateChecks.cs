using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LootDrop.Services;
using LootDrop.Views;

internal static class UpdateChecks
{
    private static readonly byte[] Payload=Encoding.UTF8.GetBytes("MZ synthetic inert package fixture — never executed");
    private static string Digest=>"sha256:"+Convert.ToHexString(SHA256.HashData(Payload)).ToLowerInvariant();
    public static object Release(string tag="v0.8.0",bool pre=false,bool draft=false,string? assetUrl=null,string? digest=null,long? size=null)
    {
        var name="LootDrop-v2-"+tag.TrimStart('v')+"-Setup.exe";
        return new {tag_name=tag,prerelease=pre,draft,html_url=UpdateService.ReleasesUrl+"/tag/"+tag,body="Synthetic release notes. No real installer execution.",assets=new[]{new{name,state="uploaded",size=size??Payload.Length,digest=digest??Digest,browser_download_url=assetUrl??("https://github.com/"+UpdateService.Repository+"/releases/download/"+tag+"/"+name)}}};
    }
    private static byte[] Json(params object[] releases)=>JsonSerializer.SerializeToUtf8Bytes(releases);
    private static UpdateRelease Parse(object release)=>UpdateService.SelectRelease(Json(release),true)!;
    public static async Task RunCore(Action<bool,string> check,string root)
    {
        check(UpdateService.SelectRelease(Json(),false)==null,"No release produces no available update");
        check(UpdateService.SelectRelease(Json(Release(pre:true)),false)==null,"Tester prereleases are excluded without opt-in");
        check(UpdateService.SelectRelease(Json(Release(pre:true)),true)!.Prerelease,"Tester prereleases require explicit opt-in");
        check(UpdateService.SelectRelease(Json(Release(draft:true)),true)==null,"Drafts are never offered");
        check(UpdateService.SelectRelease(Json(Release("v0.7.9"),Release("v0.7.10")),false)!.Tag=="v0.7.10","Version comparison is numeric, not lexical or response order");
        var release=Parse(Release());
        check(!UpdateService.IsNewer(release,new Version(0,8,0,0)) && !UpdateService.IsNewer(release,new Version(0,9,0)) && UpdateService.IsNewer(release,new Version(0,7,9)),"Equal, older and newer versions compare consistently across assembly revision forms");
        foreach(var bad in new[]{"v../evil","v0.8.0-rc1","v0.8","v999999999.8.0"})
            check(UpdateService.SelectRelease(Json(Release(bad)),true)==null,"Unsupported or unsafe release tag is ignored");
        foreach(var url in new[]{"https://evil.com/file.exe","http://github.com/remicks/LootDrop-Windows/releases/download/v0.8.0/a.exe","https://github.com.evil.com/a","https://user@github.com/a","https://github.com/other/repo/releases/download/v0.8.0/a.exe","file:///C:/bad.exe"})
            check(Parse(Release(assetUrl:url)).Assets.Count==0,"Foreign/unsafe update package URL is not downloadable");
        foreach(var digest in new[]{"","sha256:bad","sha1:"+new string('a',64)})
            check(Parse(Release(digest:digest)).Assets.Count==0,"Missing or malformed package digest disables download");
        check(Parse(Release(size:UpdateService.MaxDownloadBytes+1)).Assets.Count==0 && Parse(Release(size:0)).Assets.Count==0,"Zero and excessive package sizes are rejected");
        check(UpdateService.SelectRelease(Json(Release("v0.8.0"),Release("v0.9.0",digest:"bad")),false)!.Assets.Count==0,"Broken latest metadata never silently offers an older installer");
        var malformed=false;try{UpdateService.SelectRelease(Encoding.UTF8.GetBytes("{bad"),false);}catch(JsonException){malformed=true;}check(malformed,"Malformed update JSON fails closed");
        var folder=Path.Combine(root,"update-download");Directory.CreateDirectory(folder);
        async Task Rejected(Func<HttpRequestMessage,HttpResponseMessage> reply,string name)
        {
            var sub=Path.Combine(folder,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(sub);
            using var client=new HttpClient(new UpdateHandler(reply));var service=new UpdateService(client);var failed=false;
            try{await service.DownloadAsync(release,release.Assets[0],sub,default);}catch(Exception ex) when(ex is IOException or InvalidDataException or HttpRequestException){failed=true;}
            check(failed && Directory.GetFiles(sub).Length==0,name);
        }
        using(var client=new HttpClient(new UpdateHandler(_=>new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(Json(Release()))})))
            check((await new UpdateService(client).CheckAsync(false,default))!.Tag=="v0.8.0","Checker retrieves and parses only the fixed GitHub metadata endpoint");
        using(var guardClient=new HttpClient(new UpdateHandler(_=>throw new Exception("Untrusted path dispatched"))))
        {
            var blocked=false;try{await new UpdateService(guardClient).DownloadAsync(release,release.Assets[0],@"\\untrusted-server\share",default);}catch(IOException){blocked=true;}
            check(blocked,"UNC download folder is rejected before network filesystem access");
            blocked=false;try{await new UpdateService(guardClient).DownloadAsync(release with {Tag="../escape"},release.Assets[0],folder,default);}catch(InvalidDataException){blocked=true;}
            check(blocked,"Forged release record cannot bypass tag/path validation at download time");
        }
        var requests=new List<string>();
        using(var client=new HttpClient(new UpdateHandler(req=>
        {
            requests.Add(req.RequestUri!.AbsoluteUri);
            if(req.RequestUri.Host=="github.com") {var response=new HttpResponseMessage(HttpStatusCode.Found);response.Headers.Location=new Uri("https://release-assets.githubusercontent.com/inert-fixture?sig=fixture");return response;}
            return new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(Payload)};
        })))
        {
            var saved=await new UpdateService(client).DownloadAsync(release,release.Assets[0],folder,default);
            check(File.ReadAllBytes(saved).SequenceEqual(Payload) && requests.Count==2,"Trusted GitHub asset redirect saves bytes only after size and SHA256 match");
            var count=requests.Count;var rejected=false;try{await new UpdateService(client).DownloadAsync(release,release.Assets[0],folder,default);}catch(IOException){rejected=true;}
            check(rejected && requests.Count==count && File.ReadAllBytes(saved).SequenceEqual(Payload),"Existing downloads are not overwritten and no request is made");
        }
        foreach(var redirect in new[]{"https://evil.com/installer","http://release-assets.githubusercontent.com/file","https://release-assets.githubusercontent.com.evil.com/file","https://127.0.0.1/file","https://user@release-assets.githubusercontent.com/file"})
            await Rejected(_=>{var response=new HttpResponseMessage(HttpStatusCode.Found);response.Headers.Location=new Uri(redirect);return response;},"Unsafe update redirect blocked without saving a package");
        await Rejected(_=>{var response=new HttpResponseMessage(HttpStatusCode.Found);response.Headers.Location=new Uri("https://github.com/loop");return response;},"Redirect loops are bounded and leave no partial download");
        await Rejected(_=>new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(new byte[Payload.Length])},"SHA256 mismatch removes incomplete package");
        await Rejected(_=>new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(new byte[Payload.Length-1])},"Truncated package is rejected without leaving a file");
        await Rejected(_=>new HttpResponseMessage(HttpStatusCode.OK){Content=new UnknownLengthContent(new byte[Payload.Length+1])},"Chunked oversized package is stopped at the expected byte limit");
        await Rejected(_=>new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),"Server failure never creates a package");
        using(var cancel=new CancellationTokenSource())
        using(var client=new HttpClient(new UpdateHandler(_=>throw new Exception("Cancelled request dispatched"))))
        {cancel.Cancel();var cancelled=false;try{await new UpdateService(client).DownloadAsync(release,release.Assets[0],Path.Combine(folder,"cancel"),cancel.Token);}catch(OperationCanceledException){cancelled=true;}catch(IOException){Directory.CreateDirectory(Path.Combine(folder,"cancel"));try{await new UpdateService(client).DownloadAsync(release,release.Assets[0],Path.Combine(folder,"cancel"),cancel.Token);}catch(OperationCanceledException){cancelled=true;}}check(cancelled && Directory.GetFiles(Path.Combine(folder,"cancel")).Length==0,"Cancelled download cannot persist or execute a file");}
    }
    public static void RunWindow(Action<bool,string> check,string root,bool screenshots)
    {
        var requests=0;var fail=false;
        using var client=new HttpClient(new UpdateHandler(_=>{requests++;return new HttpResponseMessage(fail?HttpStatusCode.ServiceUnavailable:HttpStatusCode.OK){Content=new ByteArrayContent(Json(Release("v9.0.0",pre:true)))};}));
        var window=new MainWindow(new LocalLibraryService(Path.Combine(root,"updates-ui")),false,updateService:new UpdateService(client));window.Show();
        check(requests==0 && window.FindControl<CheckBox>("IncludePrereleasesCheck")!.IsChecked!=true,"No startup update request; prerelease channel is off by default");
        window.FindControl<CheckBox>("IncludePrereleasesCheck")!.IsChecked=true;
        window.CheckForUpdatesAsync().GetAwaiter().GetResult();Dispatcher.UIThread.RunJobs();
        check(requests==1 && window.FindControl<TextBlock>("UpdateStatusText")!.Text!.Contains("v9.0.0") && window.FindControl<Button>("DownloadUpdateButton")!.IsEnabled,"Real update UI shows a newer tester release and enables its validated download");
        window.FindControl<Button>("NavSettings")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
        window.FindControl<Border>("UpdatesPanel")!.BringIntoView();Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        if(screenshots){using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(AppContext.BaseDirectory,"theme-screenshots","Update-Checker.png"),new Avalonia.Media.Imaging.PngBitmapEncoderOptions());}
        window.FindControl<CheckBox>("IncludePrereleasesCheck")!.IsChecked=false;
        check(!window.FindControl<Button>("DownloadUpdateButton")!.IsEnabled && !window.FindControl<Button>("ReleaseNotesButton")!.IsEnabled,"Changing update channel clears stale actions");
        fail=true;window.CheckForUpdatesAsync().GetAwaiter().GetResult();Dispatcher.UIThread.RunJobs();
        check(!window.FindControl<Button>("DownloadUpdateButton")!.IsEnabled && window.FindControl<TextBlock>("UpdateStatusText")!.Text!.Contains("Could not check"),"Failed update check does not claim up-to-date or retain stale download");
        window.AllowExit();window.Close();
    }
    public static async Task RunLive()
    {
        var service=new UpdateService();
        var stable=await service.CheckAsync(false,default);
        Console.WriteLine("LIVE UPDATES stable: " + (stable?.Tag ?? "no published stable release"));
        var release=await service.CheckAsync(true,default) ?? throw new Exception("No live tester release found");
        if(!UpdateService.IsNewer(release,new Version(0,7,8)))throw new Exception("Older client did not detect live update");
        Console.WriteLine($"LIVE UPDATES tester: {release.Tag}; packages: {release.Assets.Count}; simulated 0.7.8 detects update: True; installed {UpdateService.CurrentVersionText} newer available: {UpdateService.IsNewer(release,UpdateService.CurrentVersion)}");
        var asset=release.Assets.Single(a=>a.Name.EndsWith("-Setup.exe",StringComparison.Ordinal));
        var folder=Path.Combine(AppContext.BaseDirectory,"live-update-download-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        var path=await service.DownloadAsync(release,asset,folder,default);
        using var input=File.OpenRead(path);var hash=Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
        if(hash!=asset.Sha256 || input.Length!=asset.Size)throw new Exception("Live download verification failed");
        Console.WriteLine($"LIVE DOWNLOAD verified: {asset.Name}; bytes: {input.Length}; SHA256: {hash}; executed: False");
    }
    private sealed class UpdateHandler(Func<HttpRequestMessage,HttpResponseMessage> reply):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){token.ThrowIfCancellationRequested();return Task.FromResult(reply(request));}}
    private sealed class UnknownLengthContent(byte[] bytes):HttpContent
    {protected override bool TryComputeLength(out long length){length=0;return false;}protected override Task SerializeToStreamAsync(Stream stream,TransportContext? context)=>stream.WriteAsync(bytes).AsTask();}
}


