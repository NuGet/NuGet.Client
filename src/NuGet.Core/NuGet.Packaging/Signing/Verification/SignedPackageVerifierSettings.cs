// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Feed settings used to verify packages.
    /// </summary>
    public sealed class SignedPackageVerifierSettings
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

        public bool AllowAlwaysVerifyingCountersignature { get; }

        /// <summary>
        /// Treat unknown revocation status as a warning instead of an error during verification.
        /// </summary>
        public bool AllowUnknownRevocation { get; }

        /// <summary>
        /// Allow an empty or null RepositoryCertificateList.
        /// </summary>
        public bool AllowNoRepositoryCertificateList { get; }

        /// <summary>
        /// Allow an empty or null ClientCertificateList.
        /// </summary>
        public bool AllowNoClientCertificateList { get; }

        /// <summary>
        /// Allowlist of repository certificates hashes.
        /// </summary>
        public IReadOnlyList<VerificationAllowListEntry> RepositoryCertificateList { get; }

        /// <summary>
        /// Allowlist of client side certificate hashes.
        /// </summary>
        public IReadOnlyList<VerificationAllowListEntry> ClientCertificateList { get; }

        public SignedPackageVerifierSettings(
            bool allowUnsigned,
            bool allowIllegal,
            bool allowUntrusted,
            bool allowIgnoreTimestamp,
            bool allowMultipleTimestamps,
            bool allowNoTimestamp,
            bool allowUnknownRevocation,
            bool allowNoRepositoryCertificateList,
            bool allowNoClientCertificateList,
            bool allowAlwaysVerifyingCountersignature)
            : this (
                  allowUnsigned,
                  allowIllegal,
                  allowUntrusted,
                  allowIgnoreTimestamp,
                  allowMultipleTimestamps,
                  allowNoTimestamp,
                  allowUnknownRevocation,
                  allowNoRepositoryCertificateList,
                  allowNoClientCertificateList,
                  allowAlwaysVerifyingCountersignature,
                  repoAllowListEntries: null,
                  clientAllowListEntries: null)
        {
        }

        public SignedPackageVerifierSettings(
            bool allowUnsigned,
            bool allowIllegal,
            bool allowUntrusted,
            bool allowIgnoreTimestamp,
            bool allowMultipleTimestamps,
            bool allowNoTimestamp,
            bool allowUnknownRevocation,
            bool allowNoRepositoryCertificateList,
            bool allowNoClientCertificateList,
            bool allowAlwaysVerifyingCountersignature,
            IReadOnlyList<VerificationAllowListEntry> repoAllowListEntries,
            IReadOnlyList<VerificationAllowListEntry> clientAllowListEntries)
        {
            AllowUnsigned = allowUnsigned;
            AllowIllegal = allowIllegal;
            AllowUntrusted = allowUntrusted;
            AllowIgnoreTimestamp = allowIgnoreTimestamp;
            AllowMultipleTimestamps = allowMultipleTimestamps;
            AllowNoTimestamp = allowNoTimestamp;
            AllowUnknownRevocation = allowUnknownRevocation;
            AllowNoRepositoryCertificateList = allowNoRepositoryCertificateList;
            AllowNoClientCertificateList = allowNoClientCertificateList;
            AllowAlwaysVerifyingCountersignature = allowAlwaysVerifyingCountersignature;
            RepositoryCertificateList = repoAllowListEntries;
            ClientCertificateList = clientAllowListEntries;
        }

        /// <summary>
        /// Default settings.
        /// </summary>
        public static SignedPackageVerifierSettings GetDefault(
            IReadOnlyList<VerificationAllowListEntry> repoAllowListEntries = null,
            IReadOnlyList<VerificationAllowListEntry> clientAllowListEntries = null)
        {
            return new SignedPackageVerifierSettings(
                allowUnsigned: true,
                allowIllegal: true,
                allowUntrusted: true,
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                allowNoRepositoryCertificateList: true,
                allowNoClientCertificateList: true,
                allowAlwaysVerifyingCountersignature: true,
                repoAllowListEntries: repoAllowListEntries,
                clientAllowListEntries: clientAllowListEntries);
        }

        /// <summary>
        /// Default policy for scenarios in VS in accept mode
        /// </summary>
        public static SignedPackageVerifierSettings GetAcceptModeDefaultPolicy(
            IReadOnlyList<VerificationAllowListEntry> repoAllowListEntries = null,
            IReadOnlyList<VerificationAllowListEntry> clientAllowListEntries = null)
        {
            return new SignedPackageVerifierSettings(
                allowUnsigned: true,
                allowIllegal: true,
                allowUntrusted: true,
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                allowNoRepositoryCertificateList: true,
                allowNoClientCertificateList: true,
                allowAlwaysVerifyingCountersignature: false,
                repoAllowListEntries: repoAllowListEntries,
                clientAllowListEntries: clientAllowListEntries);
        }

        /// <summary>
        /// Default policy for scenarios in VS in require mode
        /// </summary>
        public static SignedPackageVerifierSettings GetRequireModeDefaultPolicy(
            IReadOnlyList<VerificationAllowListEntry> repoAllowListEntries = null,
            IReadOnlyList<VerificationAllowListEntry> clientAllowListEntries = null)
        {
            return new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                allowNoRepositoryCertificateList: false,
                allowNoClientCertificateList: false,
                allowAlwaysVerifyingCountersignature: false,
                repoAllowListEntries: repoAllowListEntries,
                clientAllowListEntries: clientAllowListEntries);
        }

        /// <summary>
        /// Default policy for nuget.exe verify --signatures command.
        /// </summary>
        public static SignedPackageVerifierSettings GetVerifyCommandDefaultPolicy(
            IReadOnlyList<VerificationAllowListEntry> repoAllowListEntries = null,
            IReadOnlyList<VerificationAllowListEntry> clientAllowListEntries = null)
        {
            return new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                allowNoRepositoryCertificateList: true,
                allowNoClientCertificateList: true,
                allowAlwaysVerifyingCountersignature: true,
                repoAllowListEntries: repoAllowListEntries,
                clientAllowListEntries: clientAllowListEntries);
        }
    }
}