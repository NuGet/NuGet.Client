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
            settings = settings ?? SignedPackageVerifierSettings.GetDefault();

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
        /// <remarks>This is only public for ease of testing</remarks>
        /// <returns>Status of trust for signature.</returns>
        public virtual SignatureVerificationSummary Verify(
            Timestamp timestamp,
            SignatureVerifySettings settings,
            HashAlgorithmName fingerprintAlgorithm,
            X509Certificate2Collection certificateExtraStore,
            List<SignatureLog> issues)
        {
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }
            settings = settings ?? SignatureVerifySettings.GetDefault();
            var flags = SignatureVerificationStatusFlags.NoErrors;
            var signatureType = GetType().Name;

            var certificate = SignerInfo.Certificate;
            if (certificate == null)
            {
                issues.Add(SignatureLog.Issue(settings.TreatIssuesAsErrors, NuGetLogCode.NU3010, string.Format(CultureInfo.CurrentCulture, Strings.Verify_ErrorNoCertificate, signatureType)));

                flags |= SignatureVerificationStatusFlags.NoCertificate;
                return new SignatureVerificationSummary(Type, SignatureVerificationStatus.Illegal, flags);
            }

            issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture,
                Strings.VerificationCertDisplay,
                signatureType,
                $"{Environment.NewLine}{CertificateUtility.X509Certificate2ToString(certificate, fingerprintAlgorithm)}")));

            try
            {
                SignerInfo.CheckSignature(verifySignatureOnly: true);
            }
            catch (Exception e)
            {
                issues.Add(SignatureLog.Issue(settings.TreatIssuesAsErrors, NuGetLogCode.NU3012, string.Format(CultureInfo.CurrentCulture, Strings.VerifyErrorSignatureVerificationFailed, signatureType)));
                issues.Add(SignatureLog.DebugLog(e.ToString()));
                flags |= SignatureVerificationStatusFlags.SignatureCheckFailed;

                return new SignatureVerificationSummary(Type, SignatureVerificationStatus.Illegal, flags);
            }

            DateTime? expirationTime = null;
            var certificateFlags = VerificationUtility.ValidateSigningCertificate(certificate, settings.TreatIssuesAsErrors, signatureType, issues);
            if (certificateFlags != SignatureVerificationStatusFlags.NoErrors)
            {
                flags |= certificateFlags;
            }
            else
            {
                timestamp = timestamp ?? new Timestamp();
                using (var chainHolder = new X509ChainHolder())
                {
                    var chain = chainHolder.Chain;

                    // This flag should only be set for verification scenarios, not signing.
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;

                    CertificateChainUtility.SetCertBuildChainPolicy(chain.ChainPolicy, certificateExtraStore, timestamp.UpperLimit.LocalDateTime, CertificateType.Signature);
                    var chainBuildingSucceed = CertificateChainUtility.BuildCertificateChain(chain, certificate, out var chainStatuses);

                    issues.Add(SignatureLog.DetailedLog(CertificateUtility.X509ChainToString(chain, fingerprintAlgorithm)));
                    var chainBuildingHasIssues = false;

                    if (!chainBuildingSucceed)
                    {
                        var statusFlags = CertificateChainUtility.DefaultObservedStatusFlags;

                        IEnumerable<string> messages;
                        if (CertificateChainUtility.TryGetStatusMessage(chainStatuses, statusFlags, out messages))
                        {
                            foreach (var message in messages)
                            {
                                issues.Add(SignatureLog.Issue(settings.TreatIssuesAsErrors, NuGetLogCode.NU3012, string.Format(CultureInfo.CurrentCulture, Strings.VerifyChainBuildingIssue, signatureType, message)));
                            }

                            chainBuildingHasIssues = true;
                            flags |= SignatureVerificationStatusFlags.ChainBuildingNotConformantWithSpec;
                        }

                        // For all the special cases, chain status list only has unique elements for each chain status flag present
                        // therefore if we are checking for one specific chain status we can use the first of the returned list
                        // if we are combining checks for more than one, then we have to use the whole list.
                        if (CertificateChainUtility.TryGetStatusMessage(chainStatuses, X509ChainStatusFlags.Revoked, out messages))
                        {
                            issues.Add(SignatureLog.Error(NuGetLogCode.NU3012, string.Format(CultureInfo.CurrentCulture, Strings.VerifyChainBuildingIssue, signatureType, messages.First())));
                            flags |= SignatureVerificationStatusFlags.CertificateRevoked;

                            return new SignatureVerificationSummary(Type, SignatureVerificationStatus.Suspect, flags, timestamp);
                        }

                        if (CertificateChainUtility.TryGetStatusMessage(chainStatuses, X509ChainStatusFlags.UntrustedRoot, out messages))
                        {
                            if (!settings.AllowUntrustedRoot)
                            {
                                issues.Add(SignatureLog.Issue(settings.TreatIssuesAsErrors, NuGetLogCode.NU3018, string.Format(CultureInfo.CurrentCulture, Strings.VerifyChainBuildingIssue, signatureType, messages.First())));

                                chainBuildingHasIssues = true;
                            }
                            flags |= SignatureVerificationStatusFlags.UntrustedRoot;
                        }

                        const X509ChainStatusFlags RevocationStatusFlags = X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation;
                        if (CertificateChainUtility.TryGetStatusMessage(chainStatuses, RevocationStatusFlags, out messages))
                        {
                            if (settings.TreatIssuesAsErrors)
                            {
                                foreach (var message in messages)
                                {
                                    issues.Add(SignatureLog.Issue(!settings.AllowUnknownRevocation, NuGetLogCode.NU3018, string.Format(CultureInfo.CurrentCulture, Strings.VerifyChainBuildingIssue, signatureType, message)));
                                }
                            }

                            if (!settings.AllowUnknownRevocation)
                            {
                                chainBuildingHasIssues = true;
                                flags |= SignatureVerificationStatusFlags.UnknownRevocation;
                            }
                        }

                        // Debug log any errors
                        issues.Add(SignatureLog.DebugLog(
                            string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.VerifyErrorInvalidCertificateChain,
                                signatureType,
                                string.Join(", ", chainStatuses.Select(x => x.Status.ToString())))));
                    }

                    var signatureTimeValid = Rfc3161TimestampVerificationUtility.ValidateSignerCertificateAgainstTimestamp(certificate, timestamp);
                    if (signatureTimeValid && !chainBuildingHasIssues)
                    {
                        return new SignatureVerificationSummary(Type, SignatureVerificationStatus.Valid, flags, timestamp);
                    }
                    else if (!signatureTimeValid)
                    {
                        if (settings.LogOnSignatureExpired)
                        {
                            issues.Add(SignatureLog.Issue(settings.TreatIssuesAsErrors, NuGetLogCode.NU3011, string.Format(CultureInfo.CurrentCulture, Strings.SignatureNotTimeValid, signatureType)));
                        }
                        flags |= SignatureVerificationStatusFlags.CertificateExpired;
                        expirationTime = DateTime.SpecifyKind(certificate.NotAfter, DateTimeKind.Local);
                    }
                }
            }

            return new SignatureVerificationSummary(Type, SignatureVerificationStatus.Illegal, flags, timestamp, expirationTime);
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

                    using (var certificates = SignatureUtility.GetTimestampCertificates(
                        timestampCms,
                        SigningSpecifications.V1))
                    {
                        if (certificates == null || certificates.Count == 0)
                        {
                            throw new SignatureException(NuGetLogCode.NU3029, Strings.InvalidTimestampSignature);
                        }
                    }

                    timestampList.Add(new Timestamp(timestampCms));
                }
            }

            return timestampList;
        }
#endif
    }
}