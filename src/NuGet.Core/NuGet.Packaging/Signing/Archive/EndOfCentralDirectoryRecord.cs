// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
            reader.BaseStream.Seek(offset: -22, origin: SeekOrigin.End);

            SignedPackageArchiveIOUtility.SeekReaderBackwardToMatchByteSignature(reader, BitConverter.GetBytes(Signature));

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
    }
}