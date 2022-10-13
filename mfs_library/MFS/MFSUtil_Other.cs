using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mfs_library
{
    public static partial class MFSRAMUtil
    {
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

        public static int GetFreeSpaceSize(MFSDisk mfsDisk)
        {
            int unused = Leo.RamSize[mfsDisk.RAMVolume.DiskType] - GetTotalUsedSize(mfsDisk) - Leo.LBAToByte(mfsDisk.RAMVolume.DiskType, Leo.RamStartLBA[mfsDisk.RAMVolume.DiskType], 6);

            return unused;
        }

        public static int GetCapacitySize(MFSDisk mfsDisk)
        {
            int totalsize = 0;
            for (int i = 6; i < Leo.SIZE_LBA - Leo.RamStartLBA[mfsDisk.RAMVolume.DiskType]; i++)
            {
                switch (mfsDisk.RAMVolume.FAT[i])
                {
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
    }
}
