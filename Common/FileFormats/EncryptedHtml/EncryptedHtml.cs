using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace monono2.Common.EncryptedHtml
{
    public static class EncryptedHtml
    {
        public static byte[] Decode(string originalFilename, Stream input)
        {
            var b = DecodeInternal(originalFilename, input);
            return b.Skip(1).ToArray();
        }

        public static string DecodeToString(string originalFilename, Stream input)
        {
            var b = DecodeInternal(originalFilename, input);

            // TODO - currently only knows utf16le...
            if (b[1] != 0xFF || b[2] != 0xFE)
                throw new InvalidOperationException("unexpected BOM");

            return Encoding.Unicode.GetString(b, 3, b.Length - 3);
        }

        private static byte[] DecodeInternal(string originalFilename, Stream input)
        {
            if (input.ReadByte() != 0x81)
                throw new InvalidOperationException("invalid header");

            using (var ms = new MemoryStream())
            {
                input.CopyTo(ms);
                var b = ms.ToArray();

                b[0] = 0;

                int magic = 0;
                int magic2 = 0;

                var filenameWithoutExt = Path.GetFileNameWithoutExtension(originalFilename);
                for (int i = 0; i < filenameWithoutExt.Length; i++)
                {
                    var tmp = (filenameWithoutExt[i] & 0xF) + i;
                    magic += tmp;
                    magic2 ^= tmp;
                }

                for (int i = 0; i < b.Length; i++)
                {
                    magic += 0x1D;
                    magic ^= magic2;
                    magic2 += 3;
                    b[i] ^= (byte)magic;
                }

                //if (b[0] != 0x81)
                //    throw new InvalidOperationException("unexpected decode result");
                
                return b;
            }
        }

    }
}
