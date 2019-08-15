using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mfs_manager
{
    public static class Util
    {
        public static uint ReadBEU32(byte[] Data, int Offset)
        {
            return (uint)((Data[Offset + 0] << 24) | (Data[Offset + 1] << 16) | (Data[Offset + 2] << 8) | (Data[Offset + 3] << 0));
        }

        public static ushort ReadBEU16(byte[] Data, int Offset)
        {
            return (ushort)((Data[Offset + 0] << 8) | (Data[Offset + 1] << 0));
        }

        public static string ReadStringN(byte[] Data, int Offset, int Size)
        {
            return Encoding.GetEncoding(932).GetString(Data, Offset, Size).TrimEnd('\x00');
        }
    }
}
