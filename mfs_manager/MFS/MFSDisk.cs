using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;

namespace mfs_manager
{
    public class MFSDisk
    {
        public string Filename;
        public MFS.DiskFormat Format;
        public int OffsetToMFSRAM;
        public int OffsetToSysData;
        public int DiskType;
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
                        this.DiskType = DiskType;
                        Format = MFS.DiskFormat.RAM;
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
                            //Convert Data from MAME to RAM
                            byte[] DataTemp = new byte[Leo.RamSize[diskType]];

                            int copiedAmount = 0;
                            for (int lba = Leo.RamStartLBA[diskType]; lba < Leo.MAX_LBA; lba++)
                            {
                                int calcOffset = Leo.LBAToMAMEOffset(lba, sysData);
                                int calcSize = Leo.LBAToByte(diskType, lba, 1);
                                file.Seek(calcOffset, SeekOrigin.Begin);
                                file.Read(DataTemp, copiedAmount, calcSize);
                                copiedAmount += calcSize;
                            }

                            //Copy full file
                            Data = new byte[file.Length];
                            file.Seek(0, SeekOrigin.Begin);
                            file.Read(Data, 0, Data.Length);

                            RAMVolume = new MFSRAMVolume(DataTemp, OffsetToMFSRAM);
                            this.DiskType = diskType;
                            Format = MFS.DiskFormat.MAME;
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

                            RAMVolume = new MFSRAMVolume(Data, OffsetToMFSRAM);
                            DiskType = i;
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

            int offset = 0;
            for (int i = 0; i < 3; i++)
            {
                byte[] temp = new byte[Leo.LBAToByte(DiskType, Leo.RamStartLBA[DiskType] + i, 1)];
                Array.Copy(volume, offset, temp, 0, temp.Length);
                WriteLBA(Leo.RamStartLBA[DiskType] + i, temp);
                offset += temp.Length;
            }

            offset = 0;
            for (int i = 3; i < 6; i++)
            {
                byte[] temp = new byte[Leo.LBAToByte(DiskType, Leo.RamStartLBA[DiskType] + i, 1)];
                Array.Copy(volume, offset, temp, 0, temp.Length);
                WriteLBA(Leo.RamStartLBA[DiskType] + i, temp);
                offset += temp.Length;
            }

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
            if (Format == MFS.DiskFormat.MAME)
            {
                byte[] sysData = new byte[Leo.SECTOR_SIZE[0]];
                Array.Copy(Data, OffsetToSysData, sysData, 0, sysData.Length);

                int sourceOffset = Leo.LBAToMAMEOffset(lba, sysData);
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
            if (Format == MFS.DiskFormat.MAME)
            {
                byte[] sysData = new byte[Leo.SECTOR_SIZE[0]];
                Array.Copy(Data, OffsetToSysData, sysData, 0, sysData.Length);

                int destOffset = Leo.LBAToMAMEOffset(lba, sysData);
                Array.Copy(data, 0, Data, destOffset, blockSize);
            }
            else
            {
                int destOffset = Leo.LBAToByte(DiskType, Leo.RamStartLBA[DiskType], lba - Leo.RamStartLBA[DiskType]) + OffsetToMFSRAM;
                Array.Copy(data, 0, Data, destOffset, blockSize);
            }
        }
    }
}
