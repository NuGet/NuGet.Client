// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif
using System.Security.Cryptography.X509Certificates;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Utility methods for signing.
    /// </summary>
    public static class SigningUtility
    {
        public static void Verify(SignPackageRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!CertificateUtility.IsSignatureAlgorithmSupported(request.Certificate))
            {
                throw new SignatureException(NuGetLogCode.NU3013, Strings.SigningCertificateHasUnsupportedSignatureAlgorithm);
            }

            if (!CertificateUtility.IsCertificatePublicKeyValid(request.Certificate))
            {
                throw new SignatureException(NuGetLogCode.NU3014, Strings.SigningCertificateFailsPublicKeyLengthRequirement);
            }

            if (CertificateUtility.HasExtendedKeyUsage(request.Certificate, Oids.LifetimeSigningEku))
            {
                throw new SignatureException(NuGetLogCode.NU3015, Strings.ErrorCertificateHasLifetimeSigningEKU);
            }

            if (CertificateUtility.IsCertificateValidityPeriodInTheFuture(request.Certificate))
            {
                throw new SignatureException(NuGetLogCode.NU3017, Strings.SignatureNotYetValid);
            }

            request.BuildSigningCertificateChainOnce();
        }

#if IS_DESKTOP
        public static CryptographicAttributeObjectCollection CreateSignedAttributes(
            SignPackageRequest request,
            IReadOnlyList<X509Certificate2> chainList)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (chainList == null || chainList.Count == 0)
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(chainList));
            }

            var attributes = new CryptographicAttributeObjectCollection
            {
                new Pkcs9SigningTime()
            };

            if (request.SignatureType != SignatureType.Unknown)
            {
                // Add signature type if set.
                attributes.Add(AttributeUtility.CreateCommitmentTypeIndication(request.SignatureType));
            }

            attributes.Add(AttributeUtility.CreateSigningCertificateV2(chainList[0], request.SignatureHashAlgorithm));

            return attributes;
        }

        public static CryptographicAttributeObjectCollection CreateSignedAttributesForRepository(
            SignPackageRequest request,
            IReadOnlyList<X509Certificate2> chainList,
            Uri v3ServiceIndexUrl,
            IReadOnlyList<string> packageOwners)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (chainList == null || chainList.Count == 0)
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(chainList));
            }

            if (v3ServiceIndexUrl == null)
            {
                throw new ArgumentNullException(nameof(v3ServiceIndexUrl));
            }

            if (!v3ServiceIndexUrl.IsAbsoluteUri)
            {
                throw new ArgumentException(Strings.InvalidUrl, nameof(v3ServiceIndexUrl));
            }

            if (v3ServiceIndexUrl.Scheme != "https")
            {
                throw new ArgumentException(Strings.InvalidUrl, nameof(v3ServiceIndexUrl));
            }

            if (packageOwners != null && packageOwners.Any(packageOwner => string.IsNullOrWhiteSpace(packageOwner)))
            {
                throw new ArgumentException(Strings.NuGetPackageOwnersInvalidValue, nameof(packageOwners));
            }

            var attributes = CreateSignedAttributes(request, chainList);

            attributes.Add(AttributeUtility.CreateNuGetV3ServiceIndexUrl(v3ServiceIndexUrl));

            if (packageOwners?.Count > 0)
            {
                attributes.Add(AttributeUtility.CreateNuGetPackageOwners(packageOwners));
            }

            return attributes;
        }
#endif
    }
}