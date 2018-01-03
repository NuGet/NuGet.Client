// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Contains a request for generating package signature.
    /// </summary>
    public sealed class SignPackageRequest : IDisposable
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

        internal IReadOnlyList<X509Certificate2> Chain { get; private set; }

#if IS_DESKTOP
        /// <summary>
        /// PrivateKey is only used in mssign command.
        /// </summary>
        public System.Security.Cryptography.CngKey PrivateKey { get; set; }

#endif

        /// <summary>
        /// Instantiates a new instance of the <see cref="SignPackageRequest" /> class.
        /// </summary>
        /// <param name="certificate">The signing certificate.</param>
        /// <param name="signatureHashAlgorithm">The signature hash algorithm.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="certificate" />
        /// is <c>null</c>.</exception>
        public SignPackageRequest(
            X509Certificate2 certificate,
            HashAlgorithmName signatureHashAlgorithm)
            : this(certificate, signatureHashAlgorithm, HashAlgorithmName.SHA256)
        {
        }

        /// <summary>
        /// Instantiates a new instance of the <see cref="SignPackageRequest" /> class.
        /// </summary>
        /// <param name="certificate">The signing certificate.</param>
        /// <param name="signatureHashAlgorithm">The signature hash algorithm.</param>
        /// <param name="timestampHashAlgorithm">The timestamp hash algorithm.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="certificate" />
        /// is <c>null</c>.</exception>
        public SignPackageRequest(
            X509Certificate2 certificate,
            HashAlgorithmName signatureHashAlgorithm,
            HashAlgorithmName timestampHashAlgorithm)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            Certificate = certificate;
            SignatureHashAlgorithm = signatureHashAlgorithm;
            TimestampHashAlgorithm = timestampHashAlgorithm;
            SignatureType = SignatureType.Author;
            AdditionalCertificates = new X509Certificate2Collection();
        }

        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                Certificate?.Dispose();
#if IS_DESKTOP
                PrivateKey?.Dispose();
#endif

                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Gets the signature type.
        /// </summary>
        public SignatureType SignatureType { get; }

        internal void BuildCertificateChainOnce()
        {
            if (Chain == null)
            {
#if IS_DESKTOP
                Chain = SigningUtility.GetCertificateChain(Certificate, AdditionalCertificates);
#else
                throw new NotImplementedException();
#endif
            }
        }
    }
}