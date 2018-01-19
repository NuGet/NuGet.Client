// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
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
        public static IReadOnlyList<X509Certificate2> GetCertificateChainForSigning(
            X509Certificate2 certificate,
            X509Certificate2Collection extraStore,
            NuGetVerificationCertificateType certificateType)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            if (extraStore == null)
            {
                throw new ArgumentNullException(nameof(extraStore));
            }

            using (var chainHolder = new X509ChainHolder())
            {
                var chain = chainHolder.Chain;

                SetCertBuildChainPolicy(
                    chain.ChainPolicy,
                    extraStore,
                    DateTime.Now,
                    certificateType);

                if (BuildCertificateChain(chain, certificate, out var chainStatuses))
                {
                    return GetCertificateListFromChain(chain);
                }

                var messages = GetMessagesFromChainStatuses(chainStatuses);

                throw new SignatureException(NuGetLogCode.NU3018, string.Format(CultureInfo.CurrentCulture, Strings.ErrorInvalidCertificateChain, string.Join(", ", messages)));
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


        internal static void SetCertBuildChainPolicy(
            X509ChainPolicy policy,
            X509Certificate2Collection additionalCertificates,
            DateTime verificationTime,
            NuGetVerificationCertificateType certificateType)
        {
            if (certificateType == NuGetVerificationCertificateType.Signature)
            {
                policy.ApplicationPolicy.Add(new Oid(Oids.CodeSigningEku));
            }
            else if (certificateType == NuGetVerificationCertificateType.Timestamp)
            {
                policy.ApplicationPolicy.Add(new Oid(Oids.TimeStampingEku));
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
        internal const X509ChainStatusFlags NotIgnoredCertificateFlags =
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

            if (chainStatus.Any())
            {
                return true;
            }

            return false;
        }

        internal static bool TryGetStatusMessage(X509ChainStatus[] chainStatuses, X509ChainStatusFlags status, out IEnumerable<string> messages)
        {
            messages = null;

            if (ChainStatusListIncludesStatus(chainStatuses, status, out var chainStatus))
            {
                messages = GetMessagesFromChainStatuses(chainStatus.ToArray());

                return true;
            }

            return false;
        }

        internal static IEnumerable<string> GetMessagesFromChainStatuses(X509ChainStatus[] chainStatuses)
        {
            return chainStatuses
                .Select(x => x.StatusInformation?.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal);
        }
    }
}
