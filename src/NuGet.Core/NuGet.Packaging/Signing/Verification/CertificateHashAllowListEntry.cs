// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class CertificateHashAllowListEntry : VerificationAllowListEntry
    {
        public string CertificateFingerprint { get; }

        public HashAlgorithmName FingerprintAlgorithm { get; }

        public CertificateHashAllowListEntry(SignaturePlacement placement, VerificationTarget target, string fingerprint)
            :this(placement, target, fingerprint, HashAlgorithmName.SHA256)
        {
        }

        public CertificateHashAllowListEntry(SignaturePlacement placement, VerificationTarget target, string fingerprint, HashAlgorithmName fingerprintAlgorithm)
           : base(target, placement)
        {
            CertificateFingerprint = fingerprint;
            FingerprintAlgorithm = fingerprintAlgorithm;
        }
    }
}
