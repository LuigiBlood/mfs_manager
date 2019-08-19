using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace mfs_manager
{
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
            if (!File.Exists(filepath))
            {
                Format = MFS.DiskFormat.Invalid;
                return;
            }

            FileStream file = new FileStream(filepath, FileMode.Open);

            if (file.Length >= Leo.RamSize[5])
            {
                //Check Header
                file.Seek(0, SeekOrigin.Begin);
                byte[] test = new byte[MFS.RAM_ID.Length];
                file.Read(test, 0, test.Length);

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
    }
}
