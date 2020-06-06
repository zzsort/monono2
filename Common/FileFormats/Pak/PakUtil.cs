using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace monono2.Common.FileFormats.Pak
{
    public static class PakUtil
    {
        public static string NormalizeFilename(string originalFilename)
        {
            return originalFilename.ToLower(new CultureInfo("en-US", false)).Replace('\\', '/');
        }

        public static string ReadFilename(BinaryReader br, int length)
        {
            return NormalizeFilename(Encoding.UTF8.GetString(br.ReadBytes(length)));
        }
    }
}
