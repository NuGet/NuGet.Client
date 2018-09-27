// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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

        public override bool Equals(object obj)
        {
            if (obj is CertificateHashAllowListEntry hashEntry)
            {
                return string.Equals(Fingerprint, hashEntry.Fingerprint, StringComparison.Ordinal);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Fingerprint.GetHashCode();
        }
    }
}
