using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace nvpatch
{
    class Program
    {
        static string[] GpuSymbols = new string[] {
            "NvOptimusEnablement",
            "AmdPowerXpressRequestHighPerformance"
        };

        unsafe static int Main(string[] args)
        {
            string inFile = null;
            string outFile = null;
            bool statusMode = false;
            bool enableMode = false;
            bool disableMode = false;
            bool quietMode = false;

            // Work out input/output file names
            for (int i = 0; i < args.Length; i++)
            {
                if (Utils.IsSwitch(args[i], out var name, out var value))
                {
                    switch (name)
                    {
                        case "enable":
                            enableMode = true;
                            break;

                        case "disable":
                            disableMode = true;
                            break;

                        case "status":
                            statusMode = true;
                            break;

                        case "quiet":
                            quietMode = true;
                            break;

                        case "help":
                            ShowLogo();
                            ShowHelp();
                            return 0;

                        case "version":
                            ShowLogo();
                            return 0;
                    }
                }
                else
                {
                    if (inFile == null)
                        inFile = args[i];
                    else if (outFile == null)
                        outFile = args[i];
                    else
                        throw new InvalidOperationException($"Don't know what to do with command line arg '{args[i]}'");
                }
            }

            if (inFile == null)
            {
                ShowLogo();
                ShowHelp();
                return 0;
            }

            if (outFile == null)
                outFile = inFile;

            // Read the file
            var pe = new PEFile(inFile);

            // Get (or create) the export table)
            var exports = new PEExportTable(pe);

            // If neither enable nor disable, switch to status mode
            if (!enableMode && !disableMode)
                statusMode = true;

            // Just check it?
            if (statusMode)
            {
                foreach (var s in GpuSymbols)
                {
                    var export = exports.Find(s);
                    if (export == null)
                    {
                        Console.WriteLine($"Module doesn't export {s} symbol");
                    }
                    else
                    {
                        var value = *(uint*)pe.GetRVA(export.RVA);
                        Console.WriteLine($"Module exports {s} symbol as 0x{value:X8}");
                    }
                }
                return 0;
            }

            // Are all the symbols already present, update existing entries
            if (GpuSymbols.All(x => exports.Find(x) != null))
            {
                foreach (var s in GpuSymbols)
                {
                    *(uint*)pe.GetRVA(exports.Find(s).RVA) = enableMode ? 1u : 0;
                }
            }
            else
            {
                if (pe.FindSection(".nvpatch") != null)
                {
                    throw new InvalidOperationException("Can't patch as some symbols are missing and .nvpatch section has already been created");
                }

                // Create a new section into which we'll write the changes
                var newSection = pe.AddSection();
                newSection.Name = ".nvpatch";
                newSection.Characteristics = SectionFlags.InitializedData | SectionFlags.MemRead;

                // Setup the module name
                exports.ModuleName = System.IO.Path.GetFileName(args[0]);

                // Create entres
                foreach (var s in GpuSymbols)
                {
                    // Add export table entry
                    exports.Add(new PEExportTable.Entry()
                    {
                        Ordinal = exports.GetNextOrdinal(),
                        Name = s,
                        RVA = newSection.CurrentRVA,
                    });

                    // Write it's value
                    newSection.OutputStream.Write((uint)(enableMode ? 1u : 0));
                }

                // Write the new exports table
                var newExportDD = exports.Write(newSection);

                // Patch the data directories with the new export table
                pe.DataDirectories[(int)DataDirectoryIndex.ExportTable] = newExportDD;
            }

            // Clear the checksum (just in case)
            pe.WindowsHeader->CheckSum = 0;

            // Rewrite the file
            pe.Write(outFile);
            pe.Dispose();

            if (!quietMode)
            {
                Console.WriteLine("OK");
            }

            return 0;
        }

        static void ShowLogo()
        {
            Console.WriteLine("nvpatch v{0}", Assembly.GetExecutingAssembly().GetName().Version);
            Console.WriteLine("Copyright © 2021 Topten Software. All Rights Reserved");
            Console.WriteLine();
        }

        static void ShowHelp()
        {
            Console.WriteLine("Usage: nvpatch [options] <inputfile.exe> [<outputfile.exe]");
            Console.WriteLine();
            Console.WriteLine("Adds, updates or queries the export symbols 'NvOptimusEnablement'");
            Console.WriteLine("and 'AmdPowerXpressRequestHighPerformance' in an existing .exe");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --enable       sets GPU export symbols to 1 (adding if missing");
            Console.WriteLine("  --disable      sets GPU export symbols to 0 (if it exists)");
            Console.WriteLine("  --status       shows the current NvOptimusEnablement status");
            Console.WriteLine("  --help         show this help, or help for a command");
            Console.WriteLine("  --version      show version information");
        }

    }
}


/*

            Console.WriteLine($"Length: 0x{pe.Length:X8}");
            Console.WriteLine($"Machine type: 0x{pe.CoffHeader->Machine:X2}");
            Console.WriteLine($"Optional header size: 0x{pe.CoffHeader->SizeOfOptionalHeader}");
            Console.WriteLine($"Magic: 0x{pe.StandardHeader->Magic:X4}");
            Console.WriteLine($"Size Code: 0x{pe.StandardHeader->SizeOfCode:X8}");
            Console.WriteLine($"Size Initialized: 0x{pe.StandardHeader->SizeOfInitializedData:X8}");
            Console.WriteLine($"Size Unintialized: 0x{pe.StandardHeader->SizeOfUninitializedData:X8}");
            Console.WriteLine($"Section alignment: 0x{pe.WindowsHeader->SectionAlignment:X8}");
            Console.WriteLine($"File alignment: 0x{pe.WindowsHeader->FileAlignment:X8}");
            Console.WriteLine("Data Directories:");
            for (int i = 0; i < pe.DataDirectoryCount; i++)
            {
                var dd = pe.DataDirectories + i;
                Console.WriteLine($"  #{i + 1,-2}: 0x{dd->VirtualAddress:X8} + 0x{dd->Size:X8}  {(i < 16 ? ((DataDirectoryIndex)i).ToString() : "na")}");
            }

            // Get the section headers
            Console.WriteLine("Sections");
            for (int i = 0; i < pe.CoffHeader->NumberOfSection; i++)
            {
                Console.WriteLine($"  #{i+1,-2} {pe.SectionHeaders[i].Name,8} - 0x{pe.SectionHeaders[i].VirtualAddress:X8} + 0x{pe.SectionHeaders[i].VirtualSize:X8} Disk: 0x{pe.SectionHeaders[i].PointerToRawData:X8} - 0x{pe.SectionHeaders[i].SizeOfRawData:X8}");
            }

            // Work out how much room there is for additional headers
            var esh = (byte*)(pe.SectionHeaders + pe.CoffHeader->NumberOfSection) - pe.Base;
            Console.WriteLine($"Section headers end at 0x{esh:X8}, room for {(pe.FirstUsedSectionHeader->PointerToRawData - esh) / Marshal.SizeOf<SectionHeader>()} more sections");

            // Get the exports table

            foreach (var e in exports.All)
            {
                Console.WriteLine($"#{e.Ordinal,-10} 0x{e.RVA:X8} {e.Name} => {*(uint*)pe.GetRVA(e.RVA):X8}");
            }
*/



