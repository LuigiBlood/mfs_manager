using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace mfs_library
{
    public static class MFS
    {
        //MFS Volume
        public struct VolumeAttr
        {
            public bool isWriteProtected;          //Filesystem is Write Protected
            public bool isVolumeWriteProtected;    //Cannot be written by other applications
            public bool isVolumeReadProtected;     //Cannot be read by other applications
        }

        public const string ROM_ID = "64dd-Multi0201";
        public const string RAM_ID = "64dd-Multi0101";

        //MFS FAT
        public enum FAT : ushort
        {
            Unused = 0x0000,
            DontManage = 0xFFFD,
            Prohibited = 0xFFFE,
            LastFileBlock = 0xFFFF
        }

        public const int FAT_MAX = 2874;

        //MFS Entry
        public struct EntryAttr
        {
            public bool CopyLimit;         //Limit Copy
            public bool Encode;            //Encode
            public bool Hidden;            //Hidden
            public bool DisableRead;       //Cannot be read by other applications
            public bool DisableWrite;      //Cannot be written, renamed, or deleted by other applications
        }

        public static readonly ushort[] EntryLimit = { 899, 814, 729, 644, 559, 474, 0 };

        public class Date
        {
            public uint Year;
            public uint Month;
            public uint Day;
            public uint Hour;
            public uint Minute;
            public uint Second;

            public Date()
            {
                Year = (uint)DateTime.Now.Year;
                Month = (uint)DateTime.Now.Month;
                Day = (uint)DateTime.Now.Day;
                Hour = (uint)DateTime.Now.Hour;
                Minute = (uint)DateTime.Now.Minute;
                Second = (uint)DateTime.Now.Second;
            }

            public Date(uint data)
            {
                Load(data);
            }

            public void Load(uint data)
            {
                Year = (data >> 25) + 1996;
                Month = (data >> 21) & 0xF;
                Day = (data >> 16) & 0x1F;
                Hour = (data >> 11) & 0x1F;
                Minute = (data >> 5) & 0x3F;
                Second = ((data >> 0) & 0x1F) * 2;
            }

            public uint Save()
            {
                uint temp = 0;

                temp |= (Year - 1996) << 25;
                temp |= Month << 21;
                temp |= Day << 16;
                temp |= Hour << 11;
                temp |= Minute << 5;
                temp |= (Second / 2) << 0;

                return temp;
            }
        }

        enum Error
        {
            Good = 0, Argument, Filename, FileNotExist, DiskFull, FileAlreadyExists
        }
    }
}
