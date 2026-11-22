using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace LootDrop.Services;
public sealed record AppIconChoice(string Key,string Label,int ResourceIndex)
{
    public Bitmap Preview => AppIconCatalog.Preview(Key=="theme"?"purple":Key);
}
public static class AppIconCatalog
{
    public static IReadOnlyList<AppIconChoice> Choices { get; } = Array.AsReadOnly(new[] {
        new AppIconChoice("theme","Match app theme",0),
        new AppIconChoice("purple","Midnight Purple",0),new AppIconChoice("blue","Electric Blue",1),
        new AppIconChoice("cyan","Neon Cyan",2),new AppIconChoice("green","Emerald Green",3),
        new AppIconChoice("red","Crimson Red",4),new AppIconChoice("orange","Sunset Orange",5),
        new AppIconChoice("pink","Hot Pink",6),new AppIconChoice("gold","Royal Gold",7)
    });
    private static readonly Dictionary<string,Bitmap> Previews=new(StringComparer.Ordinal);
    public static bool IsValid(string? key)=>Choices.Any(c=>c.Key==key);
    public static AppIconChoice Resolve(string? preference,string themeKey)=>Choices.FirstOrDefault(c=>c.Key==(preference=="theme"?themeKey:preference) && c.Key!="theme") ?? Choices[1];
    public static byte[] Bytes(string key)
    {
        if(!Choices.Any(c=>c.Key==key && c.Key!="theme"))throw new InvalidDataException("Unknown embedded icon.");
        using var stream=AssetLoader.Open(new Uri("avares://LootDrop/Assets/lootdrop-"+key+".ico"));
        using var data=new MemoryStream();stream.CopyTo(data);return data.ToArray();
    }
    public static WindowIcon Load(string key) {using var stream=new MemoryStream(Bytes(key));return new WindowIcon(stream);}
    public static Bitmap Preview(string key)
    {
        if(Previews.TryGetValue(key,out var bitmap))return bitmap;
        var bytes=Bytes(key);var count=BitConverter.ToUInt16(bytes,4);
        for(var i=0;i<count;i++)
        {
            var entry=6+i*16;
            if(bytes[entry]!=32)continue;
            var size=checked((int)BitConverter.ToUInt32(bytes,entry+8));var offset=checked((int)BitConverter.ToUInt32(bytes,entry+12));
            if(offset<6+count*16 || size<8 || (long)offset+size>bytes.Length)throw new InvalidDataException("Invalid embedded icon frame.");
            using var stream=new MemoryStream(bytes,offset,size,false);bitmap=new Bitmap(stream);Previews.Add(key,bitmap);return bitmap;
        }
        throw new InvalidDataException("Embedded icon preview missing.");
    }
}
