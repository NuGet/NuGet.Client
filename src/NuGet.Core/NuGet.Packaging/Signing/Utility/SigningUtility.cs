// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
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
        public static void VerifyCertificate(X509Certificate2 certificate)
        {
            if (!CertificateUtility.IsSignatureAlgorithmSupported(certificate))
            {
                throw new SignatureException(NuGetLogCode.NU3013, Strings.SigningCertificateHasUnsupportedSignatureAlgorithm);
            }

            if (!CertificateUtility.IsCertificatePublicKeyValid(certificate))
            {
                throw new SignatureException(NuGetLogCode.NU3014, Strings.SigningCertificateFailsPublicKeyLengthRequirement);
            }

            if (CertificateUtility.HasExtendedKeyUsage(certificate, Oids.LifetimeSignerEkuOid))
            {
                throw new SignatureException(NuGetLogCode.NU3015, Strings.ErrorCertificateHasLifetimeSignerEKU);
            }

            if (CertificateUtility.IsCertificateValidityPeriodInTheFuture(certificate))
            {
                throw new SignatureException(NuGetLogCode.NU3017, Strings.SignatureNotYetValid);
            }
        }
  

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

            request.BuildSigningCertificateChainOnce();
        }

#if IS_DESKTOP
        public static CryptographicAttributeObjectCollection GetSignedAttributes(
            SignPackageRequest request,
            IReadOnlyList<X509Certificate2> chainList)
        {
            var attributes = new CryptographicAttributeObjectCollection
            {
                new Pkcs9SigningTime()
            };

            if (request.SignatureType != SignatureType.Unknown)
            {
                // Add signature type if set.
                attributes.Add(AttributeUtility.GetCommitmentTypeIndication(request.SignatureType));
            }

            // Add the full chain of certificate hashes
            attributes.Add(AttributeUtility.GetSigningCertificateV2(chainList, request.SignatureHashAlgorithm));

            return attributes;
        }
#endif
    }
}