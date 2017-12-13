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

            var timestampLimits = GetTimestampLimits(signature, !settings.FailWithMultipleTimestamps, !settings.AllowIgnoreTimestamp, !settings.AllowNoTimestamp, issues);
            if (timestampLimits == null && (!settings.AllowIgnoreTimestamp || !settings.AllowNoTimestamp))
            {
                return new SignedPackageVerificationResult(SignatureVerificationStatus.Invalid, signature, issues);
            }

            var status = VerifySignature(signature, timestampLimits, !settings.AllowUntrusted, issues);
            return new SignedPackageVerificationResult(status, signature, issues);
        }

        private Tuple<DateTimeOffset, DateTimeOffset> GetTimestampLimits(Signature signature, bool ignoreMultipleTimestamps, bool failIfInvalid, bool failIfNoTimestamp, List<SignatureLog> issues)
        {
            Tuple<DateTimeOffset, DateTimeOffset> timestampLimits = null;
            var timestampCms = new SignedCms();
            var hasFoundOneTimestamp = false;

            var authorUnsignedAttributes = signature.SignerInfo.UnsignedAttributes;
            foreach (var attribute in authorUnsignedAttributes)
            {
                if (StringComparer.Ordinal.Equals(attribute.Oid.Value, Oids.SignatureTimeStampTokenAttributeOid))
                {
                    if (hasFoundOneTimestamp && !ignoreMultipleTimestamps)
                    {
                        issues.Add(SignatureLog.Issue(true, NuGetLogCode.NU3000, Strings.ErrorMultipleTimestamps));
                        return null;
                    }
                    hasFoundOneTimestamp = true;
                    timestampCms.Decode(attribute.Values[0].RawData);
                    using (var authorSignatureNativeCms = NativeCms.Decode(signature.SignedCms.Encode(), detached: false))
                    {
                        var signatureHash = NativeCms.GetSignatureValueHash(signature.SignatureContent.HashAlgorithm, authorSignatureNativeCms);
                        timestampLimits = GetTimestampIfValid(timestampCms, signatureHash,failIfInvalid, issues);
                    }

                    if (ignoreMultipleTimestamps)
                    {
                        break;
                    }
                }
            }

            if (!hasFoundOneTimestamp)
            {
                issues.Add(SignatureLog.Issue(failIfNoTimestamp, NuGetLogCode.NU3550, Strings.ErrorNoTimestamp));
            }

            return timestampLimits;
        }

        private SignatureVerificationStatus VerifySignature(Signature signature, Tuple<DateTimeOffset, DateTimeOffset> timestampLimits, bool failuresAreFatal, List<SignatureLog> issues)
        {
            var certificate = signature.SignerInfo.Certificate;
            if (certificate != null)
            {
                try
                {
                    signature.SignerInfo.CheckSignature(verifySignatureOnly: true);
                }
                catch (Exception e)
                {
                    var code = failuresAreFatal ? NuGetLogCode.NU3030 : NuGetLogCode.NU3530;
                    issues.Add(SignatureLog.Issue(failuresAreFatal, code, Strings.ErrorSignatureVerificationFailed));
                    issues.Add(SignatureLog.DebugLog(e.ToString()));
                    return SignatureVerificationStatus.Invalid;
                }

                timestampLimits = timestampLimits ?? new Tuple<DateTimeOffset, DateTimeOffset>(DateTimeOffset.Now, DateTimeOffset.Now);
                if (Rfc3161TimestampVerificationUtility.ValidateSignerCertificateAgainstTimestamp(certificate, timestampLimits))
                {
                    // Read signed attribute containing the original cert hashes
                    // var signingCertificateAttribute = signature.SignerInfo.SignedAttributes.GetAttributeOrDefault(Oids.SigningCertificateV2);
                    // TODO: how are we going to use the signingCertificateAttribute?

                    var certificateExtraStore = signature.SignedCms.Certificates;

                    X509ChainStatus[] chainStatusList;
                    using (var chain = new X509Chain())
                    {
                        SigningUtility.SetCertBuildChainPolicy(chain.ChainPolicy, certificateExtraStore, timestampLimits.Item2.LocalDateTime, NuGetVerificationCertificateType.Signature);
                        if (SigningUtility.BuildCertificateChain(chain, certificate, out chainStatusList))
                        {
                            return SignatureVerificationStatus.Trusted;
                        }
                    }

                    var chainBuildingHasIssues = false;
                    IReadOnlyList<string> messages;
                    if(SigningUtility.TryGetStatusMessage(chainStatusList, SigningUtility.InvalidCertificateFlags, out messages))
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

                    if (failuresAreFatal || SigningUtility.CertificateValidityPeriodIsInTheFuture(certificate))
                    {
                        const X509ChainStatusFlags NotTimeValidFlags = X509ChainStatusFlags.NotTimeValid | X509ChainStatusFlags.CtlNotTimeValid;

                        if (SigningUtility.TryGetStatusMessage(chainStatusList, NotTimeValidFlags, out messages))
                        {
                            foreach (var message in messages)
                            {
                                issues.Add(SignatureLog.Issue(failuresAreFatal, NuGetLogCode.NU3021, message));
                            }
                            chainBuildingHasIssues = true;
                        }
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
                else
                {
                    // package is expired or validity is in the future, signature is not trusted
                    var code = failuresAreFatal ? NuGetLogCode.NU3030 : NuGetLogCode.NU3530;
                    issues.Add(SignatureLog.Issue(failuresAreFatal, code, Strings.ErrorSignatureVerificationFailed));
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
        private Tuple<DateTimeOffset, DateTimeOffset> GetTimestampIfValid(SignedCms timestampCms, byte[] data, bool failuresAreFatal, List<SignatureLog> issues)
        {
            var timestampSignerInfo = timestampCms.SignerInfos[0];
            var timestamperCertificate = timestampSignerInfo.Certificate;

            if (timestamperCertificate == null)
            {
                issues.Add(SignatureLog.Issue(failuresAreFatal, NuGetLogCode.NU3040, Strings.TimestampNoCertificate));
                return null;
            }

            if (Rfc3161TimestampVerificationUtility.TryReadTSTInfoFromSignedCms(timestampCms, out var tstInfo))
            {
                if (SigningUtility.IsTimestampValid(data, failuresAreFatal, issues, timestampSignerInfo, tstInfo, _specification))
                {
                    issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture, Strings.TimestampValue, tstInfo.Timestamp.LocalDateTime.ToString()) + Environment.NewLine));

                    //var signingCertificateAttribute = timestampSignerInfo.SignedAttributes.GetAttributeOrDefault(Oids.SigningCertificate);
                    //if (signingCertificateAttribute == null)
                    //{
                    //    signingCertificateAttribute = timestampSignerInfo.SignedAttributes.GetAttributeOrDefault(Oids.SigningCertificateV2);
                    //}
                    // TODO: how are we going to use the signingCertificateAttribute?

                    var certificateExtraStore = timestampCms.Certificates;

                    X509ChainStatus[] chainStatusList;
                    using (var chain = new X509Chain())
                    {
                        SigningUtility.SetCertBuildChainPolicy(chain.ChainPolicy, certificateExtraStore, DateTime.Now, NuGetVerificationCertificateType.Timestamp);
                        if (SigningUtility.BuildCertificateChain(chain, timestamperCertificate, out chainStatusList))
                        {
                            return Rfc3161TimestampVerificationUtility.GetTimeStampLimits(tstInfo);
                        }
                    }

                    var chainBuildingHasIssues = false;
                    IReadOnlyList<string> messages;

                    var timestampInvalidCertificateFlags = SigningUtility.InvalidCertificateFlags & (~X509ChainStatusFlags.Revoked);
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

                    if (failuresAreFatal || SigningUtility.CertificateValidityPeriodIsInTheFuture(timestamperCertificate))
                    {
                        const X509ChainStatusFlags NotTimeValidFlags = X509ChainStatusFlags.NotTimeValid | X509ChainStatusFlags.CtlNotTimeValid;

                        if (SigningUtility.TryGetStatusMessage(chainStatusList, NotTimeValidFlags, out messages))
                        {
                            foreach (var message in messages)
                            {
                                issues.Add(SignatureLog.Issue(failuresAreFatal, NuGetLogCode.NU3041, message));
                            }
                            chainBuildingHasIssues = true;
                        }
                    }

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
                            return Rfc3161TimestampVerificationUtility.GetTimeStampLimits(tstInfo);
                        }
                        chainBuildingHasIssues = true;
                    }

                    // Debug log any errors
                    issues.Add(SignatureLog.DebugLog(string.Format(CultureInfo.CurrentCulture, Strings.ErrorInvalidCertificateChain, string.Join(", ", chainStatusList.Select(x => x.ToString())))));
                }
            }
            else
            {
                var code = failuresAreFatal ? NuGetLogCode.NU3050 : NuGetLogCode.NU3550;
                issues.Add(SignatureLog.Issue(failuresAreFatal, code, Strings.TimestampFailureInvalidContentType));
            }

            return null;
        }
#else
        private PackageVerificationResult VerifyValidityAndTrust(ISignedPackageReader package, Signature signature, SignedPackageVerifierSettings settings)
        {
            throw new NotSupportedException();
        }
#endif
    }
}
