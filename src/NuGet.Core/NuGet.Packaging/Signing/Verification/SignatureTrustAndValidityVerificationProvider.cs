// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Security.Cryptography;
using System.Linq;
using NuGet.Common;

#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif

namespace NuGet.Packaging.Signing
{
    enum NuGetVerificationCertificateType
    {
        Signature,
        Timestamp
    }

    public class SignatureTrustAndValidityVerificationProvider : ISignatureVerificationProvider
    {
        private SigningSpecifications _specification => SigningSpecifications.V1;

        public Task<PackageVerificationResult> GetTrustResultAsync(ISignedPackageReader package, Signature signature, SignedPackageVerifierSettings settings, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var result = VerifyValidityAndTrust(package, signature, settings);
            return Task.FromResult(result);
        }

#if IS_DESKTOP
        private PackageVerificationResult VerifyValidityAndTrust(ISignedPackageReader package, Signature signature, SignedPackageVerifierSettings settings)
        {
            var issues = new List<SignatureLog>
            {
                SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture, Strings.SignatureType, signature.Type.ToString()))
            };

            Timestamp validTimestamp;
            try
            {
                validTimestamp = GetValidTimestamp(signature, !settings.FailWithMultipleTimestamps, !settings.AllowIgnoreTimestamp, !settings.AllowNoTimestamp, issues);
            }
            catch (TimestampException)
            {
                return new SignedPackageVerificationResult(SignatureVerificationStatus.Invalid, signature, issues);
            }

