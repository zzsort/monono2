using System;
using System.Diagnostics;

namespace monono2.Common
{
    public static class Log
    {
        private static bool s_isInit;
        private static bool s_isConsole;
        public static void Init(bool isConsole)
        {
            if (s_isInit)
                throw new InvalidOperationException("Log already initialized");
            s_isInit = true;
            s_isConsole = isConsole;
        }

        private static void CheckInit()
        {
            if (!s_isInit)
                throw new InvalidOperationException("Log not initialized");
        }

        public static void Write(string s)
        {
            CheckInit();
            if (s_isConsole)
                Console.Write(s);
            Debug.Write(s);
        }

        public static void WriteLine(string s)
        {
            CheckInit();
            if (s_isConsole)
                Console.WriteLine(s);
            Debug.WriteLine(s);
        }

        public static void WriteLine(object o)
        {
            WriteLine(o.ToString());
        }
    }
}
