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
        /// Returns the path for the signature file.
        /// </summary>
        public abstract string SignaturePath { get; }

        /// <summary>
        /// Returns the set of allowed hash algorithms.
        /// </summary>
        public abstract string[] AllowedHashAlgorithms { get; }

        /// <summary>
        /// Returns the set of allowed hash algorithm Oids.
        /// </summary>
        public abstract string[] AllowedHashAlgorithmOids { get; }

        /// <summary>
        /// Returns minumum length required for RSA public keys
        /// </summary>
        public abstract int RSAPublicKeyMinLength { get; }

        /// <summary>
        /// Returns a set of signature major versions supported by this client
        /// </summary>
        public abstract int[] SupportedMajorVersions { get; }

        /// <summary>
        /// Initialize a signing specification with a root folder.
        /// </summary>
        protected SigningSpecifications()
        {
        }
    }
}
