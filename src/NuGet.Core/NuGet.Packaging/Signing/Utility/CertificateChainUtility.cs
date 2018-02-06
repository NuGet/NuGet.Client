// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public static class CertificateChainUtility
    {
        /// <summary>
        /// Create a list of certificates in chain order with the leaf first and root last.
        /// </summary>
        /// <remarks>This must not be used on timestamp certificates as this method does not enforce the requirement
        /// that timestamps be trusted.</remarks>
        /// <param name="certificate">The certificate for which a chain should be built.</param>
        /// <param name="extraStore">A certificate store containing additional certificates necessary
        /// for chain building.</param>
        /// <param name="logger">A logger.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="certificate" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="extraStore" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="logger" /> is <c>null</c>.</exception>
        public static IReadOnlyList<X509Certificate2> GetCertificateChainForSigning(
            X509Certificate2 certificate,
            X509Certificate2Collection extraStore,
            ILogger logger)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            if (extraStore == null)
            {
                throw new ArgumentNullException(nameof(extraStore));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            using (var chainHolder = new X509ChainHolder())
            {
                var chain = chainHolder.Chain;

                SetCertBuildChainPolicy(
                    chain.ChainPolicy,
                    extraStore,
                    DateTime.Now,
                    NuGetVerificationCertificateType.Signature);

                if (chain.Build(certificate))
                {
                    return GetCertificateListFromChain(chain);
                }

                X509ChainStatusFlags errorStatusFlags;
                X509ChainStatusFlags warningStatusFlags;

                GetChainStatusFlagsForSigning(certificate, out errorStatusFlags, out warningStatusFlags);

                var fatalStatuses = new List<X509ChainStatus>();

                foreach (var chainStatus in chain.ChainStatus)
                {
                    if ((chainStatus.Status & errorStatusFlags) != 0)
                    {
                        fatalStatuses.Add(chainStatus);
                        logger.Log(LogMessage.CreateError(NuGetLogCode.NU3018, chainStatus.StatusInformation?.Trim()));
                    }
                    else if ((chainStatus.Status & warningStatusFlags) != 0)
                    {
                        logger.Log(LogMessage.CreateWarning(NuGetLogCode.NU3018, chainStatus.StatusInformation?.Trim()));
                    }
                }

                if (fatalStatuses.Any())
                {
                    throw new SignatureException(NuGetLogCode.NU3018, Strings.CertificateChainValidationFailed);
                }

                return GetCertificateListFromChain(chain);
            }
        }

        /// <summary>
        /// Create an ordered list of certificates. The leaf node is returned first.
        /// </summary>
        /// <param name="certChain">Certificate chain to be converted to list.</param>
        /// <remarks>This does not check validity or trust. It returns the chain as-is.</remarks>
        public static IReadOnlyList<X509Certificate2> GetCertificateListFromChain(X509Chain certChain)
        {
            if (certChain == null)
            {
                throw new ArgumentNullException(nameof(certChain));
            }

            var certs = new List<X509Certificate2>();

            foreach (var item in certChain.ChainElements)
            {
                // Return a new certificate object.
                // This allows the chain and its chain element certificates to be disposed
                // in both success and error cases.
                certs.Add(new X509Certificate2(item.Certificate.RawData));
            }

            return certs;
        }

        /// <summary>
        /// Get error/warning chain status flags for certificate chain validation during signing.
        /// </summary>
        /// <param name="certificate">The certificate to verify.</param>
        /// <param name="errorStatusFlags">Error chain status flags.</param>
        /// <param name="warningStatusFlags">Warning chain status flags.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="certificate" /> is <c>null</c>.</exception>
        public static void GetChainStatusFlagsForSigning(
            X509Certificate2 certificate,
            out X509ChainStatusFlags errorStatusFlags,
            out X509ChainStatusFlags warningStatusFlags)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            warningStatusFlags = X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation;

            if (CertificateUtility.IsSelfIssued(certificate))
            {
                warningStatusFlags |= X509ChainStatusFlags.UntrustedRoot;
            }

            // Every status flag that isn't a warning is an error.
            errorStatusFlags = (~(X509ChainStatusFlags)0) & ~warningStatusFlags;
        }

        internal static void SetCertBuildChainPolicy(
            X509ChainPolicy policy,
            X509Certificate2Collection additionalCertificates,
            DateTime verificationTime,
            NuGetVerificationCertificateType certificateType)
        {
            if (certificateType == NuGetVerificationCertificateType.Timestamp)
            {
                policy.ApplicationPolicy.Add(new Oid(Oids.TimeStampingEku));
            }
            else
            {
                policy.ApplicationPolicy.Add(new Oid(Oids.CodeSigningEku));
            }

            policy.ExtraStore.AddRange(additionalCertificates);

            policy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            policy.RevocationMode = X509RevocationMode.Online;

            if (certificateType != NuGetVerificationCertificateType.Timestamp)
            {
                policy.VerificationTime = verificationTime;
            }
        }

        internal static bool BuildCertificateChain(X509Chain chain, X509Certificate2 certificate, out X509ChainStatus[] status)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            var buildSuccess = chain.Build(certificate);
            status = new X509ChainStatus[chain.ChainStatus.Length];
            chain.ChainStatus.CopyTo(status, 0);

            // Check if time is not in the future
            return buildSuccess && !CertificateUtility.IsCertificateValidityPeriodInTheFuture(certificate);
        }

        // Ignore some chain status flags to special case them
        internal const X509ChainStatusFlags DefaultObservedStatusFlags =
            // To set the not ignored flags we have to turn on all the flags and then manually turn off ignored flags
            (~(X509ChainStatusFlags)0) &                      // Start with all flags
                                                              // These flags are ignored because they are known special cases
            (~X509ChainStatusFlags.NotTimeValid) &
            (~X509ChainStatusFlags.NotTimeNested) &           // Deprecated and therefore ignored.
            (~X509ChainStatusFlags.Revoked) &
            (~X509ChainStatusFlags.RevocationStatusUnknown) &
            (~X509ChainStatusFlags.CtlNotTimeValid) &
            (~X509ChainStatusFlags.OfflineRevocation);

        internal static bool ChainStatusListIncludesStatus(X509ChainStatus[] chainStatuses, X509ChainStatusFlags status, out IEnumerable<X509ChainStatus> chainStatus)
        {
            chainStatus = chainStatuses
                .Where(x => (x.Status & status) != 0);

            return chainStatus.Any();
        }

        internal static bool TryGetStatusMessage(X509ChainStatus[] chainStatuses, X509ChainStatusFlags status, out IEnumerable<string> messages)
        {
            messages = null;

            if (ChainStatusListIncludesStatus(chainStatuses, status, out var chainStatus))
            {
                messages = GetMessagesFromChainStatuses(chainStatus);

                return true;
            }

            return false;
        }

        internal static IEnumerable<string> GetMessagesFromChainStatuses(IEnumerable<X509ChainStatus> chainStatuses)
        {
            return chainStatuses
                .Select(x => x.StatusInformation?.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal);
        }
    }
}