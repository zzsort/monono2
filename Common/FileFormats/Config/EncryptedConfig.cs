using System;
using System.IO;
using System.Text;

namespace monono2.Common.FileFormats.Config
{
    public static class EncryptedConfig
    {
        // decodes the system.cfg file.
        public static string DecryptConfigFile(string path)
        {
            var b = File.ReadAllBytes(path);
            for (int i = 0; i < b.Length; i++)
            {
                if (b[i] >= 0x80)
                    b[i] ^= 0xFF;
            }
            return Encoding.ASCII.GetString(b);
        }
    }
}
