// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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

            if (CertificateUtility.HasExtendedKeyUsage(request.Certificate, Oids.LifetimeSignerEku))
            {
                throw new SignatureException(NuGetLogCode.NU3015, Strings.ErrorCertificateHasLifetimeSignerEKU);
            }

            if (CertificateUtility.IsCertificateValidityPeriodInTheFuture(request.Certificate))
            {
                throw new SignatureException(NuGetLogCode.NU3017, Strings.SignatureNotYetValid);
            }

            request.BuildSigningCertificateChainOnce();
        }

#if IS_DESKTOP
        internal static CryptographicAttributeObjectCollection GetSignedAttributes(
            SignPackageRequest request,
            IReadOnlyList<X509Certificate2> chainList)
        {
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
#endif
    }
}