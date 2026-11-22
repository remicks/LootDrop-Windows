using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace LootDrop.Build
{
    public sealed class EmbedIconResources : Task
    {
        [Required] public string AssemblyPath { get; set; }
        [Required] public string AssetDirectory { get; set; }
        private static readonly string[] Keys = { "purple", "blue", "cyan", "green", "red", "orange", "pink", "gold" };
        private delegate bool EnumName(IntPtr module, IntPtr type, IntPtr name, IntPtr parameter);
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] private static extern IntPtr LoadLibraryEx(string file, IntPtr reserved, uint flags);
        [DllImport("kernel32.dll")] private static extern bool FreeLibrary(IntPtr module);
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] private static extern bool EnumResourceNames(IntPtr module, IntPtr type, EnumName callback, IntPtr parameter);
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] private static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);
        [DllImport("kernel32.dll")] private static extern IntPtr LoadResource(IntPtr module, IntPtr resource);
        [DllImport("kernel32.dll")] private static extern IntPtr LockResource(IntPtr resource);
        [DllImport("kernel32.dll")] private static extern uint SizeofResource(IntPtr module, IntPtr resource);
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] private static extern IntPtr BeginUpdateResource(string file, bool deleteExisting);
        [DllImport("kernel32.dll", SetLastError=true)] private static extern bool UpdateResource(IntPtr update, IntPtr type, IntPtr name, ushort language, byte[] bytes, uint size);
        [DllImport("kernel32.dll", SetLastError=true)] private static extern bool EndUpdateResource(IntPtr update, bool discard);
        private static byte[] Read(IntPtr module, int type, int id)
        {
            var resource=FindResource(module,(IntPtr)id,(IntPtr)type);
            if(resource==IntPtr.Zero) throw new InvalidDataException("Required native resource missing: " + type + "/" + id);
            var bytes=new byte[checked((int)SizeofResource(module,resource))];
            Marshal.Copy(LockResource(LoadResource(module,resource)),bytes,0,bytes.Length); return bytes;
        }
        private static List<int> Groups(IntPtr module)
        {
            var names=new List<int>();var unsupported=false;
            EnumName callback=(m,t,n,p)=> { var id=n.ToInt64(); if(id<1 || id>65535) {unsupported=true;return false;} names.Add((int)id);return true; };
            if(!EnumResourceNames(module,(IntPtr)14,callback,IntPtr.Zero) || unsupported) throw new InvalidDataException("Icon resources missing.");
            return names;
        }
        public override bool Execute()
        {
            try
            {
                // Modify only the intermediate managed assembly, before apphost creation and single-file bundling.
                // Never rewrite the finished bundled EXE (that could destroy its appended runtime payload).
                var file=Path.GetFullPath(AssemblyPath);
                if(Path.GetExtension(file)!=".dll" || file.IndexOf(Path.DirectorySeparatorChar+"obj"+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)<0)
                    throw new InvalidDataException("Expected an intermediate assembly under obj.");
                var module=LoadLibraryEx(file,IntPtr.Zero,0x22);
                if(module==IntPtr.Zero) throw new InvalidOperationException("Cannot inspect intermediate resources.");
                byte[] manifest,version; var oldImages=new HashSet<int>();
                try
                {
                    manifest=Read(module,24,1);version=Read(module,16,1);
                    foreach(var groupId in Groups(module))
                    {
                        if(groupId<32512 || groupId>32519) throw new InvalidDataException("Unexpected icon group; refusing to overwrite.");
                        var group=Read(module,14,groupId);var count=BitConverter.ToUInt16(group,4);
                        if(group.Length!=6+14*count) throw new InvalidDataException("Invalid native icon group.");
                        for(var i=0;i<count;i++) oldImages.Add(BitConverter.ToUInt16(group,6+14*i+12));
                    }
                }
                finally { FreeLibrary(module); }
                var resources=new List<Tuple<int,int,byte[]>>();var imageId=1000;
                for(var keyIndex=0;keyIndex<Keys.Length;keyIndex++)
                {
                    var bytes=File.ReadAllBytes(Path.Combine(AssetDirectory,"lootdrop-"+Keys[keyIndex]+".ico"));
                    if(bytes.Length<6 || bytes.Length>512*1024 || BitConverter.ToUInt16(bytes,0)!=0 || BitConverter.ToUInt16(bytes,2)!=1)
                        throw new InvalidDataException("Unsupported icon input.");
                    var count=BitConverter.ToUInt16(bytes,4);if(count<1 || count>16 || bytes.Length<6+count*16)throw new InvalidDataException("Invalid icon directory.");
                    using(var stream=new MemoryStream()) using(var writer=new BinaryWriter(stream))
                    {
                        writer.Write((ushort)0);writer.Write((ushort)1);writer.Write(count);
                        for(var i=0;i<count;i++)
                        {
                            var entry=6+i*16;var size=BitConverter.ToUInt32(bytes,entry+8);var offset=BitConverter.ToUInt32(bytes,entry+12);
                            if(size==0 || offset<6+count*16 || (long)offset+size>bytes.Length)throw new InvalidDataException("Icon frame out of bounds.");
                            var frame=new byte[checked((int)size)];Buffer.BlockCopy(bytes,checked((int)offset),frame,0,frame.Length);
                            resources.Add(Tuple.Create(3,imageId,frame));writer.Write(bytes,entry,12);writer.Write((ushort)imageId++);
                        }
                        resources.Add(Tuple.Create(14,32512+keyIndex,stream.ToArray()));
                    }
                }
                var update=BeginUpdateResource(file,false);if(update==IntPtr.Zero) throw new InvalidOperationException("Cannot update intermediate icon resources.");
                try
                {
                    foreach(var id in oldImages) if(!UpdateResource(update,(IntPtr)3,(IntPtr)id,0,null,0))throw new InvalidOperationException("Cannot replace old icon frame.");
                    foreach(var entry in resources) if(!UpdateResource(update,(IntPtr)entry.Item1,(IntPtr)entry.Item2,0,entry.Item3,(uint)entry.Item3.Length))throw new InvalidOperationException("Cannot add icon resource.");
                    if(!EndUpdateResource(update,false)) throw new InvalidOperationException("Cannot finalize icons.");update=IntPtr.Zero;
                }
                finally { if(update!=IntPtr.Zero)EndUpdateResource(update,true); }
                module=LoadLibraryEx(file,IntPtr.Zero,0x22);if(module==IntPtr.Zero)throw new InvalidOperationException("Cannot verify icons.");
                try
                {
                    if(Groups(module).Count!=8 || !Read(module,24,1).SequenceEqual(manifest) || !Read(module,16,1).SequenceEqual(version))
                        throw new InvalidDataException("Native icon verification failed or manifest/version changed.");
                    foreach(var entry in resources) if(!Read(module,entry.Item1,entry.Item2).SequenceEqual(entry.Item3))throw new InvalidDataException("Native icon bytes differ.");
                }
                finally { FreeLibrary(module); }
                Log.LogMessage(MessageImportance.High,"Embedded and verified eight native icon groups; manifest/version preserved.");return true;
            }
            catch(Exception ex){Log.LogErrorFromException(ex,true);return false;}
        }
    }
}

