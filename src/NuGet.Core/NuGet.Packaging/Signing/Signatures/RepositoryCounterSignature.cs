// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class RepositoryCounterSignature : Signature, IRepositorySignature
    {
#if IS_DESKTOP
        private readonly Uri _nuGetV3ServiceIndexUrl;
        public Uri NuGetV3ServiceIndexUrl => _nuGetV3ServiceIndexUrl;

        private readonly IReadOnlyList<string> _nuGetPackageOwners;
        public IReadOnlyList<string> NuGetPackageOwners => _nuGetPackageOwners;


        private RepositoryCounterSignature(SignerInfo counterSignerInfo, Uri nuGetV3ServiceIndexUrl)
            : base(counterSignerInfo, SignatureType.Repository)
        {
            _nuGetV3ServiceIndexUrl = nuGetV3ServiceIndexUrl;
            _nuGetPackageOwners = AttributeUtility.GetNuGetPackageOwners(SignerInfo.SignedAttributes);
        }

        public static RepositoryCounterSignature GetRepositoryCounterSignature(PrimarySignature primarySignature)
        {
            var counterSignatures = primarySignature.SignerInfo.CounterSignerInfos;
            var repositoryCounterSignatures = new List<SignerInfo>();
            Uri potentialNuGetV3ServiceIndexUrl = null;
            Uri lastFoundNuGetV3ServiceIndexUrl = null;

            // We only care about the repository countersignatures, not any kind of counter signature
            foreach (var counterSignature in counterSignatures)
            {
                potentialNuGetV3ServiceIndexUrl = null;
                try
                {
                    potentialNuGetV3ServiceIndexUrl = AttributeUtility.GetNuGetV3ServiceIndexUrl(counterSignature.SignedAttributes);
                }
                // This counter signature is not a valid repository countersignature
                catch (SignatureException) { }

                if (potentialNuGetV3ServiceIndexUrl != null)
                {
                    repositoryCounterSignatures.Add(counterSignature);
                    lastFoundNuGetV3ServiceIndexUrl = potentialNuGetV3ServiceIndexUrl;
                }
            }

            if (repositoryCounterSignatures.Count > 1)
            {
                throw new SignatureException(NuGetLogCode.NU3030, Strings.Error_NotOneRepositoryCounterSignature);
            }

            var respoSignature = repositoryCounterSignatures.FirstOrDefault();
            if (respoSignature == null)
            {
                return null;
            }

            return new RepositoryCounterSignature(respoSignature, lastFoundNuGetV3ServiceIndexUrl);
        }

#else
        public static RepositoryCounterSignature GetRepositoryCounterSignature(PrimarySignature primarySignature)
        {
            return null;
        }
#endif
    }
}
