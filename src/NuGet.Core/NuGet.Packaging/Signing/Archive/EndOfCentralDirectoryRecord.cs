// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;

// ZIP specification: http://www.pkware.com/documents/casestudies/APPNOTE.TXT

namespace NuGet.Packaging.Signing
{
    internal sealed class EndOfCentralDirectoryRecord
    {
        internal const uint Signature = 0x06054b50;

        internal ushort NumberOfThisDisk { get; private set; }
        internal ushort NumberOfTheDiskWithTheStartOfTheCentralDirectory { get; private set; }
        internal ushort CountOfEntriesInCentralDirectoryOnThisDisk { get; private set; }
        internal ushort CountOfEntriesInCentralDirectory { get; private set; }
        internal uint SizeOfCentralDirectory { get; private set; }
        internal uint OffsetOfStartOfCentralDirectory { get; private set; }
        internal ushort FileCommentLength { get; private set; }
        internal byte[] FileComment { get; private set; }

        // This property is not part of the ZIP specification.
        internal long OffsetFromStart { get; private set; }

        internal static EndOfCentralDirectoryRecord Read(BinaryReader reader)
        {
            SeekToEndOfCentralDirectoryRecord(reader);

            var header = new EndOfCentralDirectoryRecord();

            header.OffsetFromStart = reader.BaseStream.Position;

            reader.ReadUInt32(); // Read and discard the byte signature.

            header.NumberOfThisDisk = reader.ReadUInt16();
            header.NumberOfTheDiskWithTheStartOfTheCentralDirectory = reader.ReadUInt16();
            header.CountOfEntriesInCentralDirectoryOnThisDisk = reader.ReadUInt16();
            header.CountOfEntriesInCentralDirectory = reader.ReadUInt16();
            header.SizeOfCentralDirectory = reader.ReadUInt32();
            header.OffsetOfStartOfCentralDirectory = reader.ReadUInt32();
            header.FileCommentLength = reader.ReadUInt16();
            header.FileComment = reader.ReadBytes(header.FileCommentLength);

            return header;
        }

        private static void SeekToEndOfCentralDirectoryRecord(BinaryReader reader)
        {
            var byteSignature = BitConverter.GetBytes(Signature);
            var length = reader.BaseStream.Length;

            if (length < byteSignature.Length)
            {
                ThrowByteSignatureNotFoundException(byteSignature);
            }

            const int DefaultBufferSize = 4096;

            var bufferSize = (int)Math.Min(length, DefaultBufferSize);
            var matchingByteCount = 0;

            // Read backwards from the end of stream in adjacent, non-overlapping chunks of size "bufferSize",
            // and search backwards for the byte signature.
            // Note:  position is 0-based, while bufferSize is 1-based.
            for (var position = length - bufferSize; position >= 0; position -= bufferSize)
            {
                reader.BaseStream.Position = position;

                var buffer = reader.ReadBytes(bufferSize);

                for (var i = buffer.Length - 1; i >= 0; --i)
                {
                    if (buffer[i] == byteSignature[byteSignature.Length - matchingByteCount - 1])
                    {
                        ++matchingByteCount;

                        if (matchingByteCount == byteSignature.Length)
                        {
                            reader.BaseStream.Position = position + i;

                            return;
                        }
                    }
                    else
                    {
                        matchingByteCount = 0;
                    }
                }

                bufferSize = (int)Math.Min(position, bufferSize);

                if (bufferSize == 0)
                {
                    break;
                }
            }

            ThrowByteSignatureNotFoundException(byteSignature);
        }

        private static void ThrowByteSignatureNotFoundException(byte[] signature)
        {
            throw new InvalidDataException(
                string.Format(CultureInfo.CurrentCulture,
                Strings.ErrorByteSignatureNotFound,
#if NETCOREAPP
                BitConverter.ToString(signature).Replace("-", "", StringComparison.Ordinal)));
#else
                BitConverter.ToString(signature).Replace("-", "")));
#endif
        }
    }
}
