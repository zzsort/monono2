using System;
using System.IO;

namespace monono2.AionClientViewer
{
    public static class ClientViewerSettings
    {
        public static string GetClientDir()
        {
            if (!File.Exists(GetConfigFilename()))
                return null;
            var lines = File.ReadAllLines(GetConfigFilename());
            if (lines.Length == 0)
                return null;
            if (!Directory.Exists(lines[0]))
                return null;
            return lines[0];
        }
        public static void SetClientDir(string dir)
        {
            if (dir != "" && !Directory.Exists(dir))
                return;
            Directory.CreateDirectory(Path.GetDirectoryName(GetConfigFilename()));
            File.WriteAllText(GetConfigFilename(), dir);
        }
        private static string GetConfigFilename()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(local, "monono2", "aionclientviewer.cfg");
        }
    }
}
