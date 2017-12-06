// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// This class is used to hold metatdata about the central directory archive structure
    /// </summary>
    public class CentralDirectoryMetadata
    {
        /// <summary>
        /// Position of the corresponding central directory header
        /// </summary>
        public long Position { get; set; }

        /// <summary>
        /// Offset to the corresponding file header
        /// </summary>
        public long OffsetToFileHeader { get; set; }

        /// <summary>
        /// Total size of corresponding file entry
        /// This should include size of local file header + encryption header + file data + data descriptor
        /// </summary>
        public long FileEntryTotalSize { get; set; }

        /// <summary>
        /// Filename for the central directory header
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        /// Size of central directory header
        /// </summary>
        public long HeaderSize { get; set; }

        /// <summary>
        /// Value used to identify how much the position of the OffsetToFileHeader property will change by
        /// the presence of a signature file
        /// </summary>
        public long ChangeInOffset { get; set; }

        /// <summary>
        /// Index in which the central directory record was read from the archive.
        /// This index represents the order of de central directory record as it is in the file.
        /// </summary>
        public int IndexInRecords { get; set; }
    }

    /// <summary>
    /// This class is used to hold metadata about the signed package archive being verified.
    /// </summary>
    public class SignedPackageArchiveMetadata
    {

        /// <summary>
        /// List of central directory metadata ordered by the same order the central directory records are in the archive
        /// </summary>
        public List<CentralDirectoryMetadata> CentralDirectoryRecords { get; set; }

        /// <summary>
        /// Position in the archive where the file headers start. Should tipically be 0
        /// </summary>
        public long StartOfFileHeaders { get; set; }

        /// <summary>
        /// Position in the archive where the central directory starts
        /// </summary>
        public long StartOfCentralDirectory { get; set; }


        /// <summary>
        /// Position in the archive where the central directory starts
        /// </summary>
        public long EndOfCentralDirectory { get; set; }

        /// <summary>
        /// Index of the signature central direcotry record.
        /// If the CentralDirectories list is ordered by IndexInRecords this index indicates the position on the list for the signature.
        /// </summary>
        public int SignatureIndexInRecords { get; set; }

        /// <summary>
        /// Position of the EOCD record
        /// </summary>
        public long EndOfCentralDirectoryRecordPosition { get; set; }
    }
}
