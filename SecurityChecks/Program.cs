using System.Net;
using LootDrop.Services;
using LootDrop.Models;
using SkiaSharp;

if (args.Contains("--native-sidebar")) { NativeDockChecks.Run(); return; }
var checks = 0;
void Check(bool value, string name)
{
    if (!value) throw new Exception("FAILED: " + name);
    checks++;
    Console.WriteLine("PASS " + name);
}
void Throws(Action action, string name)
{
    try { action(); } catch (Exception ex) when (ex is IOException or InvalidDataException) { Check(true, name); return; }
    throw new Exception("FAILED: " + name);
}
foreach (var url in new[] { "http://store.epicgames.com/p/a", "file:///C:/test.exe", "javascript:alert(1)",
    "steam://run/123", "https://user:password@store.epicgames.com/a", "https://localhost/a",
    "https://127.0.0.1/a", "https://[::1]/", "https://169.254.169.254/", "https://192.168.1.1/",
    "https://store.epicgames.com:444/a", "https://server.internal/a", "https://server.local/a",
    "https://store.epicgames.com/\r\nInjected", "https://store.epicgames.com\\@evil.com/" })
    Check(!UrlSafety.TryHttps(url, out _), "URL blocked: " + url.Replace("\r", "").Replace("\n", ""));
Check(UrlSafety.TryHttps("https://store.epicgames.com/en-US/p/game", out _), "Direct HTTPS store supported");
Check(UrlSafety.TryHttps("https://independent-store.com/game", out _), "Independent HTTPS store remains manually usable");
Check(!UrlSafety.TryImage("https://evil.com/image.png", out _), "Unknown automatic image host blocked");
Check(!UrlSafety.TryImage("https://cdn1.epicgames.com.evil.com/image.png", out _), "Image suffix spoof blocked");
Check(UrlSafety.TryImage("https://cdn1.epicgames.com/image.png", out _), "Known image CDN supported");
Check(!UrlSafety.IsDomain("fakegog.com", "gog.com"), "Domain substring spoof rejected");
var link = new ClaimLinkService();
Check((await link.CheckAsync("file:///C:/x.exe")).broken, "Unsafe claim check rejected without fetching");
Check(!(await link.CheckAsync("https://independent-store.com/game")).broken, "Safe claim remains unverified without fetching");
using var cancel = new CancellationTokenSource(); cancel.Cancel();
try { await link.CheckAsync("https://store.epicgames.com/", cancel.Token); throw new Exception("Cancellation ignored"); }
catch (OperationCanceledException) { Check(true, "Claim cancellation honored"); }

using var client = new HttpClient(new FakeHandler());
Check((await SafeHttp.GetBytesAsync(client, "https://fixture.example.org/ok", 4, default)).Length == 4, "Exact HTTP size boundary accepted");
try { await SafeHttp.GetBytesAsync(client, "https://fixture.example.org/oversize", 4, default); throw new Exception("Oversize accepted"); }
catch (IOException) { Check(true, "Declared oversized HTTP body blocked"); }
try { await SafeHttp.GetBytesAsync(client, "https://fixture.example.org/chunked", 4, default); throw new Exception("Chunked oversize accepted"); }
catch (IOException) { Check(true, "Streaming oversized HTTP body blocked without Content-Length"); }
try { await SafeHttp.GetBytesAsync(client, "https://fixture.example.org/redirect", 4, default); throw new Exception("Redirect accepted"); }
catch (HttpRequestException) { Check(true, "Redirect response not consumed as success"); }
using var shortDeadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
try { await SafeHttp.GetBytesAsync(client, "https://fixture.example.org/stall", 4, shortDeadline.Token); throw new Exception("Stall accepted"); }
catch (OperationCanceledException) { Check(true, "Streaming body cancellation enforced"); }

var verifier = new StoreVerificationService();
var spoof = new GameOffer { Title = "Fake", Platforms = "Steam", ClaimUrl = "https://evil.com/store.steampowered.com/app/123/" };
Check((await verifier.VerifyAsync(spoof, default)).result == VerificationResult.Unverified, "Steam URL embedded in attacker path cannot verify");
var titleSpoof = new GameOffer { Title = "Fake", Platforms = "Epic Games", ClaimUrl = "https://evil.com/game" };
Check((await verifier.VerifyAsync(titleSpoof, default)).result == VerificationResult.Unverified, "Epic platform label cannot verify unrelated URL");
Check(!new GameOffer { ClaimUrl = "file:///C:/test.exe" }.ClaimButtonEnabled, "Model blocks unsafe claim before background checks");

