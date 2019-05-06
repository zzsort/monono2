using System;
using System.IO;

namespace monono2.Common.BinaryXml
{
    public class BinaryXmlFileHelpers
    {
        public static int ReadPackedS32(BinaryReader stream)
        {
            int num1 = stream.ReadByte();
            int num2 = 0;
            int num3 = 0;
            for (; num1 >= 128; num1 = stream.ReadByte())
            {
                num2 |= (num1 & 0x7F) << num3;
                num3 += 7;
            }
            return num2 | (num1 << num3);
        }

        public static String ReadTable(byte[] data, int offset)
        {
            if (offset == 0)
                return "";

            return ByteHelpers.ReadUTF16Z(data, 2 * offset);
        }
    }
}
