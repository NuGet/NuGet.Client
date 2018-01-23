// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public sealed class AuthorSignPackageRequest : SignPackageRequest
    {
        /// <summary>
        /// Instantiates a new instance of the <see cref="AuthorSignPackageRequest" /> class.
        /// </summary>
        /// <param name="certificate">The signing certificate.</param>
        /// <param name="signatureHashAlgorithm">The signature hash algorithm.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="certificate" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="signatureHashAlgorithm" />
        /// is invalid.</exception>
        public AuthorSignPackageRequest(
            X509Certificate2 certificate,
            HashAlgorithmName signatureHashAlgorithm)
            : base(
                  certificate,
                  signatureHashAlgorithm,
                  signatureHashAlgorithm,
                  SignatureType.Author,
                  SignaturePlacement.PrimarySignature)
        {
        }

        /// <summary>
        /// Instantiates a new instance of the <see cref="AuthorSignPackageRequest" /> class.
        /// </summary>
        /// <param name="certificate">The signing certificate.</param>
        /// <param name="signatureHashAlgorithm">The signature hash algorithm.</param>
        /// <param name="timestampHashAlgorithm">The timestamp hash algorithm.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="certificate" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="signatureHashAlgorithm" />
        /// is invalid.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="timestampHashAlgorithm" />
        /// is invalid.</exception>
        public AuthorSignPackageRequest(
            X509Certificate2 certificate,
            HashAlgorithmName signatureHashAlgorithm,
            HashAlgorithmName timestampHashAlgorithm)
            : base(
                  certificate,
                  signatureHashAlgorithm,
                  timestampHashAlgorithm,
                  SignatureType.Author,
                  SignaturePlacement.PrimarySignature)
        {
        }
    }
}