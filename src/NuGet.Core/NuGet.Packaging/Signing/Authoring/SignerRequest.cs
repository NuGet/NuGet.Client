// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NuGet.Packaging.Signing
{
    public sealed class SignerRequest
    {
        /// <summary>
        /// Path to the package that has to be signed.
        /// Signing operation cannot be done in place, therefore this value should be different than the OutputPath.
        /// </summary>
        public string PackagePath { get; }

        /// <summary>
        /// Output directory where the signed package should be dropped.
        /// Signing operation cannot be done in place, therefore this value should be different than the PackagePath.
        /// </summary>
        public string OutputPath { get; }

        /// <summary>
        /// Switch used to indicate if an existing signature should be overwritten.
        /// </summary>
        public bool Overwrite { get; }

        /// <summary>
        /// Provider to create a Signature that can be added to the package.
        /// </summary>
        public ISignatureProvider SignatureProvider { get; }

        /// <summary>
        /// Request to sign packages
        /// </summary>
        public SignPackageRequest SignRequest { get; }

        public SignerRequest(string packagePath, string outputPath, bool overwrite, ISignatureProvider signatureProvider, SignPackageRequest signRequest)
        {
            PackagePath = packagePath ?? throw new ArgumentNullException(nameof(packagePath));
            OutputPath = outputPath ?? throw new ArgumentNullException(nameof(outputPath));

            if (StringComparer.Ordinal.Equals(packagePath, outputPath))
            {
                throw new ArgumentException(Strings.SigningCannotBeDoneInPlace);
            }

            SignatureProvider = signatureProvider ?? throw new ArgumentNullException(nameof(signatureProvider));
            SignRequest = signRequest ?? throw new ArgumentNullException(nameof(signRequest));
            Overwrite = overwrite;
        }
    }
}
