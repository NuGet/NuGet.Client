// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System.Security.Cryptography.X509Certificates;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class SignPackageRequest
    {
        /// <summary>
        /// X509Certificate2 object which should be used to sign the package.
        /// </summary>
        public X509Certificate2 Certificate { get; set; }

        /// <summary>
        /// Hashing Algorithm to be used to digest the package files.
        /// </summary>
        public HashAlgorithmName HashingAlgorithm { get; set; }

        /// <summary>
        /// URL to an RFC 3161 timestamp server.
        /// </summary>
        public string Timestamper { get; set; }

        /// <summary>
        /// Hashing Algorithm to be used by the RFC 3161 time stamp server.
        /// </summary>
        public HashAlgorithmName TimestampHashAlgorithm { get; set; }

        /// <summary>
        /// Logger to be used to display the logs during the execution of sign command.
        /// </summary>
        public ILogger Logger { get; set; }
    }
}