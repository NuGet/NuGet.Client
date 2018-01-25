// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Packaging.Signing
{
    public class CertificateHashAllowListObject : VerificationAllowListObject
    {
        public string CertificateFingerprint { get; }

        public CertificateHashAllowListObject(VerificationTarget target, string fingerprint)
            : base(target)
        {
            CertificateFingerprint = fingerprint;
        }
    }
}
