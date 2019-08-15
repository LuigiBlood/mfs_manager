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

        public void Save(string filepath)
        {
            byte[] volume = RAMVolume.SaveToArray();

            Array.Copy(volume, 0, Data, OffsetToMFSRAM, volume.Length);
            Array.Copy(volume, 0, Data, OffsetToMFSRAM + volume.Length, volume.Length);

            FileStream file = new FileStream(filepath, FileMode.Create);
            file.Write(Data, 0, Data.Length);
            file.Close();
        }

        // Additional Methods
        public byte[] GetFileData(MFSFile file)
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

        public MFSDirectory GetDirectoryFromID(ushort id)
        {
            foreach (MFSEntry entry in RAMVolume.Entries)
            {
                if (entry.GetType() == typeof(MFSDirectory) && ((MFSDirectory)entry).DirectoryID == id)
                {
                    return (MFSDirectory)entry;
                }
            }
            return null;
        }

        public string GetFilePath(MFSFile file)
        {
            return GetDirectoryPath(file) + file.Name + "." + file.Ext;
        }

        public string GetDirectoryPath(MFSFile file)
        {
            string temp = "";
            ushort dir_id = 0;
            MFSDirectory dir = GetDirectoryFromID(file.ParentDirectory);
            do
            {
                dir_id = dir.ParentDirectory;
                temp = dir.Name + (dir_id != 0xFFFE ? "/" : "") + temp;
                dir = GetDirectoryFromID(dir_id);
            }
            while (dir_id != 0xFFFE);

            return temp;
        }

        public int GetTotalUsedSize()
        {
            int totalsize = 0;
            for (int i = 6; i < Leo.SIZE_LBA - Leo.RamStartLBA[RAMVolume.DiskType]; i++)
            {
                switch (RAMVolume.FAT[i])
                {
                    case (ushort)MFS.FAT.Unused:
                    case (ushort)MFS.FAT.Prohibited:
                    case (ushort)MFS.FAT.DontManage:
                        break;
                    default:
                        totalsize += Leo.LBAToByte(RAMVolume.DiskType, Leo.RamStartLBA[RAMVolume.DiskType] + i, 1);
                        break;
                }
            }
            return totalsize;
        }

        public int GetFreeSpace()
        {
            int unused = Leo.RamSize[RAMVolume.DiskType] - GetTotalUsedSize() - Leo.LBAToByte(RAMVolume.DiskType, Leo.RamStartLBA[RAMVolume.DiskType], 6);

            return unused;
        }

        public int[] FindEntriesByName(string _name)
        {
            List<int> ids = new List<int>();

            for (int i = 0; i < RAMVolume.Entries.Count; i++)
            {
                if (RAMVolume.Entries[i].Name.Equals(_name))
                {
                    ids.Add(i);
                }
            }

            return ids.ToArray();
        }

        public bool CheckFileAlreadyExists(string _filename)
        {
            string _name = Path.GetFileNameWithoutExtension(_filename);
            string _ext = Path.GetExtension(_filename).Substring(1);

            return CheckFileAlreadyExists(_name, _ext);
        }

        public bool CheckFileAlreadyExists(string _name, string _ext)
        {
            foreach (MFSEntry entry in RAMVolume.Entries)
            {
                if (entry.GetType() == typeof(MFSFile))
                {
                    if (((MFSFile)entry).Name.Equals(_name) && ((MFSFile)entry).Ext.Equals(_ext))
                        return true;
                }
            }
            return false;
        }

        public bool CheckDirectoryAlreadyExists(string _name)
        {
            foreach (MFSEntry entry in RAMVolume.Entries)
            {
                if (entry.GetType() == typeof(MFSDirectory))
                {
                    if (((MFSDirectory)entry).Name.Equals(_name))
                        return true;
                }
            }
            return false;
        }

        //File Management
        public bool InsertFile(byte[] filedata, string name, ushort dir = 0)
        {
            string _name = Path.GetFileNameWithoutExtension(name);
            string _ext = Path.GetExtension(name).Substring(1);

            MFSFile file = new MFSFile(_name, RAMVolume.Entries[0].CompanyCode, RAMVolume.Entries[0].GameCode, _ext, (uint)filedata.Length, dir);
            return InsertFile(filedata, file);
        }

        public bool InsertFile(byte[] filedata, MFSFile file)
        {
            if (CheckFileAlreadyExists(file.Name, file.Ext))
                return false;
            if (GetFreeSpace() < filedata.Length)
                return false;

            int FATentry = -1;
            int lastFATentry = -1;
            int offset = 0;
            for (int i = 6; i < Leo.SIZE_LBA - Leo.RamStartLBA[RAMVolume.DiskType]; i++)
            {
                if (RAMVolume.FAT[i] == (ushort)MFS.FAT.Unused)
                {
                    if (FATentry == -1)
                    {
                        FATentry = i;
                        file.FATEntry = (ushort)i;
                    }
                    //Write File Data
                    Array.Copy(filedata, offset, Data, OffsetToMFSRAM + Leo.LBAToByte(RAMVolume.DiskType, Leo.RamStartLBA[RAMVolume.DiskType], i), Math.Min(Leo.LBAToByte(RAMVolume.DiskType, Leo.RamStartLBA[RAMVolume.DiskType] + i, 1), filedata.Length - offset));
                    offset += Math.Min(Leo.LBAToByte(RAMVolume.DiskType, Leo.RamStartLBA[RAMVolume.DiskType] + i, 1), filedata.Length - offset);
                    if (lastFATentry != -1)
                    {
                        RAMVolume.FAT[lastFATentry] = (ushort)i;
                    }
                    if (offset == filedata.Length)
                    {
                        RAMVolume.FAT[i] = (ushort)MFS.FAT.LastFileBlock;

                        RAMVolume.Entries.Add(file);
                        return true;
                    }
                    lastFATentry = i;
                }
            }
            return false;
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
            Load(Data, Offset);
        }

        public void Load(byte[] Data, int Offset)
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

        public byte[] SaveToArray()
        {
            byte[] temp = new byte[Leo.LBAToByte(DiskType, Leo.RamStartLBA[DiskType], 3)];

            Util.WriteStringN(MFS.RAM_ID, temp, 0, MFS.RAM_ID.Length);
            temp[0x0E] = (byte)(0 | (Attributes.isVolumeWriteProtected ? 0x20 : 0) | (Attributes.isVolumeReadProtected ? 0x40 : 0) | (Attributes.isWriteProtected ? 0x80 : 0));
            temp[0x0F] = (byte)DiskType;

            Util.WriteStringN(Name, temp, 0x10, 0x14);
            Util.WriteBEU32(Date.Save(), temp, 0x24);
            Util.WriteBEU16(Renewal, temp, 0x28);
            temp[0x2A] = Country;

            //Save FAT Entries
            for (int i = 0; i < FAT.Length; i++)
            {
                Util.WriteBEU16(FAT[i], temp, 0x3C + (i * 2));
            }

            //Save File Entries 0x16B0
            for (int i = 0; i < Entries.Count; i++)
            {
                byte[] tempentry;
                if (Entries[i].GetType() == typeof(MFSDirectory))
                    tempentry = ((MFSDirectory)Entries[i]).Save();
                else
                    tempentry = ((MFSFile)Entries[i]).Save();
                Array.Copy(tempentry, 0, temp, 0x16B0 + (0x30 * i), 0x30);
            }

            //Checksum
            uint crc = 0;
            for (int i = 0; i < (temp.Length / 4); i++)
            {
                crc ^= Util.ReadBEU32(temp, i * 4);
            }
            Util.WriteBEU32(crc, temp, 0x2C);

            return temp;
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

        public MFSEntry(string _name, string _ccode, string _gcode, ushort _dir = 0)
        {
            Attributes.CopyLimit = false;
            Attributes.Encode = false;
            Attributes.Hidden = false;
            Attributes.DisableRead = false;
            Attributes.DisableWrite = false;
            Name = _name;
            CompanyCode = _ccode;
            GameCode = _gcode;
            ParentDirectory = _dir;
            Renewal = 0;
            Date = new MFS.Date();
        }

        public MFSEntry(byte[] Data, int Offset)
        {
            LoadEntry(Data, Offset);
        }

        public void LoadEntry(byte[] Data, int Offset)
        {
            Attributes.CopyLimit = (Util.ReadBEU16(Data, Offset) & 0x0200) != 0;
            Attributes.Encode = (Util.ReadBEU16(Data, Offset) & 0x0400) != 0;
            Attributes.Hidden = (Util.ReadBEU16(Data, Offset) & 0x0800) != 0;
            Attributes.DisableRead = (Util.ReadBEU16(Data, Offset) & 0x1000) != 0;
            Attributes.DisableWrite = (Util.ReadBEU16(Data, Offset) & 0x2000) != 0;

            ParentDirectory = Util.ReadBEU16(Data, Offset + 0x02);
            CompanyCode = Util.ReadStringN(Data, Offset + 0x04, 2);
            GameCode = Util.ReadStringN(Data, Offset + 0x06, 4);

            Name = Util.ReadStringN(Data, Offset + 0x10, 0x14);

            Renewal = Data[Offset + 0x2A];
            Date = new MFS.Date(Util.ReadBEU32(Data, Offset + 0x2C));
        }

        public byte[] SaveEntry()
        {
            byte[] temp = new byte[0x30];

            //Attributes
            temp[0] = (byte)(0 | (Attributes.CopyLimit ? 0x02 : 0) | (Attributes.Encode ? 0x04 : 0) | (Attributes.Hidden ? 0x08 : 0)
                 | (Attributes.DisableRead ? 0x10 : 0) | (Attributes.DisableWrite ? 0x20 : 0));

            Util.WriteBEU16(ParentDirectory, temp, 2);
            Util.WriteStringN(CompanyCode, temp, 4, 2);
            Util.WriteStringN(GameCode, temp, 6, 4);
            Util.WriteStringN(Name, temp, 0x10, 0x14);

            temp[0x2A] = Renewal;
            Util.WriteBEU32(Date.Save(), temp, 0x2C);

            return temp;
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

        public byte[] Save()
        {
            byte[] temp = SaveEntry();

            Util.WriteBEU16(DirectoryID, temp, 0x0A);
            temp[0] |= 0x80;

            return temp;
        }
    }

    public class MFSFile : MFSEntry
    {
        public ushort FATEntry;
        public uint Size;

        public string Ext;
        public byte CopyNb;

        public MFSFile(string _name, string _ccode, string _gcode, string _ext, uint _size, ushort _dir = 0) : base(_name, _ccode, _gcode, _dir)
        {
            Ext = _ext;
            Size = _size;
            FATEntry = 0xFFFF;
            CopyNb = 0;
        }

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

        public byte[] Save()
        {
            byte[] temp = SaveEntry();

            Util.WriteBEU16(FATEntry, temp, 0x0A);
            Util.WriteBEU32(Size, temp, 0x0C);

            Util.WriteStringN(Ext, temp, 0x24, 5);
            temp[0x29] = CopyNb;
            temp[0] |= 0x40;
            return temp;
        }
    }
}
