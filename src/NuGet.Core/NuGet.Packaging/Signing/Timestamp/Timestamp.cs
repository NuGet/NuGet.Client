// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Linq;

#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif

namespace NuGet.Packaging.Signing
{
    public sealed class Timestamp
    {
#if IS_DESKTOP

        /// <summary>
        /// Upper limit of Timestamp.
        /// </summary>
        public DateTimeOffset UpperLimit { get; }

        /// <summary>
        /// Lower limit of Timestamp.
        /// </summary>
        public DateTimeOffset LowerLimit { get; }

        /// <summary>
        /// Time timestamp was created by the Time Stamp Authority.
        /// </summary>
        public DateTimeOffset GeneralizedTime { get; }

        /// <summary>
        /// A SignedCms object holding the timestamp and SignerInfo.
        /// </summary>
        public SignedCms SignedCms { get; }

        /// <summary>
        /// SignerInfo for this timestamp.
        /// </summary>
        public SignerInfo SignerInfo => SignedCms.SignerInfos[0];

        /// <summary>
        /// Timestamp token info for this timestamp.
        /// </summary>
        internal Rfc3161TimestampTokenInfo TstInfo { get; }

        /// <summary>
        /// Default constructor. Limits are set to current time.
        /// </summary>
        public Timestamp()
        {
            GeneralizedTime = DateTimeOffset.Now;
            UpperLimit = GeneralizedTime;
            LowerLimit = GeneralizedTime;
        }

        /// <summary>
        /// SignedCms containing a time stamp authority token reponse
        /// </summary>
        /// <param name="timestampCms">SignedCms from Time Stamp Authority</param>
        public Timestamp(SignedCms timestampCms)
        {
            SignedCms = timestampCms ?? throw new ArgumentNullException(nameof(timestampCms));

            if (Rfc3161TimestampVerificationUtility.TryReadTSTInfoFromSignedCms(timestampCms, out var tstInfo))
            {
                try
                {
                    SignedCms.CheckSignature(verifySignatureOnly: true);
                }
                catch (Exception ex)
                {
                    throw new TimestampException(NuGetLogCode.NU3021, Strings.TimestampSignatureValidationFailed, ex);
                }

                TstInfo = tstInfo;
                GeneralizedTime = tstInfo.Timestamp;

                var accuracyInMilliseconds = Rfc3161TimestampVerificationUtility.GetAccuracyInMilliseconds(tstInfo);
                UpperLimit = tstInfo.Timestamp.AddMilliseconds(accuracyInMilliseconds);
                LowerLimit = tstInfo.Timestamp.AddMilliseconds(-accuracyInMilliseconds);
            }
            else
            {
                throw new TimestampException(NuGetLogCode.NU3021, Strings.TimestampSignatureValidationFailed);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="signature"></param>
        /// <param name="allowIgnoreTimestamp"></param>
        /// <param name="allowUnknownRevocation"></param>
        /// <param name="issues"></param>
        /// <returns></returns>
        internal bool Verify(
            Signature signature,
            bool allowIgnoreTimestamp,
            bool allowUnknownRevocation,
            HashAlgorithmName fingerprintAlgorithm,
            List<SignatureLog> issues)
        {
            var treatIssueAsError = !allowIgnoreTimestamp;
            var timestamperCertificate = SignerInfo.Certificate;
            if (timestamperCertificate == null)
            {
                issues?.Add(SignatureLog.Issue(treatIssueAsError, NuGetLogCode.NU3020, Strings.TimestampNoCertificate));
                return false;
            }

            if (VerificationUtility.IsTimestampValid(this, signature, treatIssueAsError, issues, SigningSpecifications.V1))
            {
                issues?.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture, Strings.TimestampValue, GeneralizedTime.LocalDateTime.ToString()) + Environment.NewLine));

                issues?.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture,
                    Strings.VerificationTimestamperCertDisplay,
                    $"{Environment.NewLine}{CertificateUtility.X509Certificate2ToString(timestamperCertificate, fingerprintAlgorithm)}")));

                var certificateExtraStore = SignedCms.Certificates;

                using (var chainHolder = new X509ChainHolder())
                {
                    var chain = chainHolder.Chain;

                    // This flags should only be set for verification scenarios, not signing
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid | X509VerificationFlags.IgnoreCtlNotTimeValid;

                    CertificateChainUtility.SetCertBuildChainPolicy(chain.ChainPolicy, certificateExtraStore, DateTime.Now, NuGetVerificationCertificateType.Timestamp);

                    var chainBuildSucceed = CertificateChainUtility.BuildCertificateChain(chain, timestamperCertificate, out var chainStatusList);

                    issues?.Add(SignatureLog.DetailedLog(CertificateUtility.X509ChainToString(chain, fingerprintAlgorithm)));

                    if (chainBuildSucceed)
                    {
                        return true;
                    }

                    var chainBuildingHasIssues = false;
                    IEnumerable<string> messages;

                    var timestampInvalidCertificateFlags = CertificateChainUtility.DefaultObservedStatusFlags |
                        (X509ChainStatusFlags.Revoked) |
                        (X509ChainStatusFlags.NotTimeValid) |
                        (X509ChainStatusFlags.CtlNotTimeValid);

                    if (CertificateChainUtility.TryGetStatusMessage(chainStatusList, timestampInvalidCertificateFlags, out messages))
                    {
                        foreach (var message in messages)
                        {
                            issues?.Add(SignatureLog.Issue(treatIssueAsError, NuGetLogCode.NU3028, message));
                        }

                        chainBuildingHasIssues = true;
                    }

                    // For all the special cases, chain status list only has unique elements for each chain status flag present
                    // therefore if we are checking for one specific chain status we can use the first of the returned list
                    // if we are combining checks for more than one, then we have to use the whole list.

                    const X509ChainStatusFlags RevocationStatusFlags = X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation;
                    if (CertificateChainUtility.TryGetStatusMessage(chainStatusList, RevocationStatusFlags, out messages))
                    {
                        if (treatIssueAsError)
                        {
                            foreach (var message in messages)
                            {
                                issues?.Add(SignatureLog.Issue(!allowUnknownRevocation, NuGetLogCode.NU3028, message));
                            }
                        }

                        if (!chainBuildingHasIssues && (allowIgnoreTimestamp || allowUnknownRevocation))
                        {
                            return true;
                        }

                        chainBuildingHasIssues = true;
                    }

                    // Debug log any errors
                    issues?.Add(
                        SignatureLog.DebugLog(
                            string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.ErrorInvalidCertificateChain,
                                string.Join(", ", chainStatusList.Select(x => x.Status.ToString())))));
                }
            }

            return false;
        }
#endif
    }
}