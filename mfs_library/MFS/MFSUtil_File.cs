﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace mfs_library
{
    public static partial class MFSRAMUtil
    {
        // Additional Methods
        public static byte[] ReadFile(MFSDisk mfsDisk, string filepath)
        {
            MFSFile file = GetFileFromPath(mfsDisk, filepath);
            if (file != null)
                return ReadFile(mfsDisk, file);
            else
                return null;
        }
        
        public static byte[] ReadFile(MFSDisk mfsDisk, MFSFile file)
        {
            byte[] filedata = new byte[file.Size];

            //Add FAT Entries and Copy to data to blocks
            ushort nextblock = file.FATEntry;
            uint offset = 0;
            uint size = file.Size;

            //Recursively copy blocks
            do
            {
                byte[] blockdata = mfsDisk.Disk.ReadLBA(Leo.RamStartLBA[mfsDisk.RAMVolume.DiskType] + nextblock);
                int blocksize = blockdata.Length;
                Array.Copy(blockdata, 0, filedata, offset, Math.Min(blocksize, size));
                offset += (uint)Math.Min(blocksize, size);
                size -= (uint)Math.Min(blocksize, size);

                nextblock = mfsDisk.RAMVolume.FAT[nextblock];
            }
            while (nextblock != (ushort)MFS.FAT.LastFileBlock);

            return filedata;
        }

        public static bool WriteFile(MFSDisk mfsDisk, byte[] filedata, string filepath)
        {
            MFSDirectory _dir = GetDirectoryFromPath(mfsDisk, filepath);

            return WriteFile(mfsDisk, filedata, Path.GetFileName(filepath), _dir.DirectoryID);
        }

        public static bool WriteFile(MFSDisk mfsDisk, byte[] filedata, string name, ushort dir = 0)
        {
            string _name = Path.GetFileNameWithoutExtension(name);
            string _ext = Path.GetExtension(name);

            if (_ext.StartsWith("."))
                _ext = _ext.Substring(1);

            MFSFile file = new MFSFile(_name, mfsDisk.RAMVolume.Entries[0].CompanyCode, mfsDisk.RAMVolume.Entries[0].GameCode, _ext, (uint)filedata.Length, dir);
            return WriteFile(mfsDisk, filedata, file);
        }

        public static bool WriteFile(MFSDisk mfsDisk, byte[] filedata, MFSFile file)
        {
            if (CheckIfFileAlreadyExists(mfsDisk, file.Name, file.Ext, file.ParentDirectory))
                return false;
            if (GetFreeSpaceSize(mfsDisk) < filedata.Length)
                return false;

            int FATentry = -1;
            int lastFATentry = -1;
            int offset = 0;

            //Write free data on every free block from the start
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
                    byte[] blockdata = new byte[Leo.LBAToByte(mfsDisk.RAMVolume.DiskType, Leo.RamStartLBA[mfsDisk.RAMVolume.DiskType] + i, 1)];
                    Array.Copy(filedata, offset, blockdata, 0, Math.Min(blockdata.Length, filedata.Length - offset));
                    mfsDisk.Disk.WriteLBA(Leo.RamStartLBA[mfsDisk.RAMVolume.DiskType] + i, blockdata);

                    offset += Math.Min(blockdata.Length, filedata.Length - offset);

                    //Change FAT entry
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

        public static bool DeleteFile(MFSDisk mfsDisk, string filepath)
        {
            MFSFile file = GetFileFromPath(mfsDisk, filepath);
            if (file != null)
                return DeleteFile(mfsDisk, file);
            else
                return false;
        }

        public static bool DeleteFile(MFSDisk mfsDisk, MFSFile file)
        {
            //Delete FAT Entries recursively
            ushort nextblock = file.FATEntry;
            ushort lastblock = 0;
            do
            {
                lastblock = nextblock;
                nextblock = mfsDisk.RAMVolume.FAT[nextblock];
                mfsDisk.RAMVolume.FAT[lastblock] = 0;
            }
            while (nextblock != (ushort)MFS.FAT.LastFileBlock);

            //Delete File Data
            if (mfsDisk.RAMVolume.Entries.Remove(file))
            {
                Console.WriteLine("found");
            }

            return true;
        }

        public static bool MoveFile(MFSDisk mfsDisk, string filepathin, string filepathout)
        {
            MFSFile file = GetFileFromPath(mfsDisk, filepathin);
            if (file != null)
            {
                if (filepathout.StartsWith("/"))
                {
                    MFSDirectory dir = GetDirectoryFromPath(mfsDisk, filepathout);
                    if (dir != null)
                        file.ParentDirectory = dir.DirectoryID;
                    else
                        return false;
                }
                if (!filepathout.EndsWith("/"))
                {
                    file.Name = Path.GetFileNameWithoutExtension(filepathout);
                    string newext = Path.GetExtension(filepathout);
                    if (newext != "")
                        newext = newext.Substring(1);
                    file.Ext = newext;
                }
                return true;
            }
            else
                return false;
        }

        //Utilities
        public static MFSFile GetFileFromPath(MFSDisk mfsDisk, string filepath)
        {
            MFSDirectory dir = GetDirectoryFromPath(mfsDisk, filepath);
            string[] path = filepath.Split('/');
            path[0] = "/";

            string filename = Path.GetFileNameWithoutExtension(filepath);
            string ext = Path.GetExtension(filepath);
            if (ext.StartsWith("."))
                ext = ext.Substring(1);

            foreach (MFSFile file in GetAllFilesFromDirID(mfsDisk, dir.DirectoryID))
            {
                if (file.Name.Equals(filename) && file.Ext.Equals(ext))
                {
                    return file;
                }
            }

            return null;
        }

        public static MFSFile[] GetAllFilesFromDirID(MFSDisk mfsDisk, ushort id)
        {
            List<MFSFile> list = new List<MFSFile>();

            foreach (MFSEntry entry in mfsDisk.RAMVolume.Entries)
            {
                if (entry.GetType() == typeof(MFSFile))
                {
                    if (id == 0xFFFF || ((MFSFile)entry).ParentDirectory == id)
                        list.Add((MFSFile)entry);
                }
            }

            return list.ToArray();
        }

        public static string GetFullPath(MFSDisk mfsDisk, MFSEntry file)
        {
            string temp = GetParentDirectoryPath(mfsDisk, file) + file.Name;

            if (file.GetType() == typeof(MFSFile) && ((MFSFile)file).Ext != "")
                temp += "." + ((MFSFile)file).Ext;
            else if (file.GetType() == typeof(MFSDirectory))
                temp += "/";

            return temp;
        }

        public static string GetParentDirectoryPath(MFSDisk mfsDisk, MFSEntry file)
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

        // As there can be multiple files with the same name, it is preferable to input a parent Directory ID.
        public static bool CheckIfFileAlreadyExists(MFSDisk mfsDisk, string _filename, ushort _dir = 0xFFFF)
        {
            string _name = Path.GetFileNameWithoutExtension(_filename);
            string _ext = Path.GetExtension(_filename);

            if (_ext.StartsWith("."))
                _ext = _ext.Substring(1);

            return CheckIfFileAlreadyExists(mfsDisk, _name, _ext, _dir);
        }

        public static bool CheckIfFileAlreadyExists(MFSDisk mfsDisk, string _name, string _ext, ushort _dir = 0xFFFF)
        {
            foreach (MFSFile file in GetAllFilesFromDirID(mfsDisk, _dir))
            {
                if (file.Name.Equals(_name) && file.Ext.Equals(_ext))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
