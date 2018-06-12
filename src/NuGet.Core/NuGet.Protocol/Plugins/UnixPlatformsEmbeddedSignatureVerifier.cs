// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// Embedded Signature Verifier for the MacOS and Linux platforms.
    /// </summary>
    public class UnixPlatformsEmbeddedSignatureVerifier : EmbeddedSignatureVerifier
    {
        /// <summary>
        /// Checks if a file has a valid embedded signature.
        /// </summary>
        /// <param name="filePath">The path of a file to be checked.</param>
        /// <returns><c>true</c> if the file has a valid signature; otherwise, <c>false</c>.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="filePath" />
        /// is either <c>null</c> or an empty string.</exception>
        /// <exception cref="PlatformNotSupportedException">Thrown if the current platform is unsupported.</exception>
        public override bool IsValid(string filePath)
        {
            // There's no embedded signature verification on Linux and MacOS platforms
            return true;
        }
    }
}
