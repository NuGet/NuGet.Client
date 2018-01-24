// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Packaging.Signing
{
    public static class SignatureVerificationProviderFactory
    {
        public static IEnumerable<ISignatureVerificationProvider> GetSignatureVerificationProviders(SignatureVerificationProviderArgs args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            return new List<ISignatureVerificationProvider>()
            {
                new IntegrityVerificationProvider(),
                new SignatureTrustAndValidityVerificationProvider(args.FingerprintAlgorithm),
                new AllowListVerificationProvider(args.FingerprintAlgorithm, args.AllowList)
            };
        }
    }
}
