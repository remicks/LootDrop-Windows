using System.Threading;
using System.Threading.Tasks;
namespace LootDrop.Services;
public sealed class ClaimLinkService
{
    // Never crawl feed-supplied destinations or redirects.
    public Task<(bool broken, string note)> CheckAsync(string url, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        return Task.FromResult(UrlSafety.TryHttps(url, out _)
            ? (false, "HTTPS link · Reachability and checkout not checked")
            : (true, "Unsafe or unsupported claim URL · Opening blocked"));
    }
}