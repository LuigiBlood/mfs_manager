using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using static mfs_library.MFS;

namespace mfs_library
{
    public class MFSDisk
    {
        public LeoDisk Disk;
        public MFSRAMVolume RAMVolume;

        public MFSDisk(string filepath)
        {
            //Load Disk
            Disk = new LeoDisk(filepath);
            LoadRAMVolume();
        }

        public MFSDisk(LeoDisk disk)
        {
            //Reuse already loaded disk
            Disk = disk;
            LoadRAMVolume();
        }

        void LoadRAMVolume()
        {
            //Load RAM Volume
            if (Disk.Format == LeoDisk.DiskFormat.Invalid)
                return;
            if (Disk.RAMFileSystem != LeoDisk.FileSystem.MFS)
                return;

            RAMVolume = new MFSRAMVolume(Disk.GetRAMAreaArray(), 0);
        }

        public void Save(string filepath)
        {
            //Save RAM Volume FAT Table into Disk data
            byte[] volume = RAMVolume.SaveToArray();
            int offset = 0;
            for (int i = 0; i < 3; i++)
            {
                byte[] temp = new byte[Leo.LBAToByte(Disk.DiskType, Leo.RamStartLBA[Disk.DiskType] + i, 1)];
                Array.Copy(volume, offset, temp, 0, temp.Length);
                Disk.WriteLBA(Leo.RamStartLBA[Disk.DiskType] + i, temp);
                offset += temp.Length;
            }

            offset = 0;
            for (int i = 3; i < 6; i++)
            {
                byte[] temp = new byte[Leo.LBAToByte(Disk.DiskType, Leo.RamStartLBA[Disk.DiskType] + i, 1)];
                Array.Copy(volume, offset, temp, 0, temp.Length);
                Disk.WriteLBA(Leo.RamStartLBA[Disk.DiskType] + i, temp);
                offset += temp.Length;
            }

            //Save the Disk data
            Disk.Save(filepath);
        }
    }
}
