// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
#if HAS_SIGNING
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
#endif
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public sealed class RepositoryCountersignature : Signature, IRepositorySignature
    {
#if HAS_SIGNING
        private readonly PrimarySignature _primarySignature;

        public Uri V3ServiceIndexUrl { get; }
        public IReadOnlyList<string> PackageOwners { get; }

        public override string FriendlyName => Strings.RepositoryCountersignatureFriendlyName;

        private RepositoryCountersignature(
            PrimarySignature primarySignature,
            SignerInfo counterSignerInfo,
            Uri v3ServiceIndexUrl,
            IReadOnlyList<string> packageOwners)
            : base(counterSignerInfo, SignatureType.Repository)
        {
            _primarySignature = primarySignature;
            V3ServiceIndexUrl = v3ServiceIndexUrl;
            PackageOwners = packageOwners;
        }

        public static RepositoryCountersignature GetRepositoryCountersignature(PrimarySignature primarySignature)
        {
            if (primarySignature == null)
            {
                throw new ArgumentNullException(nameof(primarySignature));
            }

            var countersignatures = primarySignature.SignerInfo.CounterSignerInfos;
            RepositoryCountersignature repositoryCountersignature = null;

            // Only look for repository countersignatures.
            foreach (var countersignature in countersignatures)
            {
                var countersignatureType = AttributeUtility.GetSignatureType(countersignature.SignedAttributes);

                if (countersignatureType == SignatureType.Repository)
                {
                    if (repositoryCountersignature != null)
                    {
                        throw new SignatureException(NuGetLogCode.NU3032, Strings.Error_NotOneRepositoryCounterSignature);
                    }

                    if (primarySignature.Type == SignatureType.Repository)
                    {
                        throw new SignatureException(NuGetLogCode.NU3033, Strings.Error_RepositorySignatureMustNotHaveARepositoryCountersignature);
                    }

                    var v3ServiceIndexUrl = AttributeUtility.GetNuGetV3ServiceIndexUrl(countersignature.SignedAttributes);
                    var packageOwners = AttributeUtility.GetNuGetPackageOwners(countersignature.SignedAttributes);

                    repositoryCountersignature = new RepositoryCountersignature(
                        primarySignature,
                        countersignature,
                        v3ServiceIndexUrl,
                        packageOwners);
                }
            }

            return repositoryCountersignature;
        }

        public override byte[] GetSignatureValue()
        {
            using (var nativeCms = NativeCms.Decode(_primarySignature.GetBytes()))
            {
                return nativeCms.GetRepositoryCountersignatureSignatureValue();
            }
        }

        protected override void ThrowForInvalidSignature()
        {
            throw new SignatureException(NuGetLogCode.NU3031, Strings.InvalidRepositoryCountersignature);
        }

        public override SignatureVerificationSummary Verify(
            Timestamp timestamp,
            SignatureVerifySettings settings,
            HashAlgorithmName fingerprintAlgorithm,
            X509Certificate2Collection certificateExtraStore)
        {
            var issues = new List<SignatureLog>();
            settings = settings ?? SignatureVerifySettings.Default;

            issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture, Strings.SignatureType, Type.ToString())));
            issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture, Strings.NuGetV3ServiceIndexUrl, V3ServiceIndexUrl.ToString())));

            if (PackageOwners != null)
            {
                issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture, Strings.NuGetPackageOwners, string.Join(", ", PackageOwners))));
            }

            var summary = base.Verify(timestamp, settings, fingerprintAlgorithm, certificateExtraStore);

            return new SignatureVerificationSummary(
                summary.SignatureType,
                summary.Status,
                summary.Flags,
                summary.Timestamp,
                summary.ExpirationTime,
                issues.Concat(summary.Issues));
        }

        internal bool IsRelated(PrimarySignature primarySignature)
        {
            if (primarySignature == null)
            {
                throw new ArgumentNullException(nameof(primarySignature));
            }

            return ReferenceEquals(_primarySignature, primarySignature);
        }
#else
        public static RepositoryCountersignature GetRepositoryCountersignature(PrimarySignature primarySignature)
        {
            throw new NotSupportedException();
        }
#endif
    }
}
