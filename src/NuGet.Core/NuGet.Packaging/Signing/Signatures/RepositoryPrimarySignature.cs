// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif

namespace NuGet.Packaging.Signing
{
    public class RepositoryPrimarySignature : PrimarySignature, IRepositorySignature
    {
#if IS_DESKTOP
        private readonly Uri _nuGetV3ServiceIndexUrl;
        public Uri NuGetV3ServiceIndexUrl => _nuGetV3ServiceIndexUrl;

        private readonly IReadOnlyList<string> _nuGetPackageOwners;
        public IReadOnlyList<string> NuGetPackageOwners => _nuGetPackageOwners;


        public RepositoryPrimarySignature(SignedCms signedCms)
            : base(signedCms, SignatureType.Repository)
        {
            _nuGetV3ServiceIndexUrl = AttributeUtility.GetNuGetV3ServiceIndexUrl(SignerInfo.SignedAttributes);
            _nuGetPackageOwners = AttributeUtility.GetNuGetPackageOwners(SignerInfo.SignedAttributes);
        }
#endif
    }
}
