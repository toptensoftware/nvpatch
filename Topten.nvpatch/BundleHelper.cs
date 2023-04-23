// Portions of the below code are taken from the .NET runtime's source code.
// Please see: https://github.com/dotnet/runtime/tree/main/src/installer/managed/Microsoft.NET.HostModel/

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.IO;

namespace nvpatch
{
    static class BundleHelper
    {
        const int ManifestAddressLength = sizeof(long);
        // The preceding 8 bytes to this signature contain the address of the
        // bundle manifest.
        static readonly byte[] BundleHeaderSignature = {
            // 32 bytes represent the bundle signature: SHA-256 for ".net core bundle"
            0x8b, 0x12, 0x02, 0xb9, 0x6a, 0x61, 0x20, 0x38,
            0x72, 0x7b, 0x93, 0x02, 0x14, 0xd7, 0xa0, 0x32,
            0x13, 0xf5, 0xb9, 0xe6, 0xef, 0xae, 0x33, 0x18,
            0xee, 0x3b, 0x2d, 0xce, 0x24, 0xb3, 0x6a, 0xae
        };

        public static void CheckForAndUpdateManifest(byte[] inputBytes, FileStream outputStream, long offset)
        {
            var signaturePosition = Utils.KMPSearch(BundleHeaderSignature, inputBytes);
            if (signaturePosition < 0)
                return; // Nothing to do, no bundle marker found.

            var manifestPointerPosition = signaturePosition - ManifestAddressLength;

            var manifestPosition = BitConverter.ToInt64(inputBytes[manifestPointerPosition..]);
            if (manifestPosition == 0)
                return; // Nothing to do, this .NET executable has no bundle.

            // Update the position of the manifest in the output.
            outputStream.Position = manifestPointerPosition;
            outputStream.Write(BitConverter.GetBytes(manifestPosition + offset));

            /* The below code is a rough implementation of the
             * .NET Bundle Manifest format, which contains offsets
             * to files stored after the "AppHost" PE file. Because
             * we've just added more content to the PE file, we need
             * to update the bundle manifest to reflect the new locations
             * of these files, otherwise the AppHost will fail
             * to run the .NET application correctly.
             */

            using var inputStream = new MemoryStream(inputBytes);
            using var reader = new BinaryReader(inputStream);
            inputStream.Position = manifestPosition;

            void ReadInt64OffsetAndUpdate()
            {
                outputStream.Position = inputStream.Position + offset;
                var readValue = reader.ReadInt64();
                if (readValue > 0)
                    outputStream.Write(BitConverter.GetBytes(readValue + offset));
            }

            var majorVersion = reader.ReadUInt32();
            _ = reader.ReadUInt32(); // Minor version.
            var fileCount = reader.ReadInt32();
            _ = reader.ReadString(); // Bundle ID

            if (majorVersion >= 2)
            {
                ReadInt64OffsetAndUpdate(); // depsJsonOffset
                _ = reader.ReadInt64(); // depsJsonSize

                ReadInt64OffsetAndUpdate(); // runtimeConfigJsonOffset
                _ = reader.ReadInt64(); // runtimeConfigJsonSize

                _ = reader.ReadUInt64(); // flags
            }

            for (var i = 0; i < fileCount; i++)
            {
                ReadInt64OffsetAndUpdate(); // fileOffset
                _ = reader.ReadInt64(); // fileSize

                if (majorVersion >= 6)
                    _ = reader.ReadInt64(); // compressedSize

                _ = reader.ReadByte(); // type
                _ = reader.ReadString(); // path
            }
        }
    }
}
