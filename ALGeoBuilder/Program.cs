using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using monono2.Common;

namespace monono2.ALGeoBuilder
{
    class Program
    {
        static void PrintUsage()
        {
            Log.WriteLine("Usage: ALGeoBuilder.exe [options] <path2aion>");
        }

        static void Main(string[] args)
        {
            Log.Init(isConsole: true);

            var aionLevelProcessor = new AionLevelsProcessor();

            try
            {
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == "-o")
                        aionLevelProcessor.outputPath = args[++i];
                    else if (args[i] == "-version")
                        aionLevelProcessor.aionClientVersion = int.Parse(args[++i]);
                    else if (args[i] == "-lvl")
                        aionLevelProcessor.levelId = args[++i];
                    else if (args[i] == "-nav")
                        aionLevelProcessor.generateNav = true;
                    else if (args[i] == "-skip_existing_nav")
                        aionLevelProcessor.skipExistingNav = true;
                    else if (args[i] == "-geo")
                        aionLevelProcessor.generateGeo = true;
                    else if (args[i] == "-doors")
                        aionLevelProcessor.generateDoors = true;
                    else if (args[i] == "-no_h32")
                        aionLevelProcessor.noH32 = true;
                    else if (args[i] == "-no_mesh")
                        aionLevelProcessor.noMesh = true;
                    else
                        throw new InvalidOperationException("unknown arg: " + args[i]);
                }
                aionLevelProcessor.aionClientPath = args.Last();
                if (!Directory.Exists(aionLevelProcessor.aionClientPath))
                    throw new InvalidOperationException("error: last argument aionClientPath does not exist");
            }
            catch (Exception e)
            {
                Log.WriteLine(e);
                PrintUsage();
                return;
            }

            // TODO Plugin(s) loading

            if (aionLevelProcessor.aionClientVersion != 1 && aionLevelProcessor.aionClientVersion != 2)
            {
                Log.WriteLine("Unsupported Aion version " + aionLevelProcessor.aionClientVersion);
                PrintUsage();
                return;
            }

            try
            {
                Log.WriteLine("Start levels processing ...");
                aionLevelProcessor.Process();
                Log.WriteLine("Done.");
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                Log.WriteLine(e);
            }
        }
    }
}