Throws(() => SafeImage.Validate(new byte[SafeImage.MaxBytes + 1]), "Encoded image size enforced");
Throws(() => SafeImage.Validate(new byte[] { 1, 2, 3 }), "Invalid image rejected");
Throws(() => SafeImage.BackgroundPath(@"\\untrusted-server\share\wallpaper.png"), "UNC wallpaper blocked before file access");
Throws(() => SafeImage.BackgroundPath(Path.Combine(Path.GetTempPath(), "wallpaper.png")), "Wallpaper outside managed folder rejected");
using (var bitmap = new SKBitmap(10, 10))
using (var image = SKImage.FromBitmap(bitmap))
using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
    Check(SafeImage.Validate(data.ToArray()) == 10, "Valid PNG header accepted");
using (var bitmap = new SKBitmap(9000, 1))
using (var image = SKImage.FromBitmap(bitmap))
using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
    Throws(() => SafeImage.Validate(data.ToArray()), "Excessive image width rejected before Bitmap decode");

var temp = Path.Combine(AppContext.BaseDirectory, "fixtures-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
var libraryPath = Path.Combine(temp, "library.json");
var library = new LocalLibraryService(temp);
Check(library.Load().Games.Count == 0, "Missing library loads defaults");
library.Save(new LibraryState { Games = new() { new SavedGame { Id = 1, Title = "Fixture", IsClaimed = true } } });
Check(new LocalLibraryService(temp).Load().Games.Single().Title == "Fixture", "Library round-trip preserves games");
Check(!Directory.GetFiles(temp, "*.tmp").Any(), "Atomic write leaves no temporary file");
File.WriteAllText(libraryPath, "{bad-json");
var brokenLibrary = new LocalLibraryService(temp);
try { brokenLibrary.Load(); throw new Exception("Corrupt JSON accepted"); }
catch (System.Text.Json.JsonException) { Check(true, "Corrupt library reported"); }
Throws(() => brokenLibrary.Save(new LibraryState()), "Corrupt library cannot be overwritten by autosave");
Check(File.ReadAllText(libraryPath) == "{bad-json", "Original corrupt data preserved");
File.WriteAllText(libraryPath, "{\"Games\":null}");
Throws(() => new LocalLibraryService(temp).Load(), "Null library collection rejected");
File.WriteAllText(libraryPath, "{\"Games\":[{\"OriginalValue\":79228162514264337593543950335}]}");
Throws(() => new LocalLibraryService(temp).Load(), "Library values that could overflow statistics rejected");
File.WriteAllText(libraryPath, System.Text.Json.JsonSerializer.Serialize(new LibraryState { Games = new() { new SavedGame { Title = new string(' ', 4097) } } }));
Throws(() => new LocalLibraryService(temp).Load(), "Oversized library titles rejected before regex statistics");
using (var output = File.Create(libraryPath)) output.SetLength(8 * 1024 * 1024 + 1);
Throws(() => new LocalLibraryService(temp).Load(), "Oversized local library rejected");
try { await SafeHttp.GetBytesAsync(client, "http://fixture.example.org/ok", 4, default); throw new Exception("Unsafe central request accepted"); }
catch (HttpRequestException) { Check(true, "Central HTTP helper refuses non-HTTPS before dispatch"); }
var safeState = new LibraryState();
var themeLibrary = new LocalLibraryService(Path.Combine(temp, "theme-storage"));
themeLibrary.Save(safeState);
var beforeInvalidSave = File.ReadAllBytes(Path.Combine(temp, "theme-storage", "library.json"));
safeState.CustomThemes.Add(new CustomThemeRecord { Name = "Invalid", Hex = "not-a-color" });
Throws(() => themeLibrary.Save(safeState), "Invalid custom theme cannot be saved");
Check(beforeInvalidSave.SequenceEqual(File.ReadAllBytes(Path.Combine(temp, "theme-storage", "library.json"))), "Invalid theme save leaves original file unchanged");
File.WriteAllText(libraryPath, "{\"CustomThemes\":[{\"Name\":\"Broken\",\"Hex\":null}]}");
Throws(() => new LocalLibraryService(temp).Load(), "Null custom theme color rejected without UI failure");
DesktopDockChecks.RunCore(Check, temp);
ReliabilityChecks.RunCore(Check, temp);
OfferInterfaceChecks.RunCore(Check);
await UpdateChecks.RunCore(Check, temp);
ThemeChecks.Run(Check, temp, args.Contains("--theme-screenshots"));
// All fixtures are under this harness's own build directory; no real app data is used.
Console.WriteLine($"Completed {checks} security regression checks. No app launched, real links fetched, or startup registry changed.");

var iconPathIndex = Array.IndexOf(args,"--verify-published-icons");
if(iconPathIndex>=0) AppIconChecks.VerifyNative(Check,args[iconPathIndex+1]);

if (args.Contains("--live-updates")) await Task.Run(UpdateChecks.RunLive).ConfigureAwait(false);

if (args.Contains("--live-feeds"))
{
    // Explicit diagnostic mode: one request to each fixed provider endpoint; never claim links.
    foreach (var (name, fetch) in new (string, Func<CancellationToken, Task<IReadOnlyList<GameOffer>>>)[]
    {
        ("GamerPower", t => new GamerPowerService().GetActiveFreeGamesAsync(t)),
        ("Epic Games", t => new AdditionalGiveawayService().GetEpicGiveawaysAsync(t)),
        ("CheapShark", t => new AdditionalGiveawayService().GetCheapSharkFreeDealsAsync(t))
    })
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        try { var offers = await Task.Run(() => fetch(deadline.Token)).ConfigureAwait(false); Console.WriteLine($"LIVE {name}: parsed {offers.Count} offers (not checkout verification)"); }
        catch (Exception ex) { Console.WriteLine($"LIVE {name}: unavailable ({ex.GetType().Name}: {ex.Message})"); }
    }
}

