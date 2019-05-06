using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using monono2.Common;

namespace monono2
{
#if WINDOWS || LINUX
    /// <summary>
    /// The main class.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Log.Init(false);

            if (args.Length != 2 || !Directory.Exists(Path.Combine(args[0], args[1])))
            {
                throw new InvalidOperationException("Usage: aiongeoviewer.exe (client dir) (level folder)");
            }
            string gamedir = args[0];
            string level = args[1];

            using (var game = new Game1(gamedir, level))
                game.Run();
        }
    }
#endif
}
