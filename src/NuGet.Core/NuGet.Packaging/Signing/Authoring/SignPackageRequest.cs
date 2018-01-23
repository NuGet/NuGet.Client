// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// Gets the repository V3 service index URL.
        /// </summary>
        public Uri V3ServiceIndexUrl { get; }

        /// <summary>
        /// Gets a read-only list of package owners.
        /// </summary>
        public IReadOnlyList<string> PackageOwners { get; }

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
            : this(certificate, signatureHashAlgorithm, signatureHashAlgorithm)
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
            : this(
                  certificate,
                  signatureHashAlgorithm,
                  timestampHashAlgorithm,
                  SignatureType.Author,
                  SignaturePlacement.PrimarySignature,
                  v3ServiceIndexUrl: null,
                  packageOwners: null)
        {
        }

        /// <summary>
        /// Instantiates a new instance of the <see cref="SignPackageRequest" /> class.
        /// </summary>
        /// <param name="certificate">The signing certificate.</param>
        /// <param name="signatureHashAlgorithm">The signature hash algorithm.</param>
        /// <param name="timestampHashAlgorithm">The timestamp hash algorithm.</param>
        /// <param name="signaturePlacement">The signature placement.</param>
        /// <param name="v3ServiceIndexUrl">The V3 service index URL.</param>
        /// <param name="packageOwners">A read-only list of package owners.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="certificate" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="signatureHashAlgorithm" />
        /// is invalid.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="timestampHashAlgorithm" />
        /// is invalid.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="signaturePlacement" />
        /// is invalid.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="v3ServiceIndexUrl" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="v3ServiceIndexUrl" />
        /// is neither absolute nor HTTPS.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="packageOwners" />
        /// is either empty or contains an invalid value.</exception>
        public SignPackageRequest(
            X509Certificate2 certificate,
            HashAlgorithmName signatureHashAlgorithm,
            HashAlgorithmName timestampHashAlgorithm,
            SignaturePlacement signaturePlacement,
            Uri v3ServiceIndexUrl,
            IReadOnlyList<string> packageOwners)
            : this(
                  certificate,
                  signatureHashAlgorithm,
                  timestampHashAlgorithm,
                  SignatureType.Repository,
                  signaturePlacement,
                  v3ServiceIndexUrl,
                  packageOwners)
        {
        }

        private SignPackageRequest(
            X509Certificate2 certificate,
            HashAlgorithmName signatureHashAlgorithm,
            HashAlgorithmName timestampHashAlgorithm,
            SignatureType signatureType,
            SignaturePlacement signaturePlacement,
            Uri v3ServiceIndexUrl,
            IReadOnlyList<string> packageOwners)
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

            if (signatureType != SignatureType.Author && signatureType != SignatureType.Repository)
            {
                throw new ArgumentException(Strings.InvalidArgument, nameof(signatureType));
            }

            if (!Enum.IsDefined(typeof(SignaturePlacement), signaturePlacement))
            {
                throw new ArgumentException(Strings.InvalidArgument, nameof(signaturePlacement));
            }

            if (signatureType == SignatureType.Repository)
            {
                if (v3ServiceIndexUrl == null)
                {
                    throw new ArgumentNullException(nameof(v3ServiceIndexUrl));
                }

                if (!v3ServiceIndexUrl.IsAbsoluteUri)
                {
                    throw new ArgumentException(Strings.InvalidUrl, nameof(v3ServiceIndexUrl));
                }

                if (!string.Equals(v3ServiceIndexUrl.Scheme, "https", StringComparison.Ordinal))
                {
                    throw new ArgumentException(Strings.InvalidUrl, nameof(v3ServiceIndexUrl));
                }

                if (packageOwners != null)
                {
                    if (packageOwners.Any(packageOwner => string.IsNullOrWhiteSpace(packageOwner)))
                    {
                        throw new ArgumentException(Strings.NuGetPackageOwnersInvalidValue, nameof(packageOwners));
                    }

                    if (!packageOwners.Any())
                    {
                        packageOwners = null;
                    }
                }
            }

            Certificate = certificate;
            SignatureHashAlgorithm = signatureHashAlgorithm;
            TimestampHashAlgorithm = timestampHashAlgorithm;
            SignatureType = signatureType;
            SignaturePlacement = signaturePlacement;
            AdditionalCertificates = new X509Certificate2Collection();
            V3ServiceIndexUrl = v3ServiceIndexUrl;
            PackageOwners = packageOwners;
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

        /// <summary>
        /// Gets the signature placement.
        /// </summary>
        public SignaturePlacement SignaturePlacement { get; }

        internal void BuildSigningCertificateChainOnce()
        {
            if (Chain == null)
            {
                Chain = CertificateChainUtility.GetCertificateChainForSigning(Certificate, AdditionalCertificates, NuGetVerificationCertificateType.Signature);
            }
        }
    }
}