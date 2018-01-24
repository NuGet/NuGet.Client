// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Packaging.Signing
{
    public class NuGetSignatureCertificateHashAllowListObject : NuGetSignatureAllowListObject
    {
        public string CertificateFingerprint { get; }

        public NuGetSignatureCertificateHashAllowListObject(NuGetSignatureVerificationTarget target, string fingerprint)
            : base(target)
        {
            CertificateFingerprint = fingerprint;
        }
    }
}
