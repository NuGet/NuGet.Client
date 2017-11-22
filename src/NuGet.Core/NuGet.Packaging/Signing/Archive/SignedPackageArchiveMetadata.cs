// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

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
        public long SignatureLocalFileHeaderPosition { get; set; }

        /// <summary>
        /// Size of extra fields in the signature file header
        /// </summary>
        public long SignatureFileHeaderExtraSize { get; set; }

        /// <summary>
        /// Size of the signature file
        /// </summary>
        public uint SignatureFileCompressedSize { get; set; }

        /// <summary>
        /// True if the signature file has data descriptors
        /// </summary>
        public bool SignatureHasDataDescriptor { get; set; }

        /// <summary>
        /// Position of the signature file EOCD header
        /// </summary>
        public long SignatureCentralDirectoryHeaderPosition { get; set; }

        /// <summary>
        /// True if the archive is Zip64
        /// </summary>
        public bool IsZip64 { get; set; }

        /// <summary>
        /// Position of the EOCD record if the archive is Zip64
        /// </summary>
        public long Zip64EndOfCentralDirectoryRecordPosition { get; set; }

        /// <summary>
        /// Position of the EOCD Locator if the archive is Zip64
        /// </summary>
        public long Zip64EndOfCentralDirectoryLocatorPosition { get; set; }

        /// <summary>
        /// Position of the EOCD record
        /// </summary>
        public long EndOfCentralDirectoryRecordPosition { get; set; }
    }
}
