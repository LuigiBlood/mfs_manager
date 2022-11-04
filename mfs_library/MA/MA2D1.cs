using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Security.Cryptography;

namespace mfs_library.MA
{
    public static class MA2D1
    {
        public static Bitmap ConvertToBitmap(byte[] bytes)
        {
            byte[] headerf = new byte[16];
            Array.Copy(bytes, 0x480, headerf, 0, headerf.Length);
            string header = Encoding.ASCII.GetString(headerf);

            Bitmap bitmap = null;
            if (header.StartsWith("RGBA"))
            {
                //RAW
                int width = Convert.ToInt32(header.Substring(4, 3));
                int height = Convert.ToInt32(header.Substring(7, 3));
                int size = Convert.ToInt32(header.Substring(10, 6));

                bitmap = new Bitmap(width, height);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        ushort color = (ushort)((bytes[0x490 + (y * width * 2) + (x * 2)] << 8) + bytes[0x490 + (y * width * 2) + (x * 2) + 1]);
                        bitmap.SetPixel(x, y, RGBA16ToColor(color));
                    }
                }
            }
            else if (header.StartsWith("NCMP"))
            {
                //Yay1

                //TODO
            }

            return bitmap;
        }

        public static byte[] ConvertToMA2D1(Bitmap bitmap)
        {
            byte[] thumbnail = ConvertToRGBA16(bitmap, 24, 24, true);
            byte[] image = ConvertToRGBA16(bitmap, 216, 202);
            string header = "RGBA216202" + image.Length.ToString("D6");

            List<byte> data = new List<byte>();
            data.AddRange(thumbnail);
            data.AddRange(Encoding.ASCII.GetBytes(header));
            data.AddRange(image);

            return data.ToArray();
        }

        static byte[] ConvertToRGBA16(Bitmap bitmap, int width, int height, bool forcealpha = false)
        {
            Bitmap output = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(output))
            {
                g.DrawImage(bitmap, 0, 0, width, height);
            }

            List<byte> bytes = new List<byte>();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    ushort color = ColorToRGBA16(output.GetPixel(x, y), forcealpha);
                    bytes.Add((byte)(color >> 8));
                    bytes.Add((byte)(color >> 0));
                }
            }

            return bytes.ToArray();
        }

        static ushort ColorToRGBA16(Color color, bool forcealpha = false)
        {
            uint r = (uint)((color.R / 255f) * 31f) & 0x1F;
            uint g = (uint)((color.G / 255f) * 31f) & 0x1F;
            uint b = (uint)((color.B / 255f) * 31f) & 0x1F;
            uint a = (uint)((color.A / 255f) * 1f) & 1;
            if (forcealpha) a = 1;

            ushort output = (ushort)((r << 11) + (g << 6) + (b << 1) + a);

            return output;
        }

        static Color RGBA16ToColor(ushort rgba)
        {
            int r = (int)((((rgba >> 11) & 0x1F) / 31f) * 255f);
            int g = (int)((((rgba >>  6) & 0x1F) / 31f) * 255f);
            int b = (int)((((rgba >>  1) & 0x1F) / 31f) * 255f);
            int a = (rgba & 1) * 255;

            Color output = Color.FromArgb(a, r, g, b);
            return output;
        }
    }
}
