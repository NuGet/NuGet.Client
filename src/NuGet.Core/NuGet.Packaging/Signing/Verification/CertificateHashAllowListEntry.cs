// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Packaging.Signing
{
    public class CertificateHashAllowListEntry : VerificationAllowListEntry
    {
        public string CertificateFingerprint { get; }

        public CertificateHashAllowListEntry(VerificationTarget target, string fingerprint)
            : base(target)
        {
            CertificateFingerprint = fingerprint;
        }
    }
}
