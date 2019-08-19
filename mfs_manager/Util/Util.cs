using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mfs_manager
{
    public static class Util
    {
        //Read Methods
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
            return Encoding.GetEncoding(932, new EncoderReplacementFallback(), new SJISUtil.CustomDecoder()).GetString(Data, Offset, Size).TrimEnd('\x00');
        }

        //Write Methods
        public static void WriteBEU32(uint Input, byte[] Data, int Offset)
        {
            Data[Offset + 0] = (byte)((Input >> 24) & 0xFF);
            Data[Offset + 1] = (byte)((Input >> 16) & 0xFF);
            Data[Offset + 2] = (byte)((Input >> 8) & 0xFF);
            Data[Offset + 3] = (byte)((Input >> 0) & 0xFF);
        }

        public static void WriteBEU16(ushort Input, byte[] Data, int Offset)
        {
            Data[Offset + 0] = (byte)((Input >> 8) & 0xFF);
            Data[Offset + 1] = (byte)((Input >> 0) & 0xFF);
        }

        public static void WriteStringN(string Input, byte[] Data, int Offset, int Size)
        {
            byte[] temp = new byte[Size];
            for (int i = 0; i < Size; i++)
                temp[i] = 0;

            byte[] text = SJISUtil.EncodeStringToSJIS(Input);
            Array.Copy(text, temp, Math.Min(text.Length, Size));

            Array.Copy(temp, 0, Data, Offset, Size);

        }
    }
}
