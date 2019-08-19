﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mfs_manager
{
    public static class SJISUtil
    {
        static Dictionary<ushort, char> leoMapping = new Dictionary<ushort, char>();
        static bool isMappingReady = false;

        static void PrepareMapping()
        {
            if (isMappingReady)
                return;

            leoMapping = new Dictionary<ushort, char>();
            leoMapping.Add(0x86A3, '\u2660');  //Spade
            leoMapping.Add(0x86A4, '\u2663');  //Club
            leoMapping.Add(0x86A5, '\u2665');  //Heart
            leoMapping.Add(0x86A6, '\u2666');  //Diamond

            isMappingReady = true;
        }

        public static byte[] EncodeStringToSJIS(string str)
        {
            PrepareMapping();

            Encoding encode = Encoding.GetEncoding(932, new EncoderExceptionFallback(), new CustomDecoder());
            List<byte> output = new List<byte>();
            
            foreach (char c in str)
            {
                byte[] byt = { };

                try
                {
                    byt = encode.GetBytes(c.ToString());
                }
                catch (EncoderFallbackException e)
                {
                    //Any invalid chars will dealt with here
                    ushort[] keys = leoMapping.Keys.ToArray();      //SJIS
                    char[] values = leoMapping.Values.ToArray();    //Unicode

                    Debug.Assert(keys.Length == values.Length);

                    //Reverse Search
                    for (int i = 0; i < keys.Length; i++)
                    {
                        if (values[i] == e.CharUnknown)
                        {
                            byt = BitConverter.GetBytes(keys[i]).Reverse().ToArray();
                            break;
                        }
                    }
                }

                foreach (byte b in byt)
                    output.Add(b);
            }

            return output.ToArray();
        }

        //Decoder
        public class CustomDecoder : DecoderFallback
        {
            public string DefaultString;
            internal Dictionary<ushort, char> mapping;

            public CustomDecoder() : this("*")
            {
            }

            public CustomDecoder(string defaultString)
            {
                this.DefaultString = defaultString;

                // Create table of mappings
                PrepareMapping();
                mapping = leoMapping;
            }

            public override DecoderFallbackBuffer CreateFallbackBuffer()
            {
                return new CustomDecoderFallbackBuffer(this);
            }

            public override int MaxCharCount
            {
                get { return 2; }
            }
        }

        public class CustomDecoderFallbackBuffer : DecoderFallbackBuffer
        {
            int count = -1;                   // Number of characters to return
            int index = -1;                   // Index of character to return
            CustomDecoder fb;
            string charsToReturn;

            public CustomDecoderFallbackBuffer(CustomDecoder fallback)
            {
                this.fb = fallback;
            }

            public override bool Fallback(byte[] bytesUnknown, int index)
            {
                // Return false if there are already characters to map.
                if (count >= 1) return false;

                // Determine number of characters to return.
                charsToReturn = String.Empty;

                if (bytesUnknown.Length == 2)
                {
                    ushort key = (ushort)((bytesUnknown[0] << 8) + bytesUnknown[1]);
                    if (fb.mapping.ContainsKey(key))
                    {
                        charsToReturn = Convert.ToString(fb.mapping[key]);
                        count = 1;
                    }
                    else
                    {
                        // Return default.
                        charsToReturn = fb.DefaultString;
                        count = 1;
                    }
                    this.index = charsToReturn.Length - 1;
                }
                else
                {
                    //Only full width
                    charsToReturn = fb.DefaultString;
                    count = 1;
                }
                return true;
            }

            public override char GetNextChar()
            {
                // We'll return a character if possible, so subtract from the count of chars to return.
                count--;
                // If count is less than zero, we've returned all characters.
                if (count < 0)
                    return '\u0000';

                this.index--;
                return charsToReturn[this.index + 1];
            }

            public override bool MovePrevious()
            {
                // Original: if count >= -1 and pos >= 0
                if (count >= -1)
                {
                    count++;
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public override int Remaining
            {
                get { return count < 0 ? 0 : count; }
            }

            public override void Reset()
            {
                count = -1;
                index = -1;
            }
        }
    }
}
