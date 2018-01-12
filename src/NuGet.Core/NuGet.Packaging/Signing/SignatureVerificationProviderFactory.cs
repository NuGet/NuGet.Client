// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Packaging.Signing
{
    public static class SignatureVerificationProviderFactory
    {
        public static IEnumerable<ISignatureVerificationProvider> GetSignatureVerificationProviders(SignatureVerificationProviderArgs args)
        {
            return new List<ISignatureVerificationProvider>()
            {
                new IntegrityVerificationProvider(),
                new SignatureTrustAndValidityVerificationProvider(args.FingerprintAlgorithm),
                new WhitelistVerificationProvider(args.FingerprintAlgorithm, args.Whitelist)
            };
        }
    }
}
