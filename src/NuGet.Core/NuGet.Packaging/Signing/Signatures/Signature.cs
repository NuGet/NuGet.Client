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
    /// <summary>
    /// Package signature information.
    /// </summary>
    public abstract class Signature
    {
#if IS_DESKTOP
        private readonly Lazy<IReadOnlyList<Timestamp>> _timestamps;

        /// <summary>
        /// Indicates if this is an author or repository signature.
        /// </summary>
        public SignatureType Type { get; }

        /// <summary>
        /// Signature timestamps.
        /// </summary>
        public IReadOnlyList<Timestamp> Timestamps => _timestamps.Value;

        /// <summary>
        /// SignerInfo for this signature.
        /// </summary>
        public SignerInfo SignerInfo { get; }

        public abstract byte[] GetSignatureValue();

        protected Signature(SignerInfo signerInfo, SignatureType type)
        {
            SignerInfo = signerInfo;
            Type = type;

            _timestamps = new Lazy<IReadOnlyList<Timestamp>>(() => GetTimestamps(SignerInfo));

            if (Type != SignatureType.Unknown)
            {
                VerifySigningTimeAttribute(SignerInfo);
            }
        }

        protected abstract void ThrowForInvalidSignature();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="allowMultipleTimestamps"></param>
        /// <param name="allowIgnoreTimestamp"></param>
        /// <param name="allowNoTimestamp"></param>
        /// <param name="allowUnknownRevocation"></param>
        /// <param name="issues"></param>
        /// <returns></returns>
        internal Timestamp GetValidTimestamp(
            SignedPackageVerifierSettings settings,
            HashAlgorithmName fingerprintAlgorithm,
            List<SignatureLog> issues)
        {
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }
            var timestamps = Timestamps;
            settings = settings ?? SignedPackageVerifierSettings.Default;

            if (timestamps.Count == 0)
            {
                issues.Add(SignatureLog.Issue(!settings.AllowNoTimestamp, NuGetLogCode.NU3027, Strings.ErrorNoTimestamp));
                if (!settings.AllowNoTimestamp)
                {
                    throw new TimestampException(Strings.TimestampInvalid);
                }
            }

            if (timestamps.Count > 1 && !settings.AllowMultipleTimestamps)
            {
                issues.Add(SignatureLog.Issue(true, NuGetLogCode.NU3000, Strings.ErrorMultipleTimestamps));
                throw new TimestampException(Strings.TimestampInvalid);
            }

            var timestamp = timestamps.FirstOrDefault();
            if (timestamp != null && !timestamp.Verify(this, settings, fingerprintAlgorithm, issues) && !settings.AllowIgnoreTimestamp)
            {
                throw new TimestampException(Strings.TimestampInvalid);
            }

            return timestamp;
        }

        /// <summary>
        /// Verify if the signature object meets the specification trust and validity requirements.
        /// </summary>
        /// <param name="timestamp">Timestamp for this signature, if signature is not timestamped it can be null.</param>
        /// <param name="allowUntrusted">Setting that tells if a signature that does not meet any soft failure requirements can still be allowed. Used to know if warnings or errors should be logged for an issue.</param>
        /// <param name="allowUnknownRevocation">Setting that tells if unkown revocation is valid when building the chain.</param>
        /// <param name="allowUntrustedSelfSignedCertificate">Setting that tells if an untrusted self-signed certificate should be allowed as the signing certificate.</param>
        /// <param name="fingerprintAlgorithm">Algorithm used to calculate and display the certificate's fingerprint.</param>
        /// <param name="certificateExtraStore">Collection of certificates to help the chain building engine as an extra store.</param>
        /// <param name="issues">List of log messages.</param>
        /// <returns>Status of trust for signature.</returns>
        internal virtual SignatureVerificationStatus Verify(
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

            var treatIssueAsError = !settings.AllowIllegal;
            var certificate = SignerInfo.Certificate;
            if (certificate == null)
            {
                issues.Add(SignatureLog.Issue(treatIssueAsError, NuGetLogCode.NU3010, Strings.ErrorNoCertificate));

                return SignatureVerificationStatus.Illegal;
            }

            issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture,
                Strings.VerificationAuthorCertDisplay,
                $"{Environment.NewLine}{CertificateUtility.X509Certificate2ToString(certificate, fingerprintAlgorithm)}")));

            try
            {
                SignerInfo.CheckSignature(verifySignatureOnly: true);
            }
            catch (Exception e)
            {
                issues.Add(SignatureLog.Issue(treatIssueAsError, NuGetLogCode.NU3012, Strings.ErrorSignatureVerificationFailed));
                issues.Add(SignatureLog.DebugLog(e.ToString()));

                return SignatureVerificationStatus.Illegal;
            }

            if (VerificationUtility.IsSigningCertificateValid(certificate, treatIssueAsError, issues))
            {
                timestamp = timestamp ?? new Timestamp();
                if (Rfc3161TimestampVerificationUtility.ValidateSignerCertificateAgainstTimestamp(certificate, timestamp))
                {
                    using (var chainHolder = new X509ChainHolder())
                    {
                        var chain = chainHolder.Chain;

                        // These flags should only be set for verification scenarios not signing
                        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid | X509VerificationFlags.IgnoreCtlNotTimeValid;

                        CertificateChainUtility.SetCertBuildChainPolicy(chain.ChainPolicy, certificateExtraStore, timestamp.UpperLimit.LocalDateTime, CertificateType.Signature);
                        var chainBuildingSucceed = CertificateChainUtility.BuildCertificateChain(chain, certificate, out var chainStatuses);

                        issues.Add(SignatureLog.DetailedLog(CertificateUtility.X509ChainToString(chain, fingerprintAlgorithm)));

                        if (chainBuildingSucceed)
                        {
                            return SignatureVerificationStatus.Valid;
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

                            return SignatureVerificationStatus.Suspect;
                        }

                        if (isSelfSignedCertificate &&
                            CertificateChainUtility.TryGetStatusMessage(chainStatuses, X509ChainStatusFlags.UntrustedRoot, out messages))
                        {
                            issues.Add(SignatureLog.Issue(!settings.AllowUntrustedSelfIssuedCertificate, NuGetLogCode.NU3018, messages.First()));

                            if (!chainBuildingHasIssues && settings.AllowUntrustedSelfIssuedCertificate)
                            {
                                return SignatureVerificationStatus.Valid;
                            }
                        }

                        const X509ChainStatusFlags RevocationStatusFlags = X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation;
                        if (CertificateChainUtility.TryGetStatusMessage(chainStatuses, RevocationStatusFlags, out messages))
                        {
                            if (treatIssueAsError)
                            {
                                foreach (var message in messages)
                                {
                                    issues.Add(SignatureLog.Issue(!settings.AllowUnknownRevocation, NuGetLogCode.NU3018, message));
                                }
                            }

                            if (!chainBuildingHasIssues && settings.AllowUnknownRevocation)
                            {
                                return SignatureVerificationStatus.Valid;
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

            return SignatureVerificationStatus.Illegal;
        }

        private void VerifySigningTimeAttribute(SignerInfo signerInfo)
        {
            var attribute = signerInfo.SignedAttributes.GetAttributeOrDefault(Oids.SigningTime);

            if (attribute == null)
            {
                ThrowForInvalidSignature();
            }
        }

        /// <summary>
        /// Get timestamps from the signer info
        /// </summary>
        /// <param name="signer"></param>
        /// <returns></returns>
        private static IReadOnlyList<Timestamp> GetTimestamps(SignerInfo signer)
        {
            var unsignedAttributes = signer.UnsignedAttributes;

            var timestampList = new List<Timestamp>();

            foreach (var attribute in unsignedAttributes)
            {
                if (string.Equals(attribute.Oid.Value, Oids.SignatureTimeStampTokenAttribute, StringComparison.Ordinal))
                {
                    var timestampCms = new SignedCms();
                    timestampCms.Decode(attribute.Values[0].RawData);

                    var certificates = SignatureUtility.GetTimestampCertificates(
                        timestampCms,
                        SigningSpecifications.V1);

                    if (certificates == null || certificates.Count == 0)
                    {
                        throw new SignatureException(NuGetLogCode.NU3029, Strings.InvalidTimestampSignature);
                    }

                    timestampList.Add(new Timestamp(timestampCms));
                }
            }

            return timestampList;
        }
#endif
    }
}