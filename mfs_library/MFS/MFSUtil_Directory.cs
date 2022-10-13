using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mfs_library
{
    public static partial class MFSRAMUtil
    {
        public static MFSDirectory GetDirectoryFromPath(MFSDisk mfsDisk, string dirpath)
        {
            string[] path = dirpath.Split('/');
            path[0] = "/";

            return GetDirectoryFromList(mfsDisk, path.Take(path.Length - 1).ToArray(), GetAllDirectoriesFromDirID(mfsDisk, 0xFFFE));
        }

        private static MFSDirectory GetDirectoryFromList(MFSDisk mfsDisk, string[] names, MFSDirectory[] list)
        {
            foreach (MFSDirectory dir in list)
            {
                if (dir.Name == names[0])
                {
                    if (names.Length > 1)
                        return GetDirectoryFromList(mfsDisk, names.Skip(1).ToArray(), GetAllDirectoriesFromDirID(mfsDisk, dir.DirectoryID));
                    else
                        return dir;
                }
            }
            return null;
        }

        public static MFSDirectory[] GetAllDirectoriesFromDirID(MFSDisk mfsDisk, ushort id)
        {
            List<MFSDirectory> list = new List<MFSDirectory>();

            foreach (MFSEntry entry in mfsDisk.RAMVolume.Entries)
            {
                if (entry.GetType() == typeof(MFSDirectory) && ((MFSDirectory)entry).ParentDirectory == id)
                {
                    list.Add((MFSDirectory)entry);
                }
            }

            return list.ToArray();
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

        // As there can be multiple directories with the same name, it is preferable to input a parent Directory ID. 
        public static bool CheckIfDirectoryAlreadyExists(MFSDisk mfsDisk, string _name, ushort _dir = 0xFFFF)
        {
            foreach (MFSDirectory entry in GetAllDirectoriesFromDirID(mfsDisk, _dir))
            {
                if (entry.Name.Equals(_name))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
