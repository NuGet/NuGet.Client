// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Abstract class representing which paths may be used for signing in a package.
    /// </summary>
    public abstract class SigningSpecifications
    {
        /// <summary>
        /// v1.0.0 signing settings
        /// </summary>
        public static readonly SigningSpecificationsV1 V1 = new SigningSpecificationsV1();

        /// <summary>
        /// Returns the set of allowed signature file paths
        /// in the package. This may also contain
        /// optional signing file paths.
        /// </summary>
        public abstract string[] AllowedPaths { get; }

        /// <summary>
        /// Returns the set of allowed hash algorithms.
        /// </summary>
        public abstract string[] AllowedHashAlgorithms { get; }

        /// <summary>
        /// Paths required for the package to be considered
        /// signed.
        /// </summary>
        public abstract string[] RequiredPaths { get; }

        /// <summary>
        /// Root signature folder in the package.
        /// </summary>
        public string SignatureFolder { get; }

        /// <summary>
        /// Initialize a signing specification with a root folder.
        /// </summary>
        /// <param name="signatureFolder">Root folder within a package where signature files are stored.</param>
        protected SigningSpecifications(string signatureFolder)
        {
            SignatureFolder = signatureFolder ?? throw new ArgumentNullException(nameof(signatureFolder));
        }
    }
}
