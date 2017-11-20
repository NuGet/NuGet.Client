// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// This class is internally used to hold metadata about the signed package archive being verified.
    /// </summary>
    internal sealed class SignedPackageArchiveMetadata
    {
        /// <summary>
        /// Position of the signature file header
        /// </summary>
        internal long SignatureLocalFileHeaderPosition { get; set; }

        /// <summary>
        /// Total size of file entry for signature
        /// This should include size of local file header + encryption header + file data + data descriptir
        /// </summary>
        internal long SignatureFileEntryTotalSize { get; set; }

        /// <summary>
        /// Position of the signature file central directory header
        /// </summary>
        internal long SignatureCentralDirectoryHeaderPosition { get; set; }

        /// <summary>
        /// Size of central directory header for signature file
        /// </summary>
        internal long SignatureCentralDirectoryEntrySize { get; set; }

        /// <summary>
        /// True if the archive is Zip64
        /// </summary>
        internal bool IsZip64 { get; set; }

        /// <summary>
        /// Position of the EOCD record if the archive is Zip64
        /// </summary>
        internal long Zip64EndOfCentralDirectoryRecordPosition { get; set; }

        /// <summary>
        /// Position of the EOCD record
        /// </summary>
        internal long EndOfCentralDirectoryRecordPosition { get; set; }
    }
}
