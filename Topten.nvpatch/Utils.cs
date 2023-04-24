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


        #region Code from .NET Runtime Source

        // Portions of the below code are taken from the .NET runtime's source code.
        // Please see: https://github.com/dotnet/runtime/tree/main/src/installer/managed/Microsoft.NET.HostModel/

        // Licensed to the .NET Foundation under one or more agreements.
        // The .NET Foundation licenses this file to you under the MIT license.

        // See: https://en.wikipedia.org/wiki/Knuth%E2%80%93Morris%E2%80%93Pratt_algorithm
        public static int[] ComputeKMPFailureFunction(byte[] pattern)
        {
            int[] table = new int[pattern.Length];
            if (pattern.Length >= 1)
            {
                table[0] = -1;
            }
            if (pattern.Length >= 2)
            {
                table[1] = 0;
            }

            int pos = 2;
            int cnd = 0;
            while (pos < pattern.Length)
            {
                if (pattern[pos - 1] == pattern[cnd])
                {
                    table[pos] = cnd + 1;
                    cnd++;
                    pos++;
                }
                else if (cnd > 0)
                {
                    cnd = table[cnd];
                }
                else
                {
                    table[pos] = 0;
                    pos++;
                }
            }
            return table;
        }

        // See: https://en.wikipedia.org/wiki/Knuth%E2%80%93Morris%E2%80%93Pratt_algorithm
        public static int KMPSearch(byte[] pattern, ReadOnlySpan<byte> bytes)
        {
            int m = 0;
            int i = 0;
            int[] table = ComputeKMPFailureFunction(pattern);

            while (m + i < bytes.Length)
            {
                if (pattern[i] == bytes[m + i])
                {
                    if (i == pattern.Length - 1)
                    {
                        return m;
                    }
                    i++;
                }
                else
                {
                    if (table[i] > -1)
                    {
                        m = m + i - table[i];
                        i = table[i];
                    }
                    else
                    {
                        m++;
                        i = 0;
                    }
                }
            }

            return -1;
        }

        #endregion
    }
}
