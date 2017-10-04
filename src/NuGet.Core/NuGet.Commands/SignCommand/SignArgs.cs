// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

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
        public string HashingAlgorithm { get; set; }

        /// <summary>
        /// Password for the certificate, if needed.
        /// </summary>
        public string CertificatePassphrase { get; set; }

        /// <summary>
        /// RSA signature padding to be used while signing the package.
        /// </summary>
        public string RSASignaturePadding { get; set; }

        /// <summary>
        /// Switch used to indicate if an existing signature should be overwritten.
        /// </summary>
        public bool Force { get; set; }

    }
}
