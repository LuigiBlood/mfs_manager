using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace mfs_manager
{
    public static class MFSRAMUtil
    {
        // Additional Methods
        public static byte[] GetFileData(MFSDisk mfsDisk, MFSFile file)
        {
            byte[] filedata = new byte[file.Size];

            ushort nextblock = file.FATEntry;
            uint offset = 0;
            uint size = file.Size;
            do
            {
                int blocksrc = Leo.LBAToByte(mfsDisk.RAMVolume.DiskType, Leo.RamStartLBA[mfsDisk.RAMVolume.DiskType], nextblock);
                int blocksize = Leo.LBAToByte(mfsDisk.RAMVolume.DiskType, Leo.RamStartLBA[mfsDisk.RAMVolume.DiskType] + nextblock, 1);

                Array.Copy(mfsDisk.Data, mfsDisk.OffsetToMFSRAM + blocksrc, filedata, offset, Math.Min(blocksize, size));
                offset += (uint)Math.Min(blocksize, size);
                size -= (uint)Math.Min(blocksize, size);

                nextblock = mfsDisk.RAMVolume.FAT[nextblock];
            }
            while (nextblock != (ushort)MFS.FAT.LastFileBlock);

            return filedata;
        }

        public static MFSDirectory GetDirectoryFromID(MFSDisk mfsDisk, ushort id)
        {
            foreach (MFSEntry entry in mfsDisk.RAMVolume.Entries)
            {
                if (entry.GetType() == typeof(MFSDirectory) && ((MFSDirectory)entry).DirectoryID == id)
                {
                    return (MFSDirectory)entry;
                }
            }
            return null;
        }

        public static string GetFullEntryPath(MFSDisk mfsDisk, MFSEntry file)
        {
            string temp = GetDirectoryEntryPath(mfsDisk, file) + file.Name;

            if (file.GetType() == typeof(MFSFile))
                temp += "." + ((MFSFile)file).Ext;

            return temp;
        }

        public static string GetDirectoryEntryPath(MFSDisk mfsDisk, MFSEntry file)
        {
            string temp = "";
            ushort dir_id = 0;
            MFSDirectory dir = GetDirectoryFromID(mfsDisk, file.ParentDirectory);
            while (dir != null)
            {
                dir_id = dir.ParentDirectory;
                temp = dir.Name + (dir_id != 0xFFFE ? "/" : "") + temp;
                dir = GetDirectoryFromID(mfsDisk, dir_id);
            }

            return temp;
        }

        public static int GetTotalUsedSize(MFSDisk mfsDisk)
        {
            int totalsize = 0;
            for (int i = 6; i < Leo.SIZE_LBA - Leo.RamStartLBA[mfsDisk.RAMVolume.DiskType]; i++)
            {
                switch (mfsDisk.RAMVolume.FAT[i])
                {
                    case (ushort)MFS.FAT.Unused:
                    case (ushort)MFS.FAT.Prohibited:
                    case (ushort)MFS.FAT.DontManage:
                        break;
                    default:
                        totalsize += Leo.LBAToByte(mfsDisk.RAMVolume.DiskType, Leo.RamStartLBA[mfsDisk.RAMVolume.DiskType] + i, 1);
                        break;
                }
            }
            return totalsize;
        }

        public static int GetFreeSpace(MFSDisk mfsDisk)
        {
            int unused = Leo.RamSize[mfsDisk.RAMVolume.DiskType] - GetTotalUsedSize(mfsDisk) - Leo.LBAToByte(mfsDisk.RAMVolume.DiskType, Leo.RamStartLBA[mfsDisk.RAMVolume.DiskType], 6);

            return unused;
        }

        public static int[] FindEntriesByName(MFSDisk mfsDisk, string _name)
        {
            List<int> ids = new List<int>();

            for (int i = 0; i < mfsDisk.RAMVolume.Entries.Count; i++)
            {
                if (mfsDisk.RAMVolume.Entries[i].Name.Equals(_name))
                {
                    ids.Add(i);
                }
            }

            return ids.ToArray();
        }

        public static bool CheckFileAlreadyExists(MFSDisk mfsDisk, string _filename, ushort _dir = 0xFFFF)
        {
            string _name = Path.GetFileNameWithoutExtension(_filename);
            string _ext = Path.GetExtension(_filename).Substring(1);

            return CheckFileAlreadyExists(mfsDisk, _name, _ext, _dir);
        }

        public static bool CheckFileAlreadyExists(MFSDisk mfsDisk, string _name, string _ext, ushort _dir = 0xFFFF)
        {
            foreach (MFSEntry entry in mfsDisk.RAMVolume.Entries)
            {
                if (entry.GetType() == typeof(MFSFile))
                {
                    if (((MFSFile)entry).Name.Equals(_name) && ((MFSFile)entry).Ext.Equals(_ext))
                    {
                        if (_dir == 0xFFFF)
                            return true;
                        else if (((MFSFile)entry).ParentDirectory == _dir)
                            return true;
                    }
                }
            }
            return false;
        }

        public static bool CheckDirectoryAlreadyExists(MFSDisk mfsDisk, string _name)
        {
            foreach (MFSEntry entry in mfsDisk.RAMVolume.Entries)
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
        public static bool InsertFile(MFSDisk mfsDisk, byte[] filedata, string name, ushort dir = 0)
        {
            string _name = Path.GetFileNameWithoutExtension(name);
            string _ext = Path.GetExtension(name).Substring(1);

            MFSFile file = new MFSFile(_name, mfsDisk.RAMVolume.Entries[0].CompanyCode, mfsDisk.RAMVolume.Entries[0].GameCode, _ext, (uint)filedata.Length, dir);
            return InsertFile(mfsDisk, filedata, file);
        }

        public static bool InsertFile(MFSDisk mfsDisk, byte[] filedata, MFSFile file)
        {
            if (CheckFileAlreadyExists(mfsDisk, file.Name, file.Ext, file.ParentDirectory))
                return false;
            if (GetFreeSpace(mfsDisk) < filedata.Length)
                return false;

            int FATentry = -1;
            int lastFATentry = -1;
            int offset = 0;
            for (int i = 6; i < Leo.SIZE_LBA - Leo.RamStartLBA[mfsDisk.RAMVolume.DiskType]; i++)
            {
                if (mfsDisk.RAMVolume.FAT[i] == (ushort)MFS.FAT.Unused)
                {
                    if (FATentry == -1)
                    {
                        FATentry = i;
                        file.FATEntry = (ushort)i;
                    }
                    //Write File Data
                    Array.Copy(filedata, offset, mfsDisk.Data, mfsDisk.OffsetToMFSRAM + Leo.LBAToByte(mfsDisk.RAMVolume.DiskType, Leo.RamStartLBA[mfsDisk.RAMVolume.DiskType], i), Math.Min(Leo.LBAToByte(mfsDisk.RAMVolume.DiskType, Leo.RamStartLBA[mfsDisk.RAMVolume.DiskType] + i, 1), filedata.Length - offset));
                    offset += Math.Min(Leo.LBAToByte(mfsDisk.RAMVolume.DiskType, Leo.RamStartLBA[mfsDisk.RAMVolume.DiskType] + i, 1), filedata.Length - offset);
                    if (lastFATentry != -1)
                    {
                        mfsDisk.RAMVolume.FAT[lastFATentry] = (ushort)i;
                    }
                    if (offset == filedata.Length)
                    {
                        mfsDisk.RAMVolume.FAT[i] = (ushort)MFS.FAT.LastFileBlock;

                        mfsDisk.RAMVolume.Entries.Add(file);
                        return true;
                    }
                    lastFATentry = i;
                }
            }
            return false;
        }
    }
}
