// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Packaging.Signing
{
    public class NuGetSignatureWhitelistObject
    {
        public string CertificateFingerprint { get; }

        public NuGetSignatureWhitelistObjectType Type { get; }

        public NuGetSignatureWhitelistObject(NuGetSignatureWhitelistObjectType type, string fingerprint)
        {
            Type = type;
            CertificateFingerprint = fingerprint;
        }

        public NuGetSignatureWhitelistObject(string fingerprint)
        {
            Type = NuGetSignatureWhitelistObjectType.Any;
            CertificateFingerprint = fingerprint;
        }
    }
}
