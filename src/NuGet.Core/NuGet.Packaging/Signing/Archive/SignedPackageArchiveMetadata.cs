// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Packaging.Signing
{
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
