using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LootDrop.Services;

public sealed record UpdateAsset(string Name, string Url, long Size, string Sha256);
public sealed record UpdateRelease(Version Version, string Tag, bool Prerelease, string Notes, string PageUrl, IReadOnlyList<UpdateAsset> Assets);
public sealed class UpdateService
{
    public const string Repository = "remicks/LootDrop-Windows";
    public const string ReleasesUrl = "https://github.com/" + Repository + "/releases";
    public const string ApiUrl = "https://api.github.com/repos/" + Repository + "/releases?per_page=100";
    public const long MaxDownloadBytes = 256L * 1024 * 1024;
    public static Version CurrentVersion => typeof(UpdateService).Assembly.GetName().Version ?? new Version(0,0,0,0);
    public static string CurrentVersionText => CurrentVersion.ToString(3);
    private static readonly HttpClient DefaultHttp = CreateHttp();
    private readonly HttpClient _http;
    public UpdateService(HttpClient? http = null) => _http = http ?? DefaultHttp;
    private static HttpClient CreateHttp()
    {
        var http = SafeHttp.CreateClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("LootDrop-Windows/" + CurrentVersionText);
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }
    public async Task<UpdateRelease?> CheckAsync(bool includePrereleases, CancellationToken token)
    {
        var bytes = await SafeHttp.GetBytesAsync(_http, ApiUrl, 2 * 1024 * 1024, token);
        return SelectRelease(bytes, includePrereleases);
    }
    public static bool IsNewer(UpdateRelease release, Version current) => Normalize(release.Version) > Normalize(current);
    private static Version Normalize(Version v) => new(v.Major,v.Minor,Math.Max(0,v.Build),Math.Max(0,v.Revision));
    public static UpdateRelease? SelectRelease(byte[] bytes, bool includePrereleases)
    {
        if(bytes.Length > 2 * 1024 * 1024) throw new InvalidDataException("Update metadata is too large.");
        using var doc=JsonDocument.Parse(bytes);
        if(doc.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Invalid release response.");
        var candidates=new List<UpdateRelease>();
        foreach(var item in doc.RootElement.EnumerateArray().Take(100))
        {
            if(item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("draft",out var draft) || draft.ValueKind != JsonValueKind.False ||
                !item.TryGetProperty("prerelease",out var pre) || pre.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                (!includePrereleases && pre.GetBoolean())) continue;
            var tag=Text(item,"tag_name");
            if(!Regex.IsMatch(tag,@"^v?[0-9]{1,6}\.[0-9]{1,6}\.[0-9]{1,6}(\.[0-9]{1,6})?$",RegexOptions.CultureInvariant) ||
                !Version.TryParse(tag.TrimStart('v'),out var version)) continue;
            var page=ReleasesUrl + "/tag/" + tag;
            if(Text(item,"html_url") != page) continue;
            var assets=new List<UpdateAsset>();
            if(item.TryGetProperty("assets",out var all) && all.ValueKind==JsonValueKind.Array)
            foreach(var asset in all.EnumerateArray().Take(100))
            {
                if(asset.ValueKind!=JsonValueKind.Object) continue;
                var name=Text(asset,"name");var url=Text(asset,"browser_download_url");var digest=Text(asset,"digest");
                if(Text(asset,"state")!="uploaded" || !asset.TryGetProperty("size",out var size) || !size.TryGetInt64(out var length) ||
                    !ValidAsset(tag,version!,name,url,length,digest)) continue;
                assets.Add(new(name,url,length,digest[7..].ToLowerInvariant()));
            }
            // Do not silently fall back to an older release when the newest release lacks valid packages.
            var notes=Text(item,"body"); if(notes.Length>16000) notes=notes[..16000]+"\n[Notes shortened; see release page.]";
            candidates.Add(new(version!,tag,pre.GetBoolean(),notes,page,assets.GroupBy(a=>a.Name).Where(g=>g.Count()==1).Select(g=>g.First()).ToArray()));
        }
        return candidates.OrderByDescending(r=>Normalize(r.Version)).FirstOrDefault();
    }
    private static string Text(JsonElement item,string name) => item.TryGetProperty(name,out var value) && value.ValueKind==JsonValueKind.String ? value.GetString() ?? "" : "";
    private static bool ValidAsset(string tag,Version version,string name,string url,long size,string digest)
    {
        var number=tag.TrimStart('v');
        return (name=="LootDrop-v2-"+number+"-Setup.exe" || name=="LootDrop-v2-"+number+"-Portable-win-x64.zip") &&
            url=="https://github.com/"+Repository+"/releases/download/"+tag+"/"+name &&
            size>0 && size<=MaxDownloadBytes && Regex.IsMatch(digest,@"^sha256:[a-fA-F0-9]{64}$",RegexOptions.CultureInvariant);
    }
    public async Task<string> DownloadAsync(UpdateRelease release,UpdateAsset asset,string folder,CancellationToken token,IProgress<int>? progress=null)
    {
        if(!Regex.IsMatch(release.Tag,@"^v?[0-9]{1,6}\.[0-9]{1,6}\.[0-9]{1,6}(\.[0-9]{1,6})?$",RegexOptions.CultureInvariant) ||
            !Version.TryParse(release.Tag.TrimStart('v'),out var parsed) || Normalize(parsed)!=Normalize(release.Version) ||
            !release.Assets.Contains(asset) || !ValidAsset(release.Tag,release.Version,asset.Name,asset.Url,asset.Size,"sha256:"+asset.Sha256))
            throw new InvalidDataException("Unsupported release download.");
        var fullFolder=Path.GetFullPath(folder);
        if(!Path.IsPathFullyQualified(folder) || fullFolder.StartsWith(@"\\",StringComparison.Ordinal) || fullFolder.StartsWith("//",StringComparison.Ordinal))
            throw new IOException("Choose a local drive folder, not a network or device path.");
        if(!Directory.Exists(folder)) throw new IOException("Choose an existing local folder.");
        var target=Path.Combine(Path.GetFullPath(folder),asset.Name);
        SafeLocalFiles.Check(folder,target);
        if(File.Exists(target)) throw new IOException("This package already exists in that folder. Choose another folder.");
        var temporary=target+"."+Guid.NewGuid().ToString("N")+".partial";
        var created=false;
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromMinutes(10));
        try
        {
            var uri=new Uri(asset.Url);
            for(var hop=0;hop<4;hop++)
            {
                if(!ValidDownloadHost(uri)) throw new InvalidDataException("Untrusted download redirect blocked.");
                using var response=await _http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
                if((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
                {
                    var location=response.Headers.Location ?? throw new InvalidDataException("Missing download redirect.");
                    uri=location.IsAbsoluteUri?location:new Uri(uri,location);continue;
                }
                response.EnsureSuccessStatusCode();
                if(response.Content.Headers.ContentLength is long declared && declared!=asset.Size) throw new InvalidDataException("Unexpected package size.");
                await using var input=await response.Content.ReadAsStreamAsync(deadline.Token);
                using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                long received=0;
                await using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true))
                {
                    created=true;var buffer=new byte[65536];int count;var percent=-1;
                    while((count=await input.ReadAsync(buffer,deadline.Token))!=0)
                    {
                        received+=count;if(received>asset.Size) throw new InvalidDataException("Package exceeded expected size.");
                        hash.AppendData(buffer,0,count);await output.WriteAsync(buffer.AsMemory(0,count),deadline.Token);
                        var next=(int)(received*100/asset.Size);if(next!=percent){percent=next;progress?.Report(percent);}
                    }
                    await output.FlushAsync(deadline.Token);output.Flush(true);
                }
                if(received!=asset.Size || !Convert.ToHexString(hash.GetHashAndReset()).Equals(asset.Sha256,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Package integrity check failed. Nothing was installed.");
                deadline.Token.ThrowIfCancellationRequested();SafeLocalFiles.Check(folder,target);
                File.Move(temporary,target,overwrite:false);created=false;return target;
            }
            throw new InvalidDataException("Too many download redirects.");
        }
        finally { if(created && File.Exists(temporary)) File.Delete(temporary); }
    }
    private static bool ValidDownloadHost(Uri uri) => uri.Scheme=="https" && uri.Port==443 && uri.UserInfo.Length==0 && uri.Fragment.Length==0 &&
        (uri.IdnHost=="github.com" || uri.IdnHost=="release-assets.githubusercontent.com");
}
