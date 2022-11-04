using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;

namespace mfs_library.MA
{
    public static class Yay1
    {
        public static byte[] Decompress(Stream data)
        {
            // This is reusing the code from
            // https://github.com/Daniel-McCarthy/Mr-Peeps-Compressor/blob/master/PeepsCompress/PeepsCompress/Algorithm%20Classes/YAY0.cs
            // By Daniel McCarthy, under the MIT License
            int beginningOffset = (int)data.Position;

            byte[] temp = new byte[4];
            data.Read(temp, 0, 4);
            string header = Encoding.ASCII.GetString(temp).ToLower();

            if (header != "yay0" && header != "yay1")
                return null;

            List<byte> output = new List<byte>();

            data.Read(temp, 0, 4); Array.Reverse(temp, 0, 4);
            int outputLength = BitConverter.ToInt32(temp, 0);
            data.Read(temp, 0, 4); Array.Reverse(temp, 0, 4);
            int compOffset = BitConverter.ToInt32(temp, 0) + beginningOffset;
            data.Read(temp, 0, 4); Array.Reverse(temp, 0, 4);
            int uncompOffset = BitConverter.ToInt32(temp, 0) + beginningOffset;

            int currentOffset;

            while (output.Count < outputLength)
            {
                byte bits = (byte)data.ReadByte(); //byte of layout bits
                BitArray arrayOfBits = new BitArray(new byte[1] { bits });

                for (int i = 7; i > -1 && (output.Count < outputLength); i--)
                {
                    if (arrayOfBits[i] == true)
                    {
                        //non-compressed
                        //add one byte from uncompressedOffset to newFile

                        currentOffset = (int)data.Position;

                        data.Seek(uncompOffset, SeekOrigin.Begin);

                        output.Add((byte)data.ReadByte());
                        uncompOffset++;

                        data.Seek(currentOffset, SeekOrigin.Begin);

                    }
                    else
                    {
                        //compressed
                        //read 2 bytes
                        //4 bits = length
                        //12 bits = offset

                        currentOffset = (int)data.Position;
                        data.Seek(compOffset, SeekOrigin.Begin);

                        byte byte1 = (byte)data.ReadByte();
                        byte byte2 = (byte)data.ReadByte();
                        compOffset += 2;

                        byte byte1Upper = (byte)((byte1 & 0x0F));
                        byte byte1Lower = (byte)((byte1 & 0xF0) >> 4);

                        int finalOffset = ((byte1Upper << 8) | byte2) + 1;
                        int finalLength;

                        if (byte1Lower == 0)
                        {
                            data.Seek(uncompOffset, SeekOrigin.Begin);
                            finalLength = (byte)data.ReadByte() + 0x12;
                            uncompOffset++;
                        }
                        else
                        {
                            finalLength = byte1Lower + 2;
                        }

                        for (int j = 0; j < finalLength; j++) //add data for finalLength iterations
                        {
                            output.Add(output[output.Count - finalOffset]); //add byte at offset (fileSize - finalOffset) to file
                        }

                        data.Seek(currentOffset, SeekOrigin.Begin); //return to layout bits

                    }
                }
            }

            return output.ToArray();
        }
    }
}
