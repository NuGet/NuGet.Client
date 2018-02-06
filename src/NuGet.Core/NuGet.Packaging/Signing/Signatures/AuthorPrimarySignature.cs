// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif

namespace NuGet.Packaging.Signing
{
    public class AuthorPrimarySignature : PrimarySignature
    {
#if IS_DESKTOP

        public AuthorPrimarySignature(SignedCms signedCms)
            : base(signedCms, SignatureType.Author)
        {
            VerifySigningTimeAttribute(SignerInfo);
        }

        private static void VerifySigningTimeAttribute(SignerInfo signerInfo)
        {
            var attribute = signerInfo.SignedAttributes.GetAttributeOrDefault(Oids.SigningTime);

            if (attribute == null)
            {
                ThrowForInvalidAuthorSignature();
            }
        }
#endif
    }
}
