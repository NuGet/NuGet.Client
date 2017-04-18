// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

namespace NuGet.Protocol
{
    /// <summary>
    /// Base class for embedded signature verifiers.
    /// </summary>
    public abstract class EmbeddedSignatureVerifier
    {
        /// <summary>
        /// Checks if a file has a valid embedded signature.
        /// </summary>
        /// <param name="filePath">The path of a file to be checked.</param>
        /// <returns><c>true</c> if the file has a valid signature; otherwise, <c>false</c>.</returns>
        public abstract bool IsValid(string filePath);

        /// <summary>
        /// Creates an embedded signature verifier for the current platform.
        /// </summary>
        /// <returns>An embedded signature verifier or <c>null</c> if the current platform is not supported.</returns>
        public static EmbeddedSignatureVerifier CreateOrNull()
        {
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                return new WindowsEmbeddedSignatureVerifier();
            }

            return null;
        }
    }
}