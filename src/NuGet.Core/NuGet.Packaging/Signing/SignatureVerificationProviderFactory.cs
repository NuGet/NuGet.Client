// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public static class SignatureVerificationProviderFactory
    {
        public static IList<ISignatureVerificationProvider> GetDefaultSignatureVerificationProviders()
        {
            return new List<ISignatureVerificationProvider>()
            {
                new IntegrityVerificationProvider(),
                new SignatureTrustAndValidityVerificationProvider()
            };
        }
    }
}
