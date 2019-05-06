using System;
using System.Text;

namespace monono2.Common
{
    public static class ByteHelpers
    {
        public static string ReadUTF16Z(byte[] data, int offset)
        {
            var sb = new StringBuilder();
            for (int i = offset; data[i] != 0 || data[i + 1] != 0; i += 2)
            {
                sb.Append((char)(data[i] | data[i + 1] << 8));
            }

            return sb.ToString();
        }
    }
}
