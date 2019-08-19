using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace mfs_manager
{
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

        public string CheckShiftJIS()
        {
            //Temporary Function for testing
            string temp = "";
            byte[] tempbytes = SJISUtil.EncodeStringToSJIS(Name);

            foreach (byte c in tempbytes)
            {
                temp += c.ToString("X2");
            }

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
