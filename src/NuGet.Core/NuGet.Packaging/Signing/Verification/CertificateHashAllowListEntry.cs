// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class CertificateHashAllowListEntry : VerificationAllowListEntry
    {
        public string Fingerprint { get; }

        public HashAlgorithmName FingerprintAlgorithm { get; }

        public CertificateHashAllowListEntry(VerificationTarget target, SignaturePlacement placement, string fingerprint, HashAlgorithmName algorithm)
            : base(target, placement)
        {
            Fingerprint = fingerprint;
            FingerprintAlgorithm = algorithm;
        }
    }
}