            var status = VerifySignature(signature, validTimestamp, !settings.AllowUntrusted, issues);
            return new SignedPackageVerificationResult(status, signature, issues);
        }

        private Timestamp GetValidTimestamp(Signature signature, bool ignoreMultipleTimestamps, bool failIfInvalid, bool failIfNoTimestamp, List<SignatureLog> issues)
        {
            var timestamps = signature.Timestamps;

            if (timestamps.Count == 0)
            {
                issues.Add(SignatureLog.Issue(failIfNoTimestamp, NuGetLogCode.NU3050, Strings.ErrorNoTimestamp));
                if (failIfNoTimestamp)
                {
                    throw new TimestampException();
                }
            }

            if (timestamps.Count > 1 && !ignoreMultipleTimestamps)
            {
                issues.Add(SignatureLog.Issue(true, NuGetLogCode.NU3000, Strings.ErrorMultipleTimestamps));
                throw new TimestampException();
            }

            var timestamp = timestamps.FirstOrDefault();
            if (timestamp != null)
            {
                using (var authorSignatureNativeCms = NativeCms.Decode(signature.SignedCms.Encode(), detached: false))
                {
                    var signatureHash = NativeCms.GetSignatureValueHash(signature.SignatureContent.HashAlgorithm, authorSignatureNativeCms);

                    if (!IsTimestampValid(timestamp, signatureHash, failIfInvalid, issues) && failIfInvalid)
                    {
                        throw new TimestampException();
                    }
                }
            }

            return timestamp;
        }

        private SignatureVerificationStatus VerifySignature(Signature signature, Timestamp timestamp, bool failuresAreFatal, List<SignatureLog> issues)
        {
            var certificate = signature.SignerInfo.Certificate;
            if (certificate != null)
            {
                issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture,
                    Strings.VerificationAuthorCertDisplay,
                    $"{Environment.NewLine}{CertificateUtility.X509Certificate2ToString(certificate)}")));

                try
                {
                    signature.SignerInfo.CheckSignature(verifySignatureOnly: true);
                }
                catch (Exception e)
                {
                    issues.Add(SignatureLog.Issue(failuresAreFatal, NuGetLogCode.NU3030, Strings.ErrorSignatureVerificationFailed));
                    issues.Add(SignatureLog.DebugLog(e.ToString()));
                    return SignatureVerificationStatus.Invalid;
                }

                if (!SigningUtility.IsCertificateValidityPeriodInTheFuture(certificate))
                {
                    timestamp = timestamp ?? new Timestamp();
                    if (Rfc3161TimestampVerificationUtility.ValidateSignerCertificateAgainstTimestamp(certificate, timestamp))
                    {
                        // Read signed attribute containing the original cert hashes
                        // var signingCertificateAttribute = signature.SignerInfo.SignedAttributes.GetAttributeOrDefault(Oids.SigningCertificateV2);
                        // TODO: how are we going to use the signingCertificateAttribute?

                        var certificateExtraStore = signature.SignedCms.Certificates;

                        using (var chain = new X509Chain())
                        {
                            SigningUtility.SetCertBuildChainPolicy(chain.ChainPolicy, certificateExtraStore, timestamp.UpperLimit.LocalDateTime, NuGetVerificationCertificateType.Signature);
                            var chainBuildingSucceed = SigningUtility.BuildCertificateChain(chain, certificate, out var chainStatusList);

                            issues.Add(SignatureLog.DetailedLog(CertificateUtility.X509ChainToString(chain)));

                            if (chainBuildingSucceed)
                            {
                                return SignatureVerificationStatus.Trusted;
                            }

                            var chainBuildingHasIssues = false;
                            IReadOnlyList<string> messages;
                            if (SigningUtility.TryGetStatusMessage(chainStatusList, SigningUtility.NotIgnoredCertificateFlags, out messages))
                            {
                                foreach (var message in messages)
                                {
                                    issues.Add(SignatureLog.Issue(failuresAreFatal, NuGetLogCode.NU3021, message));
                                }
                                chainBuildingHasIssues = true;
                            }

                            // For all the special cases, chain status list only has unique elements for each chain status flag present
                            // therefore if we are checking for one specific chain status we can use the first of the returned list
                            // if we are combining checks for more than one, then we have to use the whole list.
                            IReadOnlyList<X509ChainStatus> chainStatus = null;
                            if (SigningUtility.ChainStatusListIncludesStatus(chainStatusList, X509ChainStatusFlags.Revoked, out chainStatus))
                            {
                                var status = chainStatus.First();
                                issues.Add(SignatureLog.Issue(true, NuGetLogCode.NU3021, status.StatusInformation));
                                return SignatureVerificationStatus.Invalid;
                            }

                            const X509ChainStatusFlags RevocationStatusFlags = X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation;
                            if (SigningUtility.TryGetStatusMessage(chainStatusList, RevocationStatusFlags, out messages))
                            {
                                if (failuresAreFatal)
                                {
                                    foreach (var message in messages)
                                    {
                                        issues.Add(SignatureLog.Issue(failuresAreFatal, NuGetLogCode.NU3021, message));
                                    }
                                }
                                else if (!chainBuildingHasIssues)
                                {
                                    return SignatureVerificationStatus.Trusted;
                                }
                                chainBuildingHasIssues = true;
                            }

                            // Debug log any errors
                            issues.Add(SignatureLog.DebugLog(string.Format(CultureInfo.CurrentCulture, Strings.ErrorInvalidCertificateChain, string.Join(", ", chainStatusList.Select(x => x.ToString())))));
                        }
                    }
                    else
                    {
                        issues.Add(SignatureLog.Issue(failuresAreFatal, NuGetLogCode.NU3030, Strings.ErrorSignatureVerificationFailed));
                    }
                }
                else
                {
                    issues.Add(SignatureLog.Issue(failuresAreFatal, NuGetLogCode.NU3024, Strings.SignatureNotYetValid));
                }
            }
            else
            {
                issues.Add(SignatureLog.Issue(failuresAreFatal, NuGetLogCode.NU3020, Strings.ErrorNoCertificate));
            }

            return SignatureVerificationStatus.Untrusted;
        }

        /// <summary>
        /// Validates a SignedCms object containing a timestamp response.
        /// </summary>
        /// <param name="timestampCms">SignedCms response from the timestamp authority.</param>
        /// <param name="data">byte[] data that was signed and timestamped.</param>
        private bool IsTimestampValid(Timestamp timestamp, byte[] data, bool failuresAreFatal, List<SignatureLog> issues)
        {
            var timestamperCertificate = timestamp.SignerInfo.Certificate;

            if (timestamperCertificate == null)
            {
                issues.Add(SignatureLog.Issue(failuresAreFatal, NuGetLogCode.NU3040, Strings.TimestampNoCertificate));
                return false;
            }

            if (SigningUtility.IsTimestampValid(timestamp, data, failuresAreFatal, issues, _specification))
            {
                issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture, Strings.TimestampValue, timestamp.GeneralizedTime.LocalDateTime.ToString()) + Environment.NewLine));

                issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture,
                    Strings.VerificationTimestamperCertDisplay,
                    $"{Environment.NewLine}{CertificateUtility.X509Certificate2ToString(timestamperCertificate)}")));

                //var signingCertificateAttribute = timestampSignerInfo.SignedAttributes.GetAttributeOrDefault(Oids.SigningCertificate);
                //if (signingCertificateAttribute == null)
                //{
                //    signingCertificateAttribute = timestampSignerInfo.SignedAttributes.GetAttributeOrDefault(Oids.SigningCertificateV2);
                //}
                // TODO: how are we going to use the signingCertificateAttribute?

                var certificateExtraStore = timestamp.SignedCms.Certificates;

                using (var chain = new X509Chain())
                {
                    SigningUtility.SetCertBuildChainPolicy(chain.ChainPolicy, certificateExtraStore, DateTime.Now, NuGetVerificationCertificateType.Timestamp);

                    var chainBuildSucceed = SigningUtility.BuildCertificateChain(chain, timestamperCertificate, out var chainStatusList);

                    issues.Add(SignatureLog.DetailedLog(CertificateUtility.X509ChainToString(chain)));

                    if (chainBuildSucceed)
                    {
                        return true;
                    }

                    var chainBuildingHasIssues = false;
                    IReadOnlyList<string> messages;

                    var timestampInvalidCertificateFlags = SigningUtility.NotIgnoredCertificateFlags |
                        (X509ChainStatusFlags.Revoked) |
                        (X509ChainStatusFlags.NotTimeValid) |
                        (X509ChainStatusFlags.CtlNotTimeValid);

                    if (SigningUtility.TryGetStatusMessage(chainStatusList, timestampInvalidCertificateFlags, out messages))
                    {
                        foreach (var message in messages)
                        {
                            issues.Add(SignatureLog.Issue(failuresAreFatal, NuGetLogCode.NU3041, message));
                        }
                        chainBuildingHasIssues = true;
                    }

                    // For all the special cases, chain status list only has unique elements for each chain status flag present
                    // therefore if we are checking for one specific chain status we can use the first of the returned list
                    // if we are combining checks for more than one, then we have to use the whole list.

                    const X509ChainStatusFlags RevocationStatusFlags = X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation;
                    if (SigningUtility.TryGetStatusMessage(chainStatusList, RevocationStatusFlags, out messages))
                    {
                        if (failuresAreFatal)
                        {
                            foreach (var message in messages)
                            {
                                issues.Add(SignatureLog.Issue(failuresAreFatal, NuGetLogCode.NU3041, message));
                            }
                        }
                        else if (!chainBuildingHasIssues)
                        {
                            return true;
                        }
                        chainBuildingHasIssues = true;
                    }

                    // Debug log any errors
                    issues.Add(SignatureLog.DebugLog(string.Format(CultureInfo.CurrentCulture, Strings.ErrorInvalidCertificateChain, string.Join(", ", chainStatusList.Select(x => x.ToString())))));
                }
            }
            return false;
        }
#else
        private PackageVerificationResult VerifyValidityAndTrust(ISignedPackageReader package, Signature signature, SignedPackageVerifierSettings settings)
        {
            throw new NotSupportedException();
        }
#endif
    }
}
