// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Shared;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Feed settings used to verify packages.
    /// </summary>
    public class SignedPackageVerifierSettings
    {
        /// <summary>
        /// Allow packages that do not contain signatures.
        /// </summary>
        public bool AllowUnsigned { get; }

        /// <summary>
        /// Allow packages with signatures that do not conform to the specification.
        /// </summary>
        public bool AllowIllegal { get; }

        /// <summary>
        /// Allow packages that have not been explicitly trusted by the consumer.
        /// </summary>
        public bool AllowUntrusted { get; }

        /// <summary>
        /// Allow packages that do not chain to a trusted root certificate.
        /// </summary>
        public bool AllowUntrustedSelfIssuedCertificate { get; }

        /// <summary>
        /// Allow ignoring timestamp.
        /// </summary>
        public bool AllowIgnoreTimestamp { get; }

        /// <summary>
        /// Allow more than one timestamp.
        /// </summary>
        public bool AllowMultipleTimestamps { get; }

        /// <summary>
        /// Allow no timestamp.
        /// </summary>
        public bool AllowNoTimestamp { get; }

        /// <summary>
        /// Treat unknown revocation status as a warning instead of an error during verification.
        /// </summary>
        public bool AllowUnknownRevocation { get; }       

        /// <summary>
        /// Allowlist of repository certificates hashes.
        /// </summary>
        public IReadOnlyList<VerificationAllowListEntry> RepositoryAllowListEntries { get; }

        /// <summary>
        /// Allowlist of client side certificate hashes.
        /// </summary>
        public IReadOnlyList<VerificationAllowListEntry> ClientAllowListEntries { get; }


        public SignedPackageVerifierSettings(
            bool allowUnsigned,
            bool allowIllegal,
            bool allowUntrusted,
            bool allowUntrustedSelfIssuedCertificate,
            bool allowIgnoreTimestamp,
            bool allowMultipleTimestamps,
            bool allowNoTimestamp,
            bool allowUnknownRevocation)
        {
            AllowUnsigned = allowUnsigned;
            AllowIllegal = allowIllegal;
            AllowUntrusted = allowUntrusted;
            AllowUntrustedSelfIssuedCertificate = allowUntrustedSelfIssuedCertificate;
            AllowIgnoreTimestamp = allowIgnoreTimestamp;
            AllowMultipleTimestamps = allowMultipleTimestamps;
            AllowNoTimestamp = allowNoTimestamp;
            AllowUnknownRevocation = allowUnknownRevocation;
        }

        public SignedPackageVerifierSettings(
            bool allowUnsigned,
            bool allowIllegal,
            bool allowUntrusted,
            bool allowUntrustedSelfIssuedCertificate,
            bool allowIgnoreTimestamp,
            bool allowMultipleTimestamps,
            bool allowNoTimestamp,
            bool allowUnknownRevocation,
            IReadOnlyList<VerificationAllowListEntry> repoAllowListEntries,
            IReadOnlyList<VerificationAllowListEntry> clientAllowListEntries)
        {
            AllowUnsigned = allowUnsigned;
            AllowIllegal = allowIllegal;
            AllowUntrusted = allowUntrusted;
            AllowUntrustedSelfIssuedCertificate = allowUntrustedSelfIssuedCertificate;
            AllowIgnoreTimestamp = allowIgnoreTimestamp;
            AllowMultipleTimestamps = allowMultipleTimestamps;
            AllowNoTimestamp = allowNoTimestamp;
            AllowUnknownRevocation = allowUnknownRevocation;
            RepositoryAllowListEntries = repoAllowListEntries;
            ClientAllowListEntries = clientAllowListEntries;
        }

        /// <summary>
        /// Default settings.
        /// </summary>
        public static SignedPackageVerifierSettings Default(
            IReadOnlyList<VerificationAllowListEntry> repoAllowListEntries = null,
            IReadOnlyList<VerificationAllowListEntry> clientAllowListEntries = null)
        {
            return new SignedPackageVerifierSettings(
                allowUnsigned: true,
                allowIllegal: true,
                allowUntrusted: true,
                allowUntrustedSelfIssuedCertificate: true,
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                repoAllowListEntries: repoAllowListEntries,
                clientAllowListEntries: clientAllowListEntries);
        }

        /// <summary>
        /// Allow all.
        /// </summary>
        public static SignedPackageVerifierSettings AllowAll(
            IReadOnlyList<VerificationAllowListEntry> repoAllowListEntries = null,
            IReadOnlyList<VerificationAllowListEntry> clientAllowListEntries = null)
        {
            return new SignedPackageVerifierSettings(
                allowUnsigned: true,
                allowIllegal: true,
                allowUntrusted: true,
                allowUntrustedSelfIssuedCertificate: true,
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                repoAllowListEntries: repoAllowListEntries,
                clientAllowListEntries: clientAllowListEntries);
        }

        /// <summary>
        /// Default policy for scenarios in VS.
        /// </summary>
        public static SignedPackageVerifierSettings VSClientDefaultPolicy(
            IReadOnlyList<VerificationAllowListEntry> repoAllowListEntries = null,
            IReadOnlyList<VerificationAllowListEntry> clientAllowListEntries = null)
        {
            return new SignedPackageVerifierSettings(
                allowUnsigned: true,
                allowIllegal: true,
                allowUntrusted: true,
                allowUntrustedSelfIssuedCertificate: true,
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                repoAllowListEntries: repoAllowListEntries,
                clientAllowListEntries: clientAllowListEntries);
        }

        /// <summary>
        /// Default policy for nuget.exe verify --signatures command.
        /// </summary>
        public static SignedPackageVerifierSettings VerifyCommandDefaultPolicy(
            IReadOnlyList<VerificationAllowListEntry> repoAllowListEntries = null,
            IReadOnlyList<VerificationAllowListEntry> clientAllowListEntries = null)
        {
            return new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowUntrustedSelfIssuedCertificate: true,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                repoAllowListEntries: repoAllowListEntries,
                clientAllowListEntries: clientAllowListEntries);
        }
    }
}