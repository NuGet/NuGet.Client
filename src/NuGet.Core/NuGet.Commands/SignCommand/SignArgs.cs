// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using NuGet.Common;
using NuGet.Packaging.Signing;

namespace NuGet.Commands
{
    /// <summary>
    /// Object to hold params passed to the sign command and pass to the sign command runner.
    /// </summary>
    public class SignArgs
    {
        /// <summary>
        /// Path to the package that has to be signed.
        /// </summary>
        public string PackagePath { get; set; }

        /// <summary>
        /// Output directory where the signed package should be dropped.
        /// </summary>
        public string OutputDirectory { get; set; }

        /// <summary>
        /// Path to a Certificate file.
        /// </summary>
        public string CertificatePath { get; set; }

        /// <summary>
        /// Name of the store to be used when searching for a certificate.
        /// </summary>
        public StoreName CertificateStoreName { get; set; }

        /// <summary>
        /// Location of the store to be used when searching for a certificate.
        /// </summary>
        public StoreLocation CertificateStoreLocation { get; set; }

        /// <summary>
        /// Subject Name for the certificate that can be used to search the local certificate store.
        /// </summary>
        public string CertificateSubjectName { get; set; }

        /// <summary>
        /// Fingerprint for the certificate that can be used to search the local certificate store.
        /// </summary>
        public string CertificateFingerprint { get; set; }

        /// <summary>
        /// Cryptographic Service Provider name that can be used to get the private key.
        /// </summary>
        public string CryptographicServiceProvider { get; set; }

        /// <summary>
        /// Key Container name that can be used to get the private key.
        /// </summary>
        public string KeyContainer { get; set; }

        /// <summary>
        /// Hashing Algorithm to be used to digest the package files.
        /// </summary>
        public HashAlgorithmName SignatureHashAlgorithm { get; set; }

        /// <summary>
        /// URL to an RFC 3161 timestamp server.
        /// </summary>
        public string Timestamper { get; set; }

        /// <summary>
        /// Hashing Algorithm to be used by the RFC 3161 time stamp server.
        /// </summary>
        public HashAlgorithmName TimestampHashAlgorithm { get; set; }

        /// <summary>
        /// Password for a pfx file, if needed.
        /// </summary>
        public string CertificatePassword { get; set; }

        /// <summary>
        /// Switch used to indicate if an existing signature should be overwritten.
        /// </summary>
        public bool Overwrite { get; set; }

        /// <summary>
        /// Switch used to indicate that we should not prompt for user input or confirmations.
        /// </summary>
        public bool NonInteractive { get; set; }

        /// <summary>
        /// Logger to be used to display the logs during the execution of sign command.
        /// </summary>
        public ILogger Logger { get; set; }
    }
}
