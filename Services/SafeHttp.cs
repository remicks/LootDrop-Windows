using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LootDrop.Services;

public static class SafeHttp
{
    public static HttpClient CreateClient() => new(new HttpClientHandler
    {
        AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false,
        AutomaticDecompression = DecompressionMethods.None
    }) { Timeout = TimeSpan.FromSeconds(20), MaxResponseContentBufferSize = 8 * 1024 * 1024 };

    public static async Task<byte[]> GetBytesAsync(HttpClient client, string url, int limit,
        CancellationToken token)
    {
        if (!UrlSafety.TryHttps(url, out _)) throw new HttpRequestException("Unsafe or unsupported HTTPS URL.");
        if (limit <= 0 || limit > 8 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(limit));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > limit) throw new IOException("Response exceeds size limit.");
        await using var input = await response.Content.ReadAsStreamAsync(deadline.Token);
        return await ReadLimitedAsync(input, limit, deadline.Token);
    }

    public static async Task<byte[]> ReadLimitedAsync(Stream input, int limit, CancellationToken token)
    {
        using var output = new MemoryStream();
        var buffer = new byte[16384];
        int count;
        while ((count = await input.ReadAsync(buffer.AsMemory(), token)) != 0)
        {
            if (output.Length + count > limit) throw new IOException("Data exceeds size limit.");
            await output.WriteAsync(buffer.AsMemory(0, count), token);
        }
        return output.ToArray();
    }
}
