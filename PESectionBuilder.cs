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
    /// Helper for building a new PE section
    /// </summary>
    unsafe class PESectionBuilder
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="owner">The owning PE File</param>
        /// <param name="rva">The base RVA of this section</param>
        public PESectionBuilder(PEFile owner, uint rva, uint filePosition)
        {
            Owner = owner;
            RVA = rva;
            FilePosition = filePosition;
            _stream = new MemoryStream();
        }

        /// <summary>
        /// Close this section, calulating the final virtual and disk size
        /// </summary>
        public void Close()
        {
            if (_stream != null)
            {
                _bytes = _stream.ToArray();
                _stream = null;
            }
        }

        /// <summary>
        /// Get the stream for writing content to
        /// </summary>
        public MemoryStream OutputStream => _stream;

        /// <summary>
        /// Get the finalized bytes for this section
        /// </summary>
        public byte[] Bytes => _bytes;

        /// <summary>
        /// Get RVA of current output position 
        /// </summary>
        public uint CurrentRVA => RVA + (uint)_stream.Position;

        /// <summary>
        /// Gets the owning PE File
        /// </summary>
        public PEFile Owner { get; }

        /// <summary>
        /// Get or set the name of the section
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Get the name as an 8 byte integer
        /// </summary>
        public ulong NameBytes
        {
            get
            {
                ulong val = 0;
                for (int i = Name.Length - 1; i>=0; i--)
                {
                    val = val << 8 | (byte)Name[i];
                }
                return val;
            }
        }

        /// <summary>
        /// Get the characteristics for this section
        /// </summary>
        public uint Characteristics { get; set; }

        /// <summary>
        /// Gets the base RVA for this section
        /// </summary>
        public uint RVA { get; }

        /// <summary>
        /// The file position of this section
        /// </summary>
        public uint FilePosition { get; }

        /// <summary>
        /// Gets the actual byte size of this section
        /// </summary>
        public uint Size => (uint)(_stream?.Length ?? _bytes.Length);

        /// <summary>
        /// The calculated disk size 
        /// </summary>
        public uint SizeOnDisk => Utils.RoundToAlignment(Size, Owner.WindowsHeader->FileAlignment);

        /// <summary>
        /// The calculates virtual size
        /// </summary>
        public uint VirtualSize => Size;

        MemoryStream _stream;
        byte[] _bytes;
    }
}