if (args.Contains("--live-price-metadata"))
{
    // Explicit read-only integration check of fixed catalog endpoints; never opens claim pages.
    await Task.Run(async () =>
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var verifier = new StoreVerificationService();
        var steam = new GameOffer { Id = 570, Title = "Catalog metadata fixture", Platforms = "Steam", ClaimUrl = "https://store.steampowered.com/app/570/" };
        var steamResult = await verifier.VerifyAsync(steam, deadline.Token);
        Console.WriteLine($"LIVE PRICE Steam: {steamResult.result}; recorded catalog time: {steam.LastPriceCheckUtc:O}");
        try
        {
            var epic = (await new AdditionalGiveawayService().GetEpicGiveawaysAsync(deadline.Token)).FirstOrDefault();
            if (epic == null) { Console.WriteLine("LIVE PRICE Epic: no current offer to exercise metadata."); return; }
            var first = await verifier.VerifyAsync(epic, deadline.Token);
            var stamp = epic.LastPriceCheckUtc;
            await verifier.VerifyAsync(epic, deadline.Token);
            Console.WriteLine($"LIVE PRICE Epic: {first.result}; recorded catalog time: {stamp:O}; cached reuse preserves time: {stamp.HasValue && stamp == epic.LastPriceCheckUtc}");
        }
        catch (Exception ex) { Console.WriteLine("LIVE PRICE Epic: unavailable (" + ex.GetType().Name + ")"); }
    }).ConfigureAwait(false);
}

sealed class FakeHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path == "/redirect") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect));
        var content = path is "/chunked" or "/stall"
            ? (HttpContent)new StreamContent(new FixtureStream(path == "/stall"))
            : new ByteArrayContent(new byte[path == "/oversize" ? 5 : 4]);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }
}
sealed class FixtureStream(bool stall) : Stream
{
    int remaining = 5;
    public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
    {
        if (stall) await Task.Delay(Timeout.Infinite, token);
        if (remaining == 0) return 0;
        buffer.Span[0] = 0; remaining--; return 1;
    }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}




