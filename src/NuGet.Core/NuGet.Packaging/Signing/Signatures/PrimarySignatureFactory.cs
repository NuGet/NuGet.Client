// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif

namespace NuGet.Packaging.Signing
{
    public static class PrimarySignatureFactory
    {
#if IS_DESKTOP
        public static PrimarySignature CreateSignature(SignedCms signedCms, SignatureType type)
        {
            switch(type)
            {
                case SignatureType.Author:
                    return new AuthorPrimarySignature(signedCms);
                case SignatureType.Repository:
                    return new RepositoryPrimarySignature(signedCms);
                default:
                    return new UnknownPrimarySignature(signedCms);
            }
        }
#endif
    }
}
