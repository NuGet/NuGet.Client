// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
#endif
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public sealed class RepositoryPrimarySignature : PrimarySignature, IRepositorySignature
    {
#if IS_DESKTOP
        public Uri NuGetV3ServiceIndexUrl { get; }
        public IReadOnlyList<string> NuGetPackageOwners { get; }

        public RepositoryPrimarySignature(SignedCms signedCms)
            : base(signedCms, SignatureType.Repository)
        {
            NuGetV3ServiceIndexUrl = AttributeUtility.GetNuGetV3ServiceIndexUrl(SignerInfo.SignedAttributes);
            NuGetPackageOwners = AttributeUtility.GetNuGetPackageOwners(SignerInfo.SignedAttributes);
        }

        internal override SignatureVerificationStatus Verify(
            Timestamp timestamp,
            bool allowUntrusted,
            bool allowUntrustedSelfSignedCertificate,
            bool allowUnknownRevocation,
            HashAlgorithmName fingerprintAlgorithm,
            X509Certificate2Collection certificateExtraStore,
            List<SignatureLog> issues)
        {
            issues?.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture, Strings.SignatureType, Type.ToString())));
            issues?.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture, Strings.NuGetV3ServiceIndexUrl, NuGetV3ServiceIndexUrl.ToString())));
            if (NuGetPackageOwners != null)
            {
                issues?.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture, Strings.NuGetPackageOwners, string.Join(", ", NuGetPackageOwners))));
            }
            return base.Verify(timestamp, allowUntrusted, allowUntrustedSelfSignedCertificate, allowUnknownRevocation, fingerprintAlgorithm, certificateExtraStore, issues);
        }
#endif
    }
}
