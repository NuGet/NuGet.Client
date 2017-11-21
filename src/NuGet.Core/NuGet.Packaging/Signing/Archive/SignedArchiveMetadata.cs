// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Packaging.Signing
{
    internal sealed class SignedArchiveMetadata
    {
        public long SignatureLocalFileHeaderPosition { get; set; }

        public long FileHeaderExtraSize { get; set; }

        public uint SignatureFileCompressedSize { get; set; }

        public bool SignatureHasDataDescriptor { get; set; }

        public long SignatureCentralDirectoryHeaderPosition { get; set; }

        public bool IsZip64 { get; set; }

        public long Zip64EndOfCentralDirectoryRecordPosition { get; set; }

        public long Zip64EndOfCentralDirectoryLocatorPosition { get; set; }

        public long EndOfCentralDirectoryRecordPosition { get; set; }
    }
}
