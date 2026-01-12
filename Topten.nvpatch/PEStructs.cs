using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace nvpatch
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct CoffHeader
    {
        public ushort Machine;
        public ushort NumberOfSection;
        public uint TimeDateStamp;
        public uint PointerToSymbolTable;
        public uint NumberOfSymbols;
        public ushort SizeOfOptionalHeader;
        public ushort Characteristics;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct OptionalHeaderStandard
    {
        public ushort Magic;
        public byte MajorLinkerVersion;
        public byte MinorLinkerVersion;
        public uint SizeOfCode;
        public uint SizeOfInitializedData;
        public uint SizeOfUninitializedData;
        public uint AddressOfEntryPoint;
        public uint BaseOfCode;
        public uint BaseOfData;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct OptionalHeaderStandardPlus
    {
        public ushort Magic;
        public byte MajorLinkerVersion;
        public byte MinorLinkerVersion;
        public uint SizeOfCode;
        public uint SizeOfInitializedData;
        public uint SizeOfUninitializedData;
        public uint AddressOfEntryPoint;
        public uint BaseOfCode;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct OptionalHeaderWindows
    {
        public uint ImageBase;
        public uint SectionAlignment;
        public uint FileAlignment;
        public ushort MajorOperatingSystemVersion;
        public ushort MinorOperatingSystemVersion;
        public ushort MajorImageVersion;
        public ushort MinorImageVersion;
        public ushort MajorSubsystemVersion;
        public ushort MinorSubsystemVersion;
        public uint Win32VersionValue;
        public uint SizeOfImage;
        public uint SizeOfHeaders;
        public uint CheckSum;
        public ushort Subsystem;
        public ushort DllCharacteristics;
        public uint SizeOfStackReserve;
        public uint SizeOfStackCommit;
        public uint SizeOfHeapReserve;
        public uint SizeOfHeapCommit;
        public uint LoaderFlags;
        public uint NumberOfRvaAndSizes;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct OptionalHeaderWindowsPlus
    {
        public ulong ImageBase;
        public uint SectionAlignment;
        public uint FileAlignment;
        public ushort MajorOperatingSystemVersion;
        public ushort MinorOperatingSystemVersion;
        public ushort MajorImageVersion;
        public ushort MinorImageVersion;
        public ushort MajorSubsystemVersion;
        public ushort MinorSubsystemVersion;
        public uint Win32VersionValue;
        public uint SizeOfImage;
        public uint SizeOfHeaders;
        public uint CheckSum;
        public ushort Subsystem;
        public ushort DllCharacteristics;
        public ulong SizeOfStackReserve;
        public ulong SizeOfStackCommit;
        public ulong SizeOfHeapReserve;
        public ulong SizeOfHeapCommit;
        public uint LoaderFlags;
        public uint NumberOfRvaAndSizes;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct DataDirectory
    {
        public uint VirtualAddress;
        public uint Size;
    }

    enum DataDirectoryIndex
    {
        ExportTable,
        ImportTable,
        ResourceTable,
        ExceptionTable,
        CertificateTable,
        BaseRelocationTable,
        Debug,
        Architecture,
        GlobalPtr,
        TLSTable,
        LoadConfigTable,
        BoundImport,
        IAT,
        DelayImportDescriptor,
        CLRRuntimeHeader,
        Reserved,
    }

    public enum PEArchitecture
    {
        PE32,      // x86, Magic 0x10b
        PE32Plus   // x64, Magic 0x20b
    }

    public static class MachineType
    {
        public const ushort I386 = 0x014c;   // x86
        public const ushort AMD64 = 0x8664;  // x64
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct SectionHeader
    {
        public ulong NameBytes;
        public uint VirtualSize;
        public uint VirtualAddress;
        public uint SizeOfRawData;
        public uint PointerToRawData;
        public uint PointerToRelocations;
        public uint PointerToLinenumbers;
        public ushort NumberOfRelocations;
        public ushort NumberOfLinenumbers;
        public uint Characteristics;

        public string Name
        {
            get
            {
                var sb = new StringBuilder();
                var t = NameBytes;
                while (t != 0)
                {
                    sb.Append((char)(t & 0xFF));
                    t = t >> 8;
                }
                return sb.ToString();
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct ExportDirectoryTable
    {
        public uint Flags;
        public uint TimeDateStamp;
        public ushort MajorVersion;
        public ushort MinorVersion;
        public uint NameRVA;
        public uint OrdinalBase;
        public uint AddressTableEntries;
        public uint NumberOfNamePointers;
        public uint ExportAddressTableRVA;
        public uint NamePointerRVA;
        public uint OrdinalTableRVA;
    }

    public static class SectionFlags
    {
        public const uint Code = 0x00000020;
        public const uint InitializedData= 0x00000040;
        public const uint UninitializedData = 0x00000080;
        public const uint MemRead = 0x40000000;
        public const uint MemWrite = 0x80000000;
    }
}
