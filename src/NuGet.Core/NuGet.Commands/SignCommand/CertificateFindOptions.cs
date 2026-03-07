// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System.Security.Cryptography.X509Certificates;
using System.Threading;
using NuGet.Commands.SignCommand;

namespace NuGet.Commands
{
    /// <summary>
    /// Source options for X.509 certificates.
    /// <seealso cref="CertificateProvider" />
    /// </summary>
    internal class CertificateSourceOptions
    {
        /// <summary>
        /// The certificate file path.
        /// </summary>
        public string CertificatePath { get; set; }

        /// <summary>
        /// The certificate password.
        /// </summary>
        public string CertificatePassword { get; set; }

        /// <summary>
        /// The certificate store name.
        /// </summary>
        public StoreName StoreName { get; set; }

        /// <summary>
        /// The certificate store location.
        /// </summary>
        public StoreLocation StoreLocation { get; set; }

        /// <summary>
        /// The certificate subject name or a substring to be used to search for the certificate.
        /// </summary>
        public string SubjectName { get; set; }

        /// <summary>
        /// SHA256 or SHA384 or SHA512 fingerprint of the certificate.
        /// </summary>
        public string Fingerprint { get; set; }

        /// <summary>
        /// bool used to indicate if the user can be prompted for password.
        /// </summary>
        public bool NonInteractive { get; set; }

        /// <summary>
        /// Password provider to get the password from user for opening a pfx file.
        /// </summary>
        public IPasswordProvider PasswordProvider { get; set; }

        /// <summary>
        /// Cancellation token.
        /// </summary>
        public CancellationToken Token { get; set; }

#if NET5_0_OR_GREATER
        /// <summary>
        /// Additional root certificates to trust during certificate chain validation.
        /// When provided, these are passed to <see cref="CertificateProvider"/> so that
        /// certificates with non-system-trusted roots can be discovered from stores.
        /// </summary>
        public X509Certificate2Collection AdditionalTrustAnchors { get; set; }
#endif

        /// <summary>
        /// When true, certificates with untrusted roots are allowed through store discovery.
        /// Used when trust anchor fingerprints will be verified later during signing.
        /// </summary>
        public bool AllowUntrustedRoot { get; set; }

    }
}
