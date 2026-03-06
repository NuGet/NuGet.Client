// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Security.Cryptography.X509Certificates;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Contains a request for generating package signature.
    /// </summary>
    public abstract class SignPackageRequest : IDisposable
    {
        private bool _isDisposed;

        /// <summary>
        /// Hash algorithm used to create the package signature.
        /// </summary>
        public HashAlgorithmName SignatureHashAlgorithm { get; }

        /// <summary>
        /// Hash algorithm used to timestamp the signed package.
        /// </summary>
        public HashAlgorithmName TimestampHashAlgorithm { get; }

        /// <summary>
        /// Certificate used to sign the package.
        /// </summary>
        public X509Certificate2 Certificate { get; }

        /// <summary>
        /// Gets a collection of additional certificates for building a chain for the signing certificate.
        /// </summary>
        public X509Certificate2Collection AdditionalCertificates { get; }

#if NET5_0_OR_GREATER
        /// <summary>
        /// Gets a collection of additional root certificates to trust during chain building.
        /// When non-empty, these roots are used as custom trust anchors alongside the system-trusted
        /// roots, allowing signing with certificates whose root CA is not in the machine or user
        /// trusted root store (e.g., when NoTrustedRootStore is enabled).
        /// </summary>
        /// <remarks>
        /// This property is only available on .NET 5+ where <see cref="X509ChainTrustMode.CustomRootTrust"/>
        /// and <see cref="X509ChainPolicy.CustomTrustStore"/> are supported.
        /// </remarks>
        public X509Certificate2Collection AdditionalTrustAnchors { get; }
#endif

        /// <summary>
        /// Gets the signature type.
        /// </summary>
        public abstract SignatureType SignatureType { get; }

        internal IX509CertificateChain Chain { get; private set; }

        /// <summary>
        /// PrivateKey is only used in mssign command.
        /// </summary>
        public System.Security.Cryptography.CngKey PrivateKey { get; set; }

        protected SignPackageRequest(
            X509Certificate2 certificate,
            HashAlgorithmName signatureHashAlgorithm,
            HashAlgorithmName timestampHashAlgorithm)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            if (!Enum.IsDefined(typeof(HashAlgorithmName), signatureHashAlgorithm) ||
                signatureHashAlgorithm == HashAlgorithmName.Unknown)
            {
                throw new ArgumentException(Strings.InvalidArgument, nameof(signatureHashAlgorithm));
            }

            if (!Enum.IsDefined(typeof(HashAlgorithmName), timestampHashAlgorithm) ||
                timestampHashAlgorithm == HashAlgorithmName.Unknown)
            {
                throw new ArgumentException(Strings.InvalidArgument, nameof(timestampHashAlgorithm));
            }

            Certificate = certificate;
            SignatureHashAlgorithm = signatureHashAlgorithm;
            TimestampHashAlgorithm = timestampHashAlgorithm;
            AdditionalCertificates = new X509Certificate2Collection();
#if NET5_0_OR_GREATER
            AdditionalTrustAnchors = new X509Certificate2Collection();
#endif
        }

        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            if (disposing)
            {
                Certificate?.Dispose();
                Chain?.Dispose();
                PrivateKey?.Dispose();
            }

            _isDisposed = true;
        }

        internal void BuildSigningCertificateChainOnce(ILogger logger)
        {
            if (Chain == null)
            {
#if NET5_0_OR_GREATER
                Chain = CertificateChainUtility.GetCertificateChain(
                    Certificate,
                    AdditionalCertificates,
                    logger,
                    CertificateType.Signature,
                    AdditionalTrustAnchors);
#else
                Chain = CertificateChainUtility.GetCertificateChain(
                    Certificate,
                    AdditionalCertificates,
                    logger,
                    CertificateType.Signature);
#endif
            }
        }
    }
}
