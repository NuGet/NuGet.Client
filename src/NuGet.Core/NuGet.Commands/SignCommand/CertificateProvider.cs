// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NuGet.Commands
{
    internal static class CertificateProvider
    {
        // "The system cannot find the file specified." (ERROR_FILE_NOT_FOUND)
        private const int ERROR_FILE_NOT_FOUND_HRESULT = unchecked((int)0x80070002);

        // "The specified password is not correct." (ERROR_INVALID_PASSWORD)
        private const int ERROR_INVALID_PASSWORD_HRESULT = unchecked((int)0x80070056);

        /// <summary>
        /// Looks for X509Certificates using the CertificateSourceOptions.
        /// Throws an InvalidOperationException if the option specifies a CertificateFilePath with invalid password.
        /// </summary>
        /// <param name="options">CertificateSourceOptions to be used while searching for the certificates.</param>
        /// <returns>An X509Certificate2Collection object containing matching certificates.
        /// If no matching certificates are found then it returns an empty collection.</returns>
        public static X509Certificate2Collection GetCertificates(CertificateSourceOptions options)
        {
            // check certificate path
            if (!string.IsNullOrEmpty(options.CertificatePath))
            {
                try
                {
                    var cert = new X509Certificate2(options.CertificatePath, options.CertificatePassword);
                    return new X509Certificate2Collection(cert);
                }
                catch (CryptographicException ex)
                {
                    // TODO-AM add casing for invalid password and certificate
                    if (ex.HResult == ERROR_INVALID_PASSWORD_HRESULT)
                    {
                        throw new InvalidOperationException("invalid password or certificate");
                    }
                }
            }
            else
            {
                var store = new X509Store(options.StoreName, options.StoreLocation);

                OpenStore(store);

                // check fingerprint
                if (!string.IsNullOrEmpty(options.Fingerprint))
                {
                    return store.Certificates.Find(X509FindType.FindByThumbprint, options.Fingerprint, validOnly: true);
                }

                // check subject name
                if (!string.IsNullOrEmpty(options.SubjectName))
                {
                    return store.Certificates.Find(X509FindType.FindBySubjectName, options.SubjectName, validOnly: false);
                }
            }

            // return empty certificate collection
            return new X509Certificate2Collection();
        }

        /// <summary>
        /// Opens an X509Store with read only access.
        /// Throws an InvalidOperationException if the store does not exist.
        /// </summary>
        /// <param name="store">X509Store to be opened.</param>
        private static void OpenStore(X509Store store)
        {
            try
            {
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            }
            catch (CryptographicException ex)
            {
                if (ex.HResult == ERROR_FILE_NOT_FOUND_HRESULT)
                {
                    throw new InvalidOperationException("certificate store not found");
                }
            }
        }
    }
}
