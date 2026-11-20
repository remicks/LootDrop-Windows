using System;
using System.IO;

namespace LootDrop.Services;

public static class SafeLocalFiles
{
    public static void Check(params string[] paths)
    {
        foreach (var path in paths)
            if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked data paths are unsupported.");
    }
    public static byte[] Read(string path, int limit)
    {
        Check(path);
        using var input = File.OpenRead(path);
        if (input.Length > limit) throw new IOException("File exceeds size limit.");
        using var output = new MemoryStream();
        var buffer = new byte[16384];
        int count;
        while ((count = input.Read(buffer)) != 0)
        {
            if (output.Length + count > limit) throw new IOException("File exceeds size limit.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
    public static void WriteAtomic(string path, byte[] bytes)
    {
        var folder = Path.GetDirectoryName(path)!;
        Check(folder, path);
        Directory.CreateDirectory(folder);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(true); }
            Check(folder, path);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
