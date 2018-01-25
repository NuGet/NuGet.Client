// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class SignatureVerificationProviderArgs
    {
        public IList<VerificationAllowListEntry> AllowList { get; }

        public HashAlgorithmName FingerprintAlgorithm { get; }

        public SignatureVerificationProviderArgs()
            : this(HashAlgorithmName.SHA256)
        {
        }

        public SignatureVerificationProviderArgs(HashAlgorithmName fingerprintAlgorithm)
        {
            FingerprintAlgorithm = fingerprintAlgorithm;
            AllowList = new List<VerificationAllowListEntry>();
        }

        public SignatureVerificationProviderArgs(IList<VerificationAllowListEntry> allowList, HashAlgorithmName fingerprintAlgorithm)
        {
            AllowList = allowList ?? throw new ArgumentNullException(nameof(allowList));
            FingerprintAlgorithm = fingerprintAlgorithm;
        }
    }
}
