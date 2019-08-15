using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace mfs_manager
{
    public static class MFS
    {
        //MFS Volume
        public enum DiskFormat
        {
            SDK,
            D64,
            RAM,
            Invalid
        }

        public struct VolumeAttr
        {
            public bool isWriteProtected;          //Filesystem is Write Protected
            public bool isVolumeWriteProtected;    //Cannot be written by other applications
            public bool isVolumeReadProtected;     //Cannot be read by other applications
        }

        public const string ROM_ID = "64dd-Multi0201";
        public const string RAM_ID = "64dd-Multi0101";

        //MFS FAT
        public enum FAT : ushort
        {
            Unused = 0x0000,
            DontManage = 0xFFFD,
            Prohibited = 0xFFFE,
            LastFileBlock = 0xFFFF
        }

        public const int FAT_MAX = 2874;

        //MFS Entry
        public struct EntryAttr
        {
            public bool CopyLimit;         //Limit Copy
            public bool Encode;            //Encode
            public bool Hidden;            //Hidden
            public bool DisableRead;       //Cannot be read by other applications
            public bool DisableWrite;      //Cannot be written, renamed, or deleted by other applications
        }

        public static readonly ushort[] EntryLimit = { 899, 814, 729, 644, 559, 474, 0 };

        public class Date
        {
            public uint Year;
            public uint Month;
            public uint Day;
            public uint Hour;
            public uint Minute;
            public uint Second;

            public Date()
            {
                Year = 1996;
                Month = 1;
                Day = 1;
                Hour = 0;
                Minute = 0;
                Second = 0;
            }

            public Date(uint data)
            {
                Load(data);
            }

            public void Load(uint data)
            {
                Year = (data >> 25) + 1996;
                Month = (data >> 21) & 0xF;
                Day = (data >> 16) & 0x1F;
                Hour = (data >> 11) & 0x1F;
                Minute = (data >> 5) & 0x3F;
                Second = ((data >> 0) & 0x1F) * 2;
            }

            public uint Save()
            {
                uint temp = 0;

                temp |= (Year - 1996) << 25;
                temp |= Month << 21;
                temp |= Day << 16;
                temp |= Hour << 11;
                temp |= Minute << 5;
                temp |= (Second / 2) << 0;

                return temp;
            }
        }
    }

    public class MFSDisk
    {
        public string Filename;
        public MFS.DiskFormat Format;
        public int OffsetToMFSRAM;
        public byte[] Data;
        public MFSRAMVolume RAMVolume;

        public MFSDisk(string filepath)
        {
            Load(filepath);
        }

        void Load(string filepath)
        {
            //Assume RAM format for now
            FileStream file = new FileStream(filepath, FileMode.Open);

            if (file.Length >= Leo.RamSize[5])
            {
                //Check Header
                file.Seek(0, SeekOrigin.Begin);
                byte[] test = new byte[MFS.RAM_ID.Length];
                file.Read(test, 0, test.Length);

                Console.WriteLine(Encoding.ASCII.GetString(test));

                if (Encoding.ASCII.GetString(test).Equals(MFS.RAM_ID))
                {
                    Format = MFS.DiskFormat.RAM;
                    file.Seek(15, SeekOrigin.Begin);
                    int DiskType = file.ReadByte();
                    if (DiskType >= 0 && DiskType < 6 && Leo.RamSize[DiskType] == file.Length)
                    {
                        file.Seek(0, SeekOrigin.Begin);

                        OffsetToMFSRAM = 0;
                        Data = new byte[file.Length];
                        file.Read(Data, 0, Data.Length);

                        RAMVolume = new MFSRAMVolume(Data, OffsetToMFSRAM);
                    }
                    else
                    {
                        Format = MFS.DiskFormat.Invalid;
                    }
                }
                else
                {
                    Format = MFS.DiskFormat.Invalid;
                }
            }

            file.Close();
        }

        public byte[] ReadFileData(MFSFile file)
        {
            byte[] filedata = new byte[file.Size];

            ushort nextblock = file.FATEntry;
            uint offset = 0;
            uint size = file.Size;
            do
            {
                int blocksrc = Leo.LBAToByte(RAMVolume.DiskType, Leo.RamStartLBA[RAMVolume.DiskType], nextblock);
                int blocksize = Leo.LBAToByte(RAMVolume.DiskType, Leo.RamStartLBA[RAMVolume.DiskType] + nextblock, 1);

                Array.Copy(Data, OffsetToMFSRAM + blocksrc, filedata, offset, Math.Min(blocksize, size));
                offset += (uint)Math.Min(blocksize, size);
                size -= (uint)Math.Min(blocksize, size);

                nextblock = RAMVolume.FAT[nextblock];
            }
            while (nextblock != (ushort)MFS.FAT.LastFileBlock);

            return filedata;
        }
    }

    public class MFSRAMVolume
    {
        public MFS.VolumeAttr Attributes;
        public byte DiskType;
        public string Name;
        public MFS.Date Date;
        public ushort Renewal;
        public byte Country;

        public ushort[] FAT;           //5748 / 2 = 2874 FAT entries
        public List<MFSEntry> Entries;

        public MFSRAMVolume(byte[] Data, int Offset)
        {
            Attributes.isWriteProtected = (Data[Offset + 0x0E] & 0x80) != 0;
            Attributes.isVolumeReadProtected = (Data[Offset + 0x0E] & 0x40) != 0;
            Attributes.isVolumeWriteProtected = (Data[Offset + 0x0E] & 0x20) != 0;
            DiskType = Data[Offset + 0x0F];
            Name = Util.ReadStringN(Data, Offset + 0x10, 0x14);
            Date = new MFS.Date(Util.ReadBEU32(Data, Offset + 0x24));
            Renewal = Util.ReadBEU16(Data, Offset + 0x28);
            Country = Data[Offset + 0x2A];

            //Get All FAT Entries
            FAT = new ushort[MFS.FAT_MAX];
            for (int i = 0; i < MFS.FAT_MAX; i++)
            {
                FAT[i] = Util.ReadBEU16(Data, Offset + 0x3C + (i * 2));
            }

            //Get All File Entries
            Entries = new List<MFSEntry>();
            for (int i = 0; i < MFS.EntryLimit[DiskType]; i++)
            {
                int check = (Data[Offset + 0x16B0 + (i * 0x30)] & 0xC0);
                if (check == 0x80)
                {
                    //Directory
                    MFSDirectory dir = new MFSDirectory(Data, Offset + 0x16B0 + (i * 0x30));
                    Entries.Add(dir);
                }
                else if (check == 0x40)
                {
                    //File
                    MFSFile file = new MFSFile(Data, Offset + 0x16B0 + (i * 0x30));
                    Entries.Add(file);
                }
            }
        }
    }

    public class MFSEntry
    {
        public MFS.EntryAttr Attributes;
        public ushort ParentDirectory;
        public string CompanyCode;
        public string GameCode;

        public string Name;

        public byte Renewal;
        public MFS.Date Date;

        public MFSEntry(byte[] Data, int Offset)
        {
            LoadEntry(Data, Offset);
        }

        public void LoadEntry(byte[] Data, int Offset)
        {
            Attributes.CopyLimit = (Util.ReadBEU16(Data, Offset) & 0x200) != 0;
            Attributes.Encode = (Util.ReadBEU16(Data, Offset) & 0x400) != 0;
            Attributes.Hidden = (Util.ReadBEU16(Data, Offset) & 0x800) != 0;
            Attributes.DisableRead = (Util.ReadBEU16(Data, Offset) & 0x1000) != 0;
            Attributes.DisableWrite = (Util.ReadBEU16(Data, Offset) & 0x2000) != 0;

            ParentDirectory = Util.ReadBEU16(Data, Offset + 0x02);
            CompanyCode = Util.ReadStringN(Data, Offset + 0x04, 2);
            GameCode = Util.ReadStringN(Data, Offset + 0x06, 4);

            Name = Util.ReadStringN(Data, Offset + 0x10, 0x14);

            Renewal = Data[Offset + 0x2A];
            Date = new MFS.Date(Util.ReadBEU32(Data, Offset + 0x2C));
        }
    }

    public class MFSDirectory : MFSEntry
    {
        public ushort DirectoryID;

        public MFSDirectory(byte[] Data, int Offset) : base(Data, Offset)
        {
            Load(Data, Offset);
        }

        public void Load(byte[] Data, int Offset)
        {
            DirectoryID = Util.ReadBEU16(Data, Offset + 0x0A);
        }
    }

    public class MFSFile : MFSEntry
    {
        public ushort FATEntry;
        public uint Size;

        public string Ext;
        public byte CopyNb;

        public MFSFile(byte[] Data, int Offset) : base(Data, Offset)
        {
            Load(Data, Offset);
        }

        public void Load(byte[] Data, int Offset)
        {
            FATEntry = Util.ReadBEU16(Data, Offset + 0x0A);
            Size = Util.ReadBEU32(Data, Offset + 0x0C);

            Ext = Util.ReadStringN(Data, Offset + 0x24, 5);
            CopyNb = Data[Offset + 0x29];
        }
    }
}
