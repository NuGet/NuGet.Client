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
    public sealed class RepositoryCountersignature : Signature, IRepositorySignature
    {
#if IS_DESKTOP
        public Uri V3ServiceIndexUrl { get; }
        public IReadOnlyList<string> PackageOwners { get; }

        private RepositoryCountersignature(SignerInfo counterSignerInfo, Uri v3ServiceIndexUrl, IReadOnlyList<string> packageOwners)
            : base(counterSignerInfo, SignatureType.Repository)
        {
            V3ServiceIndexUrl = v3ServiceIndexUrl;
            PackageOwners = packageOwners;
        }

        public static RepositoryCountersignature GetRepositoryCounterSignature(PrimarySignature primarySignature)
        {
            if (primarySignature.Type == SignatureType.Repository)
            {
                throw new SignatureException(NuGetLogCode.NU3033, Strings.Error_RepositorySignatureShouldNotHaveARepositoryCountersignature);
            }

            var counterSignatures = primarySignature.SignerInfo.CounterSignerInfos;
            RepositoryCountersignature repositoryCountersignature = null;

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
                    var v3ServiceIndexUrl = AttributeUtility.GetNuGetV3ServiceIndexUrl(counterSignature.SignedAttributes);
                    var packageOwners = AttributeUtility.GetNuGetPackageOwners(counterSignature.SignedAttributes);
                    repositoryCountersignature = new RepositoryCountersignature(counterSignature, v3ServiceIndexUrl, packageOwners);
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
            SignedPackageVerifierSettings settings,
            HashAlgorithmName fingerprintAlgorithm,
            X509Certificate2Collection certificateExtraStore,
            List<SignatureLog> issues)
        {
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }
            settings = settings ?? SignedPackageVerifierSettings.Default;

            issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture, Strings.SignatureType, Type.ToString())));
            issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture, Strings.NuGetV3ServiceIndexUrl, V3ServiceIndexUrl.ToString())));
            if (PackageOwners != null)
            {
                issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture, Strings.NuGetPackageOwners, string.Join(", ", PackageOwners))));
            }
            return base.Verify(timestamp, settings, fingerprintAlgorithm, certificateExtraStore, issues);
        }
#else
        public static RepositoryCountersignature GetRepositoryCounterSignature(PrimarySignature primarySignature)
        {
            return null;
        }
#endif
    }
}
