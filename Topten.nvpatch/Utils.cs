using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace nvpatch
{
    static class Utils
    {
        public static uint RoundToAlignment(uint val, uint alignment)
        {
            var over = val % alignment;
            if (over > 0)
            {
                return val + alignment - over;
            }
            else
            {
                return val;
            }

        }

        // Write a structure to the output stream
        public static void Write<T>(this Stream This, T val) where T : unmanaged
        {
            unsafe
            {
                int length = Marshal.SizeOf<T>();
                byte[] myBuffer = new byte[length];
                fixed (byte* p = myBuffer)
                {
                    Marshal.StructureToPtr(val, (IntPtr)p, true);
                }
                This.Write(myBuffer);
            }
        }


        public static bool IsSwitch(string arg, out string switchName, out string switchValue)
        {
            // Args are in format [/--]<switchname>[:<value>];
            if (arg.StartsWith("/") || arg.StartsWith("-"))
            {
                // Split into switch name and value
                switchName = arg.Substring(arg.StartsWith("--") ? 2 : 1);
                switchValue = null;
                int colonpos = switchName.IndexOf(':');
                if (colonpos >= 0)
                {
                    switchValue = switchName.Substring(colonpos + 1);
                    switchName = switchName.Substring(0, colonpos).ToLower();
                }
                return true;
            }
            else
            {
                switchValue = null;
                switchName = null;
                return false;
            }
        }
    }
}
