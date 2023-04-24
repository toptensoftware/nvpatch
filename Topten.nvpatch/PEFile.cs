using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace nvpatch
{
    /// <summary>
    /// PE File Reader
    /// </summary>
    unsafe class PEFile : IDisposable
    {
        /// <summary>
        /// Constructs a new PE File and reads a file
        /// </summary>
        /// <param name="filename">The PE file to read</param>
        public PEFile(string filename)
        {
            // Read and lock data
            _bytes = System.IO.File.ReadAllBytes(filename);
            _pin = GCHandle.Alloc(_bytes, GCHandleType.Pinned);
            _p = (byte*)_pin.AddrOfPinnedObject();

            // Check PE signature
            var PEOffset = *(uint*)(_p+ 0x3c);
            var PESignature = *(uint*)(_p+ PEOffset);
            if (PESignature != 0x00004550) //"PE\0\0"
            {
                throw new InvalidDataException("PE signature not found");
            }

            // Get the coff header
            CoffHeader = (CoffHeader*)(_p + PEOffset + 4);
            if (CoffHeader->SizeOfOptionalHeader == 0)
                throw new InvalidDataException("Optional header missing");

            // Get the standard header
            StandardHeader = (OptionalHeaderStandardPlus*)(CoffHeader + 1);
            if (StandardHeader->Magic != 0x20b)
                throw new InvalidDataException("Not a PE32+ format header");

            // Get the windows header
            WindowsHeader = (OptionalHeaderWindowsPlus*)(StandardHeader + 1);

            // Get the section headers
            SectionHeaders = (SectionHeader*)(((byte*)StandardHeader) + CoffHeader->SizeOfOptionalHeader);

            // Find the first used section header
            for (int i = 0; i < CoffHeader->NumberOfSection; i++)
            {
                if (SectionHeaders[i].SizeOfRawData > 0)
                {
                    FirstUsedSectionHeader = SectionHeaders + i;
                    break;
                }
            }

            // Get the data directories
            DataDirectories = (DataDirectory*)(WindowsHeader + 1);
            DataDirectoryCount = (int)(((byte*)SectionHeaders - (byte*)DataDirectories) / Marshal.SizeOf<DataDirectory>());
        }

        /// <summary>
        /// Dispose this object
        /// </summary>
        public void Dispose()
        {
            if (_p != null)
            {
                _pin.Free();
                _p = null;
            }
        }

        /// <summary>
        /// Gets a byte pointer to the loaded image data
        /// </summary>
        public byte* Base => _p;

        /// <summary>
        /// Gets the Coff header
        /// </summary>
        public CoffHeader* CoffHeader { get; }

        /// <summary>
        /// Gets the optional standard header
        /// </summary>
        public OptionalHeaderStandardPlus* StandardHeader { get; }

        /// <summary>
        /// Gets the optional windows header
        /// </summary>
        public OptionalHeaderWindowsPlus* WindowsHeader { get; }

        /// <summary>
        /// Gets the section headers
        /// </summary>
        public SectionHeader* SectionHeaders { get; }

        /// <summary>
        /// Gets the first used section header
        /// </summary>
        /// <remarks>
        /// Used to calculate how much room between the headers and the
        /// first section and therefore how many section headers can be
        /// inserted without having to rejig everything.
        /// </remarks>
        public SectionHeader* FirstUsedSectionHeader { get; }

        public SectionHeader* FindSection(string name)
        {
            for (int i = 0; i < CoffHeader->NumberOfSection; i++)
            {
                if (SectionHeaders[i].Name == name)
                    return SectionHeaders + i;
            }
            return null;
        }

        /// <summary>
        /// Gets the data directories
        /// </summary>
        public DataDirectory* DataDirectories { get; }

        /// <summary>
        /// Gets the data directory count
        /// </summary>
        public int DataDirectoryCount { get; }

        /// <summary>
        /// Give an RVA, work out which section it's in
        /// </summary>
        /// <param name="rva">The rva</param>
        /// <returns>The section header, or null</returns>
        public SectionHeader* SectionForRVA(uint rva)
        {
            for (int i = 0; i < CoffHeader->NumberOfSection; i++)
            {
                if (rva >= SectionHeaders[i].VirtualAddress &&
                    rva < SectionHeaders[i].VirtualAddress + SectionHeaders[i].VirtualSize)
                    return SectionHeaders + i;
            }
            return null;
        }

        /// <summary>
        /// Given an RVA returns a pointer to the image byte at that address
        /// </summary>
        /// <param name="rva">The RVA</param>
        /// <returns>A pointer to the data</returns>
        public byte* GetRVA(uint rva)
        {
            var sect = SectionForRVA(rva);
            if (sect == null)
                return null;

            return _p + sect->PointerToRawData + rva - sect->VirtualAddress;
        }

        /// <summary>
        /// Reads a null terminated string from the image data
        /// </summary>
        /// <param name="rva">RVA of the string</param>
        /// <returns>A string</returns>
        public string ReadString(uint rva)
        {
            // Hacky, ansi only...
            var ptr = (sbyte*)GetRVA(rva);
            var sb = new StringBuilder();
            while (*ptr != '\0')
            {
                sb.Append((char)*ptr++);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Gets the total length of the PE file
        /// </summary>
        public int Length => _bytes.Length;

        /// <summary>
        /// Add a new section to the image
        /// </summary>
        /// <returns>A PESectionBuilder</returns>
        public PESectionBuilder AddSection()
        {
            // Work out the rva and file position of the new section
            uint rva;
            uint filePosition;
            if (_newSections.Count > 0)
            {
                // Close previous section
                var last = _newSections[_newSections.Count - 1];
                last.Close();
                rva = last.RVA + last.VirtualSize;
                filePosition = last.SizeOnDisk;
            }
            else
            {
                // Work out RVA for the new section
                var last = SectionHeaders + (CoffHeader->NumberOfSection - 1);
                rva = last->VirtualAddress + last->VirtualSize;
                filePosition = last->PointerToRawData + last->SizeOfRawData;
            }

            // Round to alignments
            rva = Utils.RoundToAlignment(rva, WindowsHeader->SectionAlignment);
            filePosition = Utils.RoundToAlignment(filePosition, WindowsHeader->FileAlignment);

            // Create section builder
            var b = new PESectionBuilder(this, rva, filePosition);
            _newSections.Add(b);
            return b;
        }

        /// <summary>
        /// Rewrite the image to a file
        /// </summary>
        /// <param name="filename"></param>
        public void Write(string filename)
        {
            // Close last section
            if (_newSections.Count > 0)
            {
                var last = _newSections[_newSections.Count - 1];
                last.Close();
            }

            // Get the end of the last original section, IE: end of the unmodified PE file
            var lastOriginalSection = SectionHeaders + (CoffHeader->NumberOfSection - 1);
            var lastOriginalSectionEnd = lastOriginalSection->PointerToRawData + lastOriginalSection->SizeOfRawData;
            var peFileHasExtraBytes = lastOriginalSectionEnd != _bytes.Length;

            // Update the sizes
            foreach (var s in _newSections)
            {
                if ((s.Characteristics & SectionFlags.InitializedData) != 0)
                    StandardHeader->SizeOfInitializedData += s.SizeOnDisk;
                if ((s.Characteristics & SectionFlags.UninitializedData) != 0)
                    StandardHeader->SizeOfUninitializedData += s.SizeOnDisk;
                if ((s.Characteristics & SectionFlags.Code) != 0)
                    StandardHeader->SizeOfCode += s.SizeOnDisk;

                WindowsHeader->SizeOfImage += s.SizeOnDisk;
            }

            // Write new section headers into the image header
            for (int i = 0; i < _newSections.Count; i++)
            {
                // Get the section
                var section = _newSections[i];

                // Update header
                var s = SectionHeaders + CoffHeader->NumberOfSection;
                s->NameBytes = section.NameBytes;
                s->VirtualSize = section.VirtualSize;
                s->VirtualAddress = section.RVA;
                s->SizeOfRawData = section.SizeOnDisk;
                s->PointerToRawData = section.FilePosition;
                s->PointerToRelocations = 0;
                s->PointerToLinenumbers = 0;
                s->NumberOfRelocations = 0;
                s->NumberOfLinenumbers = 0;
                s->Characteristics = section.Characteristics;

                // Update the section count
                CoffHeader->NumberOfSection++;
            }

            // Create file
            using (var file = File.Create(filename))
            {
                // Write loaded bytes
                file.Write(_bytes);

                // If the original PE file had extra bytes, seek to the end of
                // the PE file's content.
                if (peFileHasExtraBytes)
                    file.Position = lastOriginalSectionEnd;

                long addedBytes = default;

                // Write new sections
                foreach (var s in _newSections)
                {
                    // Update file position
                    System.Diagnostics.Debug.Assert(s.FilePosition >= file.Position);
                    file.Position = s.FilePosition;

                    // Write the section bytes
                    file.Write(s.Bytes);

                    // Write padding
                    var padding = new byte[s.SizeOnDisk - s.Bytes.Length];
                    file.Write(padding);

                    addedBytes += s.Bytes.Length;
                    addedBytes += padding.Length;
                }

                if (peFileHasExtraBytes)
                {
                    /* Re-insert any bytes that were after the end of the original
                     * PE file, after the new end. There's a decent chance this will
                     * break the executable, but it would have anyway if we did nothing.
                     */

                    file.Write(_bytes[(int)lastOriginalSectionEnd..]);

                    /* .NET's "PublishSingleFile" setting will turn the executable into a
                     * "bundle", which involves appending all of the application's dependencies,
                     * content, and configuration to the end of the "AppHost". This manifest
                     * stores offsets to those files, and needs to be updated.
                     */
                    BundleHelper.CheckForAndUpdateManifest(_bytes, file, addedBytes);
                }
            }
        }

        // Loaded image data
        byte[] _bytes;
        GCHandle _pin;
        byte* _p;
        List<PESectionBuilder> _newSections = new();


        static PEFile()
        {
            // Sanity checks that we've got the structures declared correctly
            System.Diagnostics.Debug.Assert(Marshal.SizeOf<CoffHeader>() == 20);
            System.Diagnostics.Debug.Assert(Marshal.SizeOf<OptionalHeaderStandard>() == 28);
            System.Diagnostics.Debug.Assert(Marshal.SizeOf<OptionalHeaderStandardPlus>() == 24);
            System.Diagnostics.Debug.Assert(Marshal.SizeOf<OptionalHeaderWindows>() == 68);
            System.Diagnostics.Debug.Assert(Marshal.SizeOf<OptionalHeaderWindowsPlus>() == 88);
            System.Diagnostics.Debug.Assert(Marshal.SizeOf<OptionalHeaderWindowsPlus>() == 88);
            System.Diagnostics.Debug.Assert(Marshal.SizeOf<SectionHeader>() == 40);
            System.Diagnostics.Debug.Assert(Marshal.SizeOf<ExportDirectoryTable>() == 40);
        }
    }
}
