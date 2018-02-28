// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public sealed class SigningOptions
    {
        /// <summary>
        /// Path to the package file that will be used as an input for any signer operation.
        /// <remarks>Signer operations cannot be done in place, therefore this value should be different than OutputFilePath.</remarks>
        /// </summary>
        public string PackageFilePath { get; }

        /// <summary>
        /// Path to the file that will be used as an output for any signer operation.
        /// <remarks>Signer operations cannot be done in place, therefore this value should be different than PackageFilePath.</remarks>
        /// </summary>
        public string OutputFilePath { get; }

        /// <summary>
        /// Switch used to indicate if an existing signature should be overwritten.
        /// </summary>
        public bool Overwrite { get; }

        /// <summary>
        /// Provider to create a Signature that can be added to the package.
        /// </summary>
        public ISignatureProvider SignatureProvider { get; }

        /// <summary>
        /// Logger to be used to display the logs during the execution of signer actions.
        /// </summary>
        public ILogger Logger { get; }

        public SigningOptions(string packageFilePath, string outputFilePath, bool overwrite, ISignatureProvider signatureProvider, ILogger logger)
        {
            PackageFilePath = packageFilePath ?? throw new ArgumentNullException(nameof(packageFilePath));
            OutputFilePath = outputFilePath ?? throw new ArgumentNullException(nameof(outputFilePath));

            if (StringComparer.OrdinalIgnoreCase.Equals(NormalizeFilePath(packageFilePath), NormalizeFilePath(outputFilePath)))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.SigningCannotBeDoneInPlace, nameof(packageFilePath), nameof(outputFilePath)));
            }

            SignatureProvider = signatureProvider ?? throw new ArgumentNullException(nameof(signatureProvider));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Overwrite = overwrite;
        }

        private static string NormalizeFilePath(string filePath)
        {
            return Path.GetFullPath(filePath).TrimEnd('\\');
        }
    }
}
