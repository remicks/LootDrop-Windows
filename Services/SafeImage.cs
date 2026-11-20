using System.IO;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace LootDrop.Services;

public static class SafeImage
{
    public const int MaxBytes = 4 * 1024 * 1024;
    public static string BackgroundPath(string raw)
    {
        var root = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "LootDrop");
        var folder = Path.Combine(root, "Backgrounds");
        var full = Path.GetFullPath(raw);
        if (!string.Equals(Path.GetDirectoryName(full), folder, System.StringComparison.OrdinalIgnoreCase))
            throw new IOException("Re-select a local background through Choose background.");
        foreach (var path in new[] { root, folder, full })
            if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked background paths are not supported.");
        return full;
    }
    public static Bitmap Decode(byte[] bytes, int targetWidth)
    {
        var width = Validate(bytes);
        using var stream = new MemoryStream(bytes, writable: false);
        return Bitmap.DecodeToWidth(stream, System.Math.Min(targetWidth, width));
    }

    public static int Validate(byte[] bytes)
    {
        if (bytes.Length > MaxBytes) throw new IOException("Image exceeds 4 MiB.");
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec == null || codec.Info.Width <= 0 || codec.Info.Height <= 0 ||
            codec.Info.Width > 8192 || codec.Info.Height > 8192 ||
            (long)codec.Info.Width * codec.Info.Height > 16_000_000)
            throw new IOException("Unsupported image or excessive dimensions.");
        return codec.Info.Width;
    }
}
