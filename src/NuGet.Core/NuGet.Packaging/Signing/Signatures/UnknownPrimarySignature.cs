// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if HAS_SIGNING
using System.Security.Cryptography.Pkcs;
#endif

namespace NuGet.Packaging.Signing
{
    public sealed class UnknownPrimarySignature : PrimarySignature
    {
#if HAS_SIGNING
        public UnknownPrimarySignature(SignedCms signedCms)
            : base(signedCms, SignatureType.Unknown)
        {
        }
#endif
    }
}
