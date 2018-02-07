// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
#endif
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public sealed class RepositoryCounterSignature : Signature, IRepositorySignature
    {
#if IS_DESKTOP
        public Uri NuGetV3ServiceIndexUrl { get; }
        public IReadOnlyList<string> NuGetPackageOwners { get; }

        private RepositoryCounterSignature(SignerInfo counterSignerInfo, Uri nuGetV3ServiceIndexUrl)
            : base(counterSignerInfo, SignatureType.Repository)
        {
            NuGetV3ServiceIndexUrl = nuGetV3ServiceIndexUrl;
            NuGetPackageOwners = AttributeUtility.GetNuGetPackageOwners(SignerInfo.SignedAttributes);
        }

        public static RepositoryCounterSignature GetRepositoryCounterSignature(PrimarySignature primarySignature)
        {
            if (primarySignature.Type == SignatureType.Repository)
            {
                throw new SignatureException(NuGetLogCode.NU3033, Strings.Error_RepositorySignatureShouldNotHaveARepositoryCountersignature);
            }

            var counterSignatures = primarySignature.SignerInfo.CounterSignerInfos;
            RepositoryCounterSignature repositoryCountersignature = null;

            // We only care about the repository countersignatures, not any kind of counter signature
            foreach (var counterSignature in counterSignatures)
            {
                var countersignatureType = AttributeUtility.GetSignatureType(counterSignature.SignedAttributes);
                if (countersignatureType == SignatureType.Repository)
                {
                    if (repositoryCountersignature != null)
                    {
                        throw new SignatureException(NuGetLogCode.NU3032, Strings.Error_NotOneRepositoryCounterSignature);
                    }
                    var nuGetV3ServiceIndexUrl = AttributeUtility.GetNuGetV3ServiceIndexUrl(counterSignature.SignedAttributes);
                    repositoryCountersignature = new RepositoryCounterSignature(counterSignature, nuGetV3ServiceIndexUrl);
                }
            }

            return repositoryCountersignature;
        }

        public override byte[] GetSignatureHashValue(HashAlgorithmName hashAlgorithm)
        {
            // TODO: figure out how to get the signature hash value for a countersignature
            return new byte[] { };
        }

        protected override void ThrowForInvalidSignature()
        {
            ThrowForInvalidRepositoryCounterSignature();
        }

        private static void ThrowForInvalidRepositoryCounterSignature()
        {
            throw new SignatureException(NuGetLogCode.NU3031, Strings.InvalidRepositoryCounterSignature);
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
#else
        public static RepositoryCounterSignature GetRepositoryCounterSignature(PrimarySignature primarySignature)
        {
            return null;
        }
#endif
    }
}
