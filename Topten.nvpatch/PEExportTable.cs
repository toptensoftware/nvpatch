using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace nvpatch
{
    /// <summary>
    /// Packs and unpacks PE export tables
    /// </summary>
    unsafe class PEExportTable
    {
        /// <summary>
        /// Constructs a new export table for a PEFile
        /// </summary>
        /// <param name="pe"></param>
        public PEExportTable(PEFile pe)
        {
            _owner = pe;

            var exportDE = pe.DataDirectories[(int)DataDirectoryIndex.ExportTable];
            var exportsDirectory = (ExportDirectoryTable*)pe.GetRVA(exportDE.VirtualAddress);
            if (exportsDirectory == null)
            {
                _all = new();
                return;
            }

            _originalDirectory = exportsDirectory;

            // Read ExportAddressTable
            var addressTable = (uint*)pe.GetRVA(exportsDirectory->ExportAddressTableRVA);
            for (int i = 0; i < exportsDirectory->AddressTableEntries; i++)
            {
                var e = new Entry()
                {
                    Ordinal = exportsDirectory->OrdinalBase + (uint)i,
                    RVA = addressTable[i],
                };
                _entriesByOrdinal.Add(e.Ordinal, e);
            }

            // Now String Names
            var nameTable = (uint*)pe.GetRVA(exportsDirectory->NamePointerRVA);
            var ordinalTable = (ushort*)pe.GetRVA(exportsDirectory->OrdinalTableRVA);
            for (int i = 0; i < exportsDirectory->NumberOfNamePointers; i++)
            {
                var ordinal = ordinalTable[i] + exportsDirectory->OrdinalBase;
                var name = pe.ReadString(nameTable[i]);
                //Console.WriteLine($" '{name}' = {ordinal}");

                if (_entriesByOrdinal.TryGetValue(ordinal, out var e))
                {
                    e.Name = name;
                    e.NameRVA = nameTable[i];
                    _entriesByName.Add(e.Name, e);
                }
                else
                {
                    throw new InvalidDataException("Ordinal for name not found");
                }
            }

            // Create list of all entries
            _all = _entriesByOrdinal.Values.OrderBy(x => x.Ordinal).ToList();
        }

        /// <summary>
        /// Find an export by name
        /// </summary>
        /// <param name="name">The name of the export to find</param>
        /// <returns>The found entry, or null</returns>
        public Entry Find(string name)
        {
            _entriesByName.TryGetValue(name, out var e);
            return e;
        }

        /// <summary>
        /// Find an export by ordinal
        /// </summary>
        /// <param name="ordinal">The ordinal of the export to find</param>
        /// <returns>The found entry, or null</returns>
        public Entry Find(uint ordinal)
        {
            _entriesByOrdinal.TryGetValue(ordinal, out var e);
            return e;
        }

        /// <summary>
        /// Get all export entries
        /// </summary>
        public IList<Entry> All
        {
            get => _all;
        }

        /// <summary>
        /// Add a new entry
        /// </summary>
        /// <param name="e">The new entry</param>
        public void Add(Entry e)
        {
            _all.Add(e);
            _entriesByOrdinal.Add(e.Ordinal, e);
            if (e.Name != null)
                _entriesByName.Add(e.Name, e);
        }

        /// <summary>
        /// Get the next available ordinal
        /// </summary>
        /// <returns></returns>
        public uint GetNextOrdinal()
        {
            if (_all.Count == 0)
                return 1;
            return _all.Max(x => x.Ordinal) + 1;
        }

        /// <summary>
        /// Information about an export entry
        /// </summary>
        public class Entry
        {
            public uint Ordinal;
            public string Name;
            public uint RVA;
            public uint NameRVA;
        }

        /// <summary>
        /// Re-write the export table to a section builder
        /// </summary>
        /// <param name="sect">The section to write to</param>
        /// <returns>A DataDirectory entry for the exports entry</returns>
        public DataDirectory Write(PESectionBuilder sect)
        {
            // Create the data directory entry and store the current rva
            // as its virtual address
            var dd = new DataDirectory()
            {
                VirtualAddress = sect.CurrentRVA,
            };

            // Setup the table
            var table = new ExportDirectoryTable();
            table.OrdinalBase = _all.Min(x=>x.Ordinal);
            table.AddressTableEntries = (uint)_all.Count;
            table.TimeDateStamp = 0xFFFFFFFF;
            table.Flags = 0;
            table.MajorVersion = 0;
            table.MinorVersion = 0;

            // A writer
            var bw = new BinaryWriter(sect.OutputStream);

            // 1. Write the ExportDirectoryTable (reserver room, we'll rewrite it later)
            var savePos = sect.OutputStream.Position;
            sect.OutputStream.Write(table);

            // 2. Write the export address table
            table.ExportAddressTableRVA = sect.CurrentRVA;
            for (int i = 0; i < _all.Count; i++)
            {
                if (_entriesByOrdinal.TryGetValue((uint)(i + table.OrdinalBase), out var e))
                {
                    bw.Write(e.RVA);
                }
                else
                {
                    bw.Write((uint)0);
                }
            }

            // 3. Write Name Strings
            foreach (var e in _all.Where(x => /*x.NameRVA == 0 && */x.Name != null))
            {
                // Write the string, store it's RVA
                e.NameRVA = sect.CurrentRVA;
                sect.OutputStream.Write(Encoding.UTF8.GetBytes(e.Name));
                sect.OutputStream.Write(new byte[] { 0 });
            }

            // Write module name too
            table.NameRVA = sect.CurrentRVA;
            sect.OutputStream.Write(Encoding.UTF8.GetBytes(ModuleName));
            sect.OutputStream.Write(new byte[] { 0 });

            // 4. Write the Name Pointer Table
            table.NamePointerRVA = sect.CurrentRVA;
            var sorted = _all.OrderBy(x => x.Name).ToList();
            foreach (var e in sorted.Where(x => x.Name != null))
            {
                bw.Write(e.NameRVA);
                table.NumberOfNamePointers++;
            }

            // 5. Write the ordinal table
            table.OrdinalTableRVA = sect.CurrentRVA;
            foreach (var e in sorted.Where(x => x.Name != null))
            {
                bw.Write((ushort)(e.Ordinal - table.OrdinalBase));
            }

            // Rewind and write the table
            sect.OutputStream.Position = savePos;
            var directoryRva = sect.CurrentRVA;
            sect.OutputStream.Write(table);
            sect.OutputStream.Position = sect.OutputStream.Length;

            // Store length in DataDirectory and retur it
            dd.Size = (uint)(sect.OutputStream.Position - savePos);
            return dd;
        }


        PEFile _owner;
        List<Entry> _all;
        Dictionary<string, Entry> _entriesByName = new();
        Dictionary<uint, Entry> _entriesByOrdinal = new();
        ExportDirectoryTable* _originalDirectory;
        public string ModuleName;
    }
}
