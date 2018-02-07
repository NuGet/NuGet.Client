// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class SignatureTrustAndValidityVerificationProvider : ISignatureVerificationProvider
    {
        private HashAlgorithmName _fingerprintAlgorithm;

        private SigningSpecifications _specification => SigningSpecifications.V1;

        public SignatureTrustAndValidityVerificationProvider()
        {
            _fingerprintAlgorithm = HashAlgorithmName.SHA256;
        }

        public Task<PackageVerificationResult> GetTrustResultAsync(ISignedPackageReader package, Signature signature, SignedPackageVerifierSettings settings, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var result = VerifyValidityAndTrust(signature, settings);
            return Task.FromResult(result);
        }

#if IS_DESKTOP
        private PackageVerificationResult VerifyValidityAndTrust(Signature signature, SignedPackageVerifierSettings settings)
        {
            var issues = new List<SignatureLog>
            {
                SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture, Strings.SignatureType, signature.Type.ToString()))
            };

            Timestamp validTimestamp;
            try
            {
                validTimestamp = GetValidTimestamp(
                    signature,
                    settings.AllowMultipleTimestamps,
                    settings.AllowIgnoreTimestamp,
                    settings.AllowNoTimestamp,
                    settings.AllowUnknownRevocation,
                    issues);
            }
            catch (TimestampException)
            {
                return new SignedPackageVerificationResult(SignatureVerificationStatus.Invalid, signature, issues);
            }

            var status = VerifySignature(
                signature,
                validTimestamp,
                settings.AllowUntrusted,
                settings.AllowUntrustedSelfSignedCertificate,
                settings.AllowUnknownRevocation,
                issues);

            return new SignedPackageVerificationResult(status, signature, issues);
        }

        private Timestamp GetValidTimestamp(
            Signature signature,
            bool allowMultipleTimestamps,
            bool allowIgnoreTimestamp,
            bool allowNoTimestamp,
            bool allowUnknownRevocation,
            List<SignatureLog> issues)
        {
            var timestamps = signature.Timestamps;

            if (timestamps.Count == 0)
            {
                issues.Add(SignatureLog.Issue(!allowNoTimestamp, NuGetLogCode.NU3027, Strings.ErrorNoTimestamp));
                if (!allowNoTimestamp)
                {
                    throw new TimestampException();
                }
            }

            if (timestamps.Count > 1 && !allowMultipleTimestamps)
            {
                issues.Add(SignatureLog.Issue(true, NuGetLogCode.NU3000, Strings.ErrorMultipleTimestamps));
                throw new TimestampException();
            }

            var timestamp = timestamps.FirstOrDefault();
            if (timestamp != null)
            {
                using (var primarySignatureNativeCms = NativeCms.Decode(signature.SignedCms.Encode(), detached: false))
                {
                    var timestampHashAlgorithmName = GetTimestampMessageImprintHashAlgorithmName(timestamp);
                    if (timestampHashAlgorithmName == HashAlgorithmName.Unknown)
                    {
                        issues.Add(
                            SignatureLog.Issue(
                                !allowNoTimestamp,
                                NuGetLogCode.NU3030,
                                Strings.TimestampMessageImprintUnsupportedHashAlgorithm));

                        return null;
                    }

                    var signatureHash = NativeCms.GetSignatureValueHash(timestampHashAlgorithmName, primarySignatureNativeCms);

                    if (!IsTimestampValid(timestamp, signatureHash, allowIgnoreTimestamp, allowUnknownRevocation, issues) && !allowIgnoreTimestamp)
                    {
                        throw new TimestampException();
                    }
                }
            }

            return timestamp;
        }

        private static HashAlgorithmName GetTimestampMessageImprintHashAlgorithmName(Timestamp timestamp)
        {
            try
            {
                return CryptoHashUtility.OidToHashAlgorithmName(timestamp.TstInfo.HashAlgorithmId.Value);
            }
            catch (ArgumentException)
            {
            }

            return HashAlgorithmName.Unknown;
        }

        private SignatureVerificationStatus VerifySignature(
            Signature signature,
            Timestamp timestamp,
            bool allowUntrusted,
            bool allowUntrustedSelfSignedCertificate,
            bool allowUnknownRevocation,
            List<SignatureLog> issues)
        {
            var treatIssueAsError = !allowUntrusted;
            var certificate = signature.SignerInfo.Certificate;
            if (certificate == null)
            {
                issues.Add(SignatureLog.Issue(treatIssueAsError, NuGetLogCode.NU3010, Strings.ErrorNoCertificate));

                return SignatureVerificationStatus.Invalid;
            }

            issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture,
                Strings.VerificationAuthorCertDisplay,
                $"{Environment.NewLine}{CertificateUtility.X509Certificate2ToString(certificate, _fingerprintAlgorithm)}")));

            try
            {
                signature.SignerInfo.CheckSignature(verifySignatureOnly: true);
            }
            catch (Exception e)
            {
                issues.Add(SignatureLog.Issue(treatIssueAsError, NuGetLogCode.NU3012, Strings.ErrorSignatureVerificationFailed));
                issues.Add(SignatureLog.DebugLog(e.ToString()));

                return SignatureVerificationStatus.Invalid;
            }

            if (VerificationUtility.IsSigningCertificateValid(certificate, treatIssueAsError, issues))
            {
                timestamp = timestamp ?? new Timestamp();
                if (Rfc3161TimestampVerificationUtility.ValidateSignerCertificateAgainstTimestamp(certificate, timestamp))
                {
                    var certificateExtraStore = signature.SignedCms.Certificates;

                    using (var chainHolder = new X509ChainHolder())
                    {
                        var chain = chainHolder.Chain;

                        // These flags should only be set for verification scenarios not signing
                        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid | X509VerificationFlags.IgnoreCtlNotTimeValid;

                        CertificateChainUtility.SetCertBuildChainPolicy(chain.ChainPolicy, certificateExtraStore, timestamp.UpperLimit.LocalDateTime, CertificateType.Signature);
                        var chainBuildingSucceed = CertificateChainUtility.BuildCertificateChain(chain, certificate, out var chainStatuses);

                        issues.Add(SignatureLog.DetailedLog(CertificateUtility.X509ChainToString(chain, _fingerprintAlgorithm)));

                        if (chainBuildingSucceed)
                        {
                            return SignatureVerificationStatus.Trusted;
                        }

                        var chainBuildingHasIssues = false;
                        var statusFlags = CertificateChainUtility.DefaultObservedStatusFlags;
                        var isSelfSignedCertificate = CertificateUtility.IsSelfIssued(certificate);

                        if (isSelfSignedCertificate)
                        {
                            statusFlags &= ~X509ChainStatusFlags.UntrustedRoot;
                        }

                        IEnumerable<string> messages;
                        if (CertificateChainUtility.TryGetStatusMessage(chainStatuses, statusFlags, out messages))
                        {
                            foreach (var message in messages)
                            {
                                issues.Add(SignatureLog.Issue(treatIssueAsError, NuGetLogCode.NU3012, message));
                            }

                            chainBuildingHasIssues = true;
                        }

                        // For all the special cases, chain status list only has unique elements for each chain status flag present
                        // therefore if we are checking for one specific chain status we can use the first of the returned list
                        // if we are combining checks for more than one, then we have to use the whole list.
                        IEnumerable<X509ChainStatus> chainStatus = null;
                        if (CertificateChainUtility.ChainStatusListIncludesStatus(chainStatuses, X509ChainStatusFlags.Revoked, out chainStatus))
                        {
                            var status = chainStatus.First();

                            issues.Add(SignatureLog.Error(NuGetLogCode.NU3012, status.StatusInformation));

                            return SignatureVerificationStatus.Invalid;
                        }

                        if (isSelfSignedCertificate &&
                            CertificateChainUtility.TryGetStatusMessage(chainStatuses, X509ChainStatusFlags.UntrustedRoot, out messages))
                        {
                            issues.Add(SignatureLog.Issue(!allowUntrustedSelfSignedCertificate, NuGetLogCode.NU3018, messages.First()));

                            if (!chainBuildingHasIssues && allowUntrustedSelfSignedCertificate)
                            {
                                return SignatureVerificationStatus.Trusted;
                            }
                        }

                        const X509ChainStatusFlags RevocationStatusFlags = X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation;
                        if (CertificateChainUtility.TryGetStatusMessage(chainStatuses, RevocationStatusFlags, out messages))
                        {
                            if (treatIssueAsError)
                            {
                                foreach (var message in messages)
                                {
                                    issues.Add(SignatureLog.Issue(!allowUnknownRevocation, NuGetLogCode.NU3018, message));
                                }
                            }

                            if (!chainBuildingHasIssues && allowUnknownRevocation)
                            {
                                return SignatureVerificationStatus.Trusted;
                            }

                            chainBuildingHasIssues = true;
                        }

                        // Debug log any errors
                        issues.Add(SignatureLog.DebugLog(
                            string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.ErrorInvalidCertificateChain,
                                string.Join(", ", chainStatuses.Select(x => x.Status.ToString())))));
                    }
                }
                else
                {
                    issues.Add(SignatureLog.Issue(treatIssueAsError, NuGetLogCode.NU3011, Strings.SignatureNotTimeValid));
                }
            }

            return SignatureVerificationStatus.Untrusted;
        }

        private bool IsTimestampValid(
            Timestamp timestamp,
            byte[] messageHash,
            bool allowIgnoreTimestamp,
            bool allowUnknownRevocation,
            List<SignatureLog> issues)
        {
            var treatIssueAsError = !allowIgnoreTimestamp;
            var timestamperCertificate = timestamp.SignerInfo.Certificate;
            if (timestamperCertificate == null)
            {
                issues.Add(SignatureLog.Issue(treatIssueAsError, NuGetLogCode.NU3020, Strings.TimestampNoCertificate));
                return false;
            }

            if (VerificationUtility.IsTimestampValid(timestamp, messageHash, treatIssueAsError, issues, _specification))
            {
                issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture, Strings.TimestampValue, timestamp.GeneralizedTime.LocalDateTime.ToString()) + Environment.NewLine));

                issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture,
                    Strings.VerificationTimestamperCertDisplay,
                    $"{Environment.NewLine}{CertificateUtility.X509Certificate2ToString(timestamperCertificate, _fingerprintAlgorithm)}")));

                var certificateExtraStore = timestamp.SignedCms.Certificates;

                using (var chainHolder = new X509ChainHolder())
                {
                    var chain = chainHolder.Chain;

                    // This flags should only be set for verification scenarios, not signing
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid | X509VerificationFlags.IgnoreCtlNotTimeValid;

                    CertificateChainUtility.SetCertBuildChainPolicy(chain.ChainPolicy, certificateExtraStore, DateTime.Now, CertificateType.Timestamp);

                    var chainBuildSucceed = CertificateChainUtility.BuildCertificateChain(chain, timestamperCertificate, out var chainStatusList);

                    issues.Add(SignatureLog.DetailedLog(CertificateUtility.X509ChainToString(chain, _fingerprintAlgorithm)));

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
                            issues.Add(SignatureLog.Issue(treatIssueAsError, NuGetLogCode.NU3028, message));
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
                                issues.Add(SignatureLog.Issue(!allowUnknownRevocation, NuGetLogCode.NU3028, message));
                            }
                        }

                        if (!chainBuildingHasIssues && (allowIgnoreTimestamp || allowUnknownRevocation))
                        {
                            return true;
                        }

                        chainBuildingHasIssues = true;
                    }

                    // Debug log any errors
                    issues.Add(
                        SignatureLog.DebugLog(
                            string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.ErrorInvalidCertificateChain,
                                string.Join(", ", chainStatusList.Select(x => x.Status.ToString())))));
                }
            }

            return false;
        }
#else
        private PackageVerificationResult VerifyValidityAndTrust(Signature signature, SignedPackageVerifierSettings settings)
        {
            throw new NotSupportedException();
        }
#endif
    }
}