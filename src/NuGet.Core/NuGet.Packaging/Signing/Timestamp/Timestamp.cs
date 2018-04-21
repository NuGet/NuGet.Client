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
                    throw new TimestampException(NuGetLogCode.NU3021, Strings.VerifyError_TimestampSignatureValidationFailed, ex);
                }

                TstInfo = tstInfo;
                GeneralizedTime = tstInfo.Timestamp;

                var accuracyInMilliseconds = Rfc3161TimestampVerificationUtility.GetAccuracyInMilliseconds(tstInfo);
                UpperLimit = tstInfo.Timestamp.AddMilliseconds(accuracyInMilliseconds);
                LowerLimit = tstInfo.Timestamp.AddMilliseconds(-accuracyInMilliseconds);
            }
            else
            {
                throw new TimestampException(NuGetLogCode.NU3021, Strings.VerifyError_TimestampSignatureValidationFailed);
            }
        }

        /// <summary>
        /// Verify if the timestamp object meets the specification requirements.
        /// </summary>
        /// <param name="signature">Signature which this timestamp is for.</param>
        /// <param name="allowIgnoreTimestamp">Setting that tells if a timestamp can be ignored if it doesn't meet the requirements. Used to know if warnings or errors should be logged for an issue.</param>
        /// <param name="allowUnknownRevocation">Setting that tells if unkown revocation is valid when building the chain.</param>
        /// <param name="issues">List of log messages.</param>
        /// <returns>true if the timestamp meets the requierements, false otherwise.</returns>
        internal SignatureVerificationStatusFlags Verify(
            Signature signature,
            SignedPackageVerifierSettings settings,
            HashAlgorithmName fingerprintAlgorithm,
            List<SignatureLog> issues)
        {
            settings = settings ?? SignedPackageVerifierSettings.GetDefault();
            var flags = SignatureVerificationStatusFlags.NoErrors;

            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }

            var treatIssueAsError = !settings.AllowIgnoreTimestamp;
            var timestamperCertificate = SignerInfo.Certificate;
            if (timestamperCertificate == null)
            {
                flags |= SignatureVerificationStatusFlags.NoCertificate;

                issues.Add(SignatureLog.Issue(treatIssueAsError, NuGetLogCode.NU3020, string.Format(CultureInfo.CurrentCulture, Strings.VerifyError_TimestampNoCertificate, signature.FriendlyName)));
                return flags;
            }

            flags |= VerificationUtility.ValidateTimestamp(this, signature, treatIssueAsError, issues, SigningSpecifications.V1);
            if (flags == SignatureVerificationStatusFlags.NoErrors)
            {
                issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture, Strings.TimestampValue, GeneralizedTime.LocalDateTime.ToString()) + Environment.NewLine));

                issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture,
                    Strings.VerificationTimestamperCertDisplay,
                    signature.FriendlyName,
                    $"{Environment.NewLine}{CertificateUtility.X509Certificate2ToString(timestamperCertificate, fingerprintAlgorithm)}")));

                var certificateExtraStore = SignedCms.Certificates;

                using (var chainHolder = new X509ChainHolder())
                {
                    var chain = chainHolder.Chain;

                    // This flags should only be set for verification scenarios, not signing
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid | X509VerificationFlags.IgnoreCtlNotTimeValid;

                    CertificateChainUtility.SetCertBuildChainPolicy(chain.ChainPolicy, certificateExtraStore, DateTime.Now, CertificateType.Timestamp);

                    var chainBuildSucceed = CertificateChainUtility.BuildCertificateChain(chain, timestamperCertificate, out var chainStatusList);
                    issues.Add(SignatureLog.DetailedLog(CertificateUtility.X509ChainToString(chain, fingerprintAlgorithm)));

                    if (chainBuildSucceed)
                    {
                        return flags;
                    }

                    var chainBuildingHasIssues = false;
                    IEnumerable<string> messages;

                    var timestampInvalidCertificateFlags = CertificateChainUtility.DefaultObservedStatusFlags |
                        (X509ChainStatusFlags.NotTimeValid) |
                        (X509ChainStatusFlags.CtlNotTimeValid);

                    if (CertificateChainUtility.TryGetStatusMessage(chainStatusList, timestampInvalidCertificateFlags, out messages))
                    {
                        foreach (var message in messages)
                        {
                            issues.Add(SignatureLog.Issue(treatIssueAsError, NuGetLogCode.NU3028, string.Format(CultureInfo.CurrentCulture, Strings.VerifyError_TimestampVerifyChainBuildingIssue, signature.FriendlyName, message)));
                        }

                        flags |= SignatureVerificationStatusFlags.ChainBuildingFailure;
                        chainBuildingHasIssues = true;
                    }

                    // For all the special cases, chain status list only has unique elements for each chain status flag present
                    // therefore if we are checking for one specific chain status we can use the first of the returned list
                    // if we are combining checks for more than one, then we have to use the whole list.

                    if (CertificateChainUtility.TryGetStatusMessage(chainStatusList, X509ChainStatusFlags.UntrustedRoot, out messages))
                    {
                        issues.Add(SignatureLog.Error(NuGetLogCode.NU3028, string.Format(CultureInfo.CurrentCulture, Strings.VerifyError_TimestampVerifyChainBuildingIssue, signature.FriendlyName, messages.First())));

                        flags |= SignatureVerificationStatusFlags.UntrustedRoot;
                        chainBuildingHasIssues = true;
                    }

                    if (CertificateChainUtility.TryGetStatusMessage(chainStatusList, X509ChainStatusFlags.Revoked, out messages))
                    {
                        issues.Add(SignatureLog.Error(NuGetLogCode.NU3028, string.Format(CultureInfo.CurrentCulture, Strings.VerifyError_TimestampVerifyChainBuildingIssue, signature.FriendlyName, messages.First())));
                        flags |= SignatureVerificationStatusFlags.CertificateRevoked;

                        return flags;
                    }

                    const X509ChainStatusFlags RevocationStatusFlags = X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation;
                    if (CertificateChainUtility.TryGetStatusMessage(chainStatusList, RevocationStatusFlags, out messages))
                    {
                        if (treatIssueAsError)
                        {
                            foreach (var message in messages)
                            {
                                issues.Add(SignatureLog.Issue(!settings.AllowUnknownRevocation, NuGetLogCode.NU3028, string.Format(CultureInfo.CurrentCulture, Strings.VerifyError_TimestampVerifyChainBuildingIssue, signature.FriendlyName, message)));
                            }
                        }

                        if (!chainBuildingHasIssues && (settings.AllowIgnoreTimestamp || settings.AllowUnknownRevocation))
                        {
                            return flags;
                        }

                        flags |= SignatureVerificationStatusFlags.UnknownRevocation;
                        chainBuildingHasIssues = true;
                    }

                    // Debug log any errors
                    issues.Add(
                        SignatureLog.DebugLog(
                            string.Format(
                                CultureInfo.CurrentCulture,
                                $"{signature.FriendlyName}'s timestamp",
                                Strings.VerifyError_InvalidCertificateChain,
                                string.Join(", ", chainStatusList.Select(x => x.Status.ToString())))));
                }
            }

            return flags;
        }
#endif
    }
}