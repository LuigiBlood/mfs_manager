using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

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
            Format = MFS.DiskFormat.Invalid;
            if (!File.Exists(filepath))
            {
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
                    //It's a RAM file
                    file.Seek(15, SeekOrigin.Begin);
                    int DiskType = file.ReadByte();
                    if (DiskType >= 0 && DiskType < 6 && Leo.RamSize[DiskType] == file.Length)
                    {
                        file.Seek(0, SeekOrigin.Begin);

                        OffsetToMFSRAM = 0;
                        Data = new byte[file.Length];
                        file.Read(Data, 0, Data.Length);

                        RAMVolume = new MFSRAMVolume(Data, OffsetToMFSRAM);
                        Format = MFS.DiskFormat.RAM;
                    }
                }
                else
                {
                    //SHA256 check if N64 Cartridge Port bootloader
                    byte[] headerTest = new byte[0xFC0];
                    file.Seek(0x40, SeekOrigin.Begin);
                    file.Read(headerTest, 0, headerTest.Length);

                    SHA256 hashHeader = SHA256.Create();
                    hashHeader.ComputeHash(headerTest);

                    string hashHeaderStr = "";
                    foreach (byte b in hashHeader.Hash)
                        hashHeaderStr += b.ToString("x2");

                    Console.WriteLine(hashHeaderStr);

                    int offsetStart = 0;

                    //SHA256 = 53c0088fb777870d0af32f0251e964030e2e8b72e830c26042fd191169508c05
                    if (hashHeaderStr == "53c0088fb777870d0af32f0251e964030e2e8b72e830c26042fd191169508c05")
                        offsetStart = 0x738C0 - 0x10E8; //Start of User LBA 0 (24 w/ System Area)

                    //Try every Disk Type
                    for (int i = 0; i < 6; i++)
                    {
                        int offset = Leo.LBAToByte(i, 0, Leo.RamStartLBA[i]) - offsetStart;
                        file.Seek(offset, SeekOrigin.Begin);
                        file.Read(test, 0, test.Length);
                        if (Encoding.ASCII.GetString(test).Equals(MFS.RAM_ID))
                        {
                            OffsetToMFSRAM = offset;
                            Data = new byte[file.Length];
                            file.Seek(0, SeekOrigin.Begin);
                            file.Read(Data, 0, Data.Length);

                            RAMVolume = new MFSRAMVolume(Data, OffsetToMFSRAM);
                            if (offsetStart == 0)
                                Format = MFS.DiskFormat.SDK;
                            else
                                Format = MFS.DiskFormat.N64;
                            break;
                        }
                    }
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
