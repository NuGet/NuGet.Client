// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if HAS_SIGNING
using System.Security.Cryptography.Pkcs;
using NuGet.Common;
#endif

namespace NuGet.Packaging.Signing
{
    public static class PrimarySignatureFactory
    {
#if HAS_SIGNING
        public static PrimarySignature CreateSignature(SignedCms signedCms)
        {
            var signatureType = AttributeUtility.GetSignatureType(signedCms.SignerInfos[0].SignedAttributes);

            switch (signatureType)
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
