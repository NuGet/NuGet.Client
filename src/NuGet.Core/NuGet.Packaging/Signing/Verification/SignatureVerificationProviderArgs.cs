// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class SignatureVerificationProviderArgs
    {
        public IEnumerable<NuGetSignatureWhitelistObject> Whitelist { get; }

        public HashAlgorithmName FingerprintAlgorithm { get; }

        public SignatureVerificationProviderArgs()
            : this(HashAlgorithmName.SHA256)
        {
        }

        public SignatureVerificationProviderArgs(HashAlgorithmName fingerprintAlgorithm)
        {
            FingerprintAlgorithm = fingerprintAlgorithm;
        }

        public SignatureVerificationProviderArgs(IEnumerable<NuGetSignatureWhitelistObject> whitelist, HashAlgorithmName fingerprintAlgorithm)
        {
            Whitelist = whitelist ?? throw new ArgumentNullException(nameof(whitelist));
            FingerprintAlgorithm = fingerprintAlgorithm;
        }
    }
}
