using Microsoft.SqlServer.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static mfs_library.MFS;

namespace mfs_library
{
    public class LeoDisk
    {
        public enum DiskFormat
        {
            SDK,
            D64,
            RAM,
            N64,
            MAME,
            Invalid
        }

        public string Filename;
        public LeoDisk.DiskFormat Format;
        public int OffsetToMFSRAM;
        public int OffsetToSysData;
        public int DiskType;
        public byte[] Data;

        public LeoDisk(string filepath)
        {
            Load(filepath);
        }

        void Load(string filepath)
        {
            //Assume RAM format for now
            Format = DiskFormat.Invalid;
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

                        this.DiskType = DiskType;
                        Format = DiskFormat.RAM;
                    }
                }
                else if (file.Length == 0x435B0C0)
                {
                    //It's a MAME disk format file
                    //Check each System Data Block
                    bool correctSysData = false;

                    //Check Retail SysData
                    byte[] sysData = new byte[Leo.SECTOR_SIZE[0]];
                    foreach (int lba in Leo.LBA_SYS_PROD)
                    {
                        OffsetToSysData = Leo.BLOCK_SIZE[0] * lba;
                        file.Seek(OffsetToSysData, SeekOrigin.Begin);
                        file.Read(sysData, 0, sysData.Length);

                        bool isEqual = true;
                        for (int i = 1; i < Leo.SECTORS_PER_BLOCK; i++)
                        {
                            byte[] sysDataCompare = new byte[sysData.Length];
                            file.Read(sysDataCompare, 0, sysDataCompare.Length);

                            //Compare Bytes
                            for (int j = 0; j < sysDataCompare.Length; j++)
                            {
                                if (sysDataCompare[j] != sysData[j])
                                {
                                    isEqual = false;
                                    break;
                                }
                            }
                            //If not equal then don't bother doing more
                            if (!isEqual) break;
                        }

                        correctSysData = isEqual;

                        //If SysData is found then it's fine don't bother checking the rest
                        if (correctSysData) break;
                    }

                    //Check Dev SysData if not found
                    if (!correctSysData)
                    {
                        sysData = new byte[Leo.SECTOR_SIZE[3]];
                        foreach (int lba in Leo.LBA_SYS_DEV)
                        {
                            OffsetToSysData = Leo.BLOCK_SIZE[0] * lba;
                            file.Seek(OffsetToSysData, SeekOrigin.Begin);
                            file.Read(sysData, 0, sysData.Length);

                            bool isEqual = true;
                            for (int i = 1; i < Leo.SECTORS_PER_BLOCK; i++)
                            {
                                byte[] sysDataCompare = new byte[sysData.Length];
                                file.Read(sysDataCompare, 0, sysDataCompare.Length);

                                //Compare Bytes
                                for (int j = 0; j < sysDataCompare.Length; j++)
                                {
                                    if (sysDataCompare[j] != sysData[j])
                                    {
                                        isEqual = false;
                                        break;
                                    }
                                }
                                //If not equal then don't bother doing more
                                if (!isEqual) break;
                            }

                            correctSysData = isEqual;

                            //If SysData is found then it's fine don't bother checking the rest
                            if (correctSysData) break;
                        }
                    }

                    //if SysData found
                    if (correctSysData)
                    {
                        int diskType = sysData[0x5] & 0xF;
                        int offset = Leo.LBAToMAMEOffset(Leo.RamStartLBA[diskType], sysData);
                        file.Seek(offset, SeekOrigin.Begin);
                        file.Read(test, 0, test.Length);
                        //See if equal to RAM_ID, and if so, it is found.
                        if (Encoding.ASCII.GetString(test).Equals(MFS.RAM_ID))
                        {
                            OffsetToMFSRAM = 0;

                            //Copy full file
                            Data = new byte[file.Length];
                            file.Seek(0, SeekOrigin.Begin);
                            file.Read(Data, 0, Data.Length);

                            this.DiskType = diskType;
                            Format = DiskFormat.MAME;
                        }
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
                        //Try Offset to MFS RAM Partition
                        int offset = Leo.LBAToByte(i, 0, Leo.RamStartLBA[i]) - offsetStart;
                        file.Seek(offset, SeekOrigin.Begin);
                        file.Read(test, 0, test.Length);
                        //See if equal to RAM_ID, and if so, it is found.
                        if (Encoding.ASCII.GetString(test).Equals(MFS.RAM_ID))
                        {
                            OffsetToMFSRAM = offset;
                            Data = new byte[file.Length];
                            file.Seek(0, SeekOrigin.Begin);
                            file.Read(Data, 0, Data.Length);

                            DiskType = i;
                            if (offsetStart == 0)
                                Format = DiskFormat.SDK;
                            else
                                Format = DiskFormat.N64;
                            break;
                        }
                    }
                }
            }

            file.Close();
        }

        public void Save(string filepath)
        {
            FileStream file = new FileStream(filepath, FileMode.Create);
            file.Write(Data, 0, Data.Length);
            file.Close();
        }

        public byte[] ReadLBA(int lba)
        {
            //Do not read anywhere before RAM Area
            if (lba < Leo.RamStartLBA[DiskType]) return null;

            //Read Block
            byte[] output = new byte[Leo.LBAToByte(DiskType, lba, 1)];
            if (Format == DiskFormat.MAME)
            {
                int sourceOffset = Leo.LBAToMAMEOffset(lba, GetSystemData());
                Array.Copy(Data, sourceOffset, output, 0, output.Length);
            }
            else
            {
                int sourceOffset = Leo.LBAToByte(DiskType, Leo.RamStartLBA[DiskType], lba - Leo.RamStartLBA[DiskType]) + OffsetToMFSRAM;
                Array.Copy(Data, sourceOffset, output, 0, output.Length);
            }

            return output;
        }

        public void WriteLBA(int lba, byte[] data)
        {
            //Do not write anywhere before RAM Area
            if (lba < Leo.RamStartLBA[DiskType]) return;

            //Check if data block is exact size of the expected LBA block size
            int blockSize = Leo.LBAToByte(DiskType, lba, 1);
            if (data.Length != blockSize) return;

            //Write Block
            if (Format == DiskFormat.MAME)
            {
                int destOffset = Leo.LBAToMAMEOffset(lba, GetSystemData());
                Array.Copy(data, 0, Data, destOffset, blockSize);
            }
            else
            {
                int destOffset = Leo.LBAToByte(DiskType, Leo.RamStartLBA[DiskType], lba - Leo.RamStartLBA[DiskType]) + OffsetToMFSRAM;
                Array.Copy(data, 0, Data, destOffset, blockSize);
            }
        }

        public byte[] GetSystemData()
        {
            byte[] sysData = new byte[Leo.SECTOR_SIZE[0]];

            Array.Copy(Data, OffsetToSysData, sysData, 0, sysData.Length);

            return sysData;
        }

        public byte[] GetRAMAreaArray()
        {
            List<byte> array = new List<byte>();

            for (int lba = Leo.RamStartLBA[DiskType]; lba < Leo.MAX_LBA; lba++)
            {
                array.AddRange(ReadLBA(lba));
            }

            return array.ToArray();
        }
    }
}
