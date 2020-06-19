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
        /// <param name="certificate">The certificate for which a chain should be built.</param>
        /// <param name="extraStore">A certificate store containing additional certificates necessary
        /// for chain building.</param>
        /// <param name="logger">A logger.</param>
        /// <param name="certificateType">The certificate type.</param>
        /// <returns>A certificate chain.</returns>
        /// <remarks>This is intended to be used only during signing and timestamping operations,
        /// not verification.</remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="certificate" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="extraStore" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="logger" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="certificateType" /> is undefined.</exception>
        public static IX509CertificateChain GetCertificateChain(
            X509Certificate2 certificate,
            X509Certificate2Collection extraStore,
            ILogger logger,
            CertificateType certificateType)
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

            if (!Enum.IsDefined(typeof(CertificateType), certificateType))
            {
                throw new ArgumentException(Strings.InvalidArgument, nameof(certificateType));
            }

            using (var chainHolder = new X509ChainHolder())
            {
                var chain = chainHolder.Chain;

                SetCertBuildChainPolicy(
                    chain.ChainPolicy,
                    extraStore,
                    DateTime.Now,
                    certificateType);

                chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;

                if (chain.Build(certificate))
                {
                    return GetCertificateChain(chain);
                }

                X509ChainStatusFlags errorStatusFlags;
                X509ChainStatusFlags warningStatusFlags;

                GetChainStatusFlags(certificate, certificateType, out errorStatusFlags, out warningStatusFlags);

                var fatalStatuses = new List<X509ChainStatus>();
                var logCode = certificateType == CertificateType.Timestamp ? NuGetLogCode.NU3028 : NuGetLogCode.NU3018;

                foreach (var chainStatus in chain.ChainStatus)
                {
                    if ((chainStatus.Status & errorStatusFlags) != 0)
                    {
                        fatalStatuses.Add(chainStatus);
                        logger.Log(LogMessage.CreateError(logCode, $"{chainStatus.Status}: {chainStatus.StatusInformation?.Trim()}"));
                    }
                    else if ((chainStatus.Status & warningStatusFlags) != 0)
                    {
                        logger.Log(LogMessage.CreateWarning(logCode, $"{chainStatus.Status}: {chainStatus.StatusInformation?.Trim()}"));
                    }
                }

                if (fatalStatuses.Any())
                {
                    if (certificateType == CertificateType.Timestamp)
                    {
                        throw new TimestampException(logCode, Strings.CertificateChainValidationFailed);
                    }

                    throw new SignatureException(logCode, Strings.CertificateChainValidationFailed);
                }

                return GetCertificateChain(chain);
            }
        }

        /// <summary>
        /// Create an ordered list of certificates. The leaf node is returned first.
        /// </summary>
        /// <param name="x509Chain">Certificate chain to be converted to list.</param>
        /// <remarks>This does not check validity or trust. It returns the chain as-is.</remarks>
        public static IX509CertificateChain GetCertificateChain(X509Chain x509Chain)
        {
            if (x509Chain == null)
            {
                throw new ArgumentNullException(nameof(x509Chain));
            }

            var certs = new X509CertificateChain();

            foreach (var item in x509Chain.ChainElements)
            {
                // Return a new certificate object.
                // This allows the chain and its chain element certificates to be disposed
                // in both success and error cases.
                certs.Add(new X509Certificate2(item.Certificate.RawData));
            }

            return certs;
        }

        private static void GetChainStatusFlags(
            X509Certificate2 certificate,
            CertificateType certificateType,
            out X509ChainStatusFlags errorStatusFlags,
            out X509ChainStatusFlags warningStatusFlags)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            warningStatusFlags = X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation;

            if (certificateType == CertificateType.Signature && CertificateUtility.IsSelfIssued(certificate))
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
            CertificateType certificateType)
        {
            if (certificateType == CertificateType.Timestamp)
            {
                policy.ApplicationPolicy.Add(new Oid(Oids.TimeStampingEku));
            }
            else
            {
                policy.ApplicationPolicy.Add(new Oid(Oids.CodeSigningEku));
            }

            policy.ExtraStore.AddRange(additionalCertificates);

            policy.RevocationFlag = X509RevocationFlag.ExcludeRoot;

            if (certificateType != CertificateType.Timestamp)
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
            (~X509ChainStatusFlags.OfflineRevocation) &
            (~X509ChainStatusFlags.UntrustedRoot);

        internal static bool ChainStatusListIncludesStatus(X509ChainStatus[] chainStatuses, X509ChainStatusFlags status, out IEnumerable<X509ChainStatus> chainStatus)
        {
            chainStatus = chainStatuses
                .Where(x => (x.Status & status) != 0);

            return chainStatus.Any();
        }

        internal static bool TryGetStatusAndMessage(X509ChainStatus[] chainStatuses, X509ChainStatusFlags status, out IEnumerable<string> statusAndMessages)
        {
            statusAndMessages = null;

            if (ChainStatusListIncludesStatus(chainStatuses, status, out var chainStatus))
            {
                statusAndMessages = GetStatusAndMessagesFromChainStatuses(chainStatus);

                return true;
            }

            return false;
        }

        internal static IEnumerable<string> GetStatusAndMessagesFromChainStatuses(IEnumerable<X509ChainStatus> chainStatuses)
        {
            return chainStatuses
                .Where(x => !string.IsNullOrEmpty(x.StatusInformation?.Trim()))
                .Select(x => $"{x.Status}: {x.StatusInformation?.Trim()}")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal);
        }
    }
}
