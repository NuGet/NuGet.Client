// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
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

        // "The specified certificate file is not correct." (CRYPT_E_NO_MATCH)
        private const int CRYPT_E_NO_MATCH_HRESULT = unchecked((int)0x80092009);

        // Used to throw "Certificate file not found"
        private const string CERTIFICATE = "Certificate";

        // Used to throw "Certificate store file not found"
        private const string CERTIFICATE_STORE = "Certificate store";

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
            var resultCollection = new X509Certificate2Collection();
            if (!string.IsNullOrEmpty(options.CertificatePath))
            {
                try
                {
                    X509Certificate2 cert;

                    if (!string.IsNullOrEmpty(options.CertificatePassword))
                    {
                        cert = new X509Certificate2(options.CertificatePath, options.CertificatePassword); // use the password if the user provided it.
                    }
                    else
                    {
                        cert = new X509Certificate2(options.CertificatePath);                        
                    }

                    resultCollection = new X509Certificate2Collection(cert);
                }
                catch (CryptographicException ex)
                {

                    switch (ex.HResult)
                    {
                        case ERROR_INVALID_PASSWORD_HRESULT:
                            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                                Strings.SignCommandInvalidPasswordException,
                                options.CertificatePath,
                                nameof(options.CertificatePassword)));

                        case ERROR_FILE_NOT_FOUND_HRESULT:
                            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                                    Strings.SignCommandFileNotFound,
                                    CERTIFICATE,
                                    options.CertificatePath));

                        case CRYPT_E_NO_MATCH_HRESULT:
                            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                                    Strings.SignCommandInvalidCertException,
                                    options.CertificatePath));

                        default:
                            throw ex;
                    }
                }
            }
            else
            {
                var store = new X509Store(options.StoreName, options.StoreLocation);

                OpenStore(store);

                if (!string.IsNullOrEmpty(options.Fingerprint))
                {
                    resultCollection = store.Certificates.Find(X509FindType.FindByThumbprint, options.Fingerprint, validOnly: false);
                }

                if (!string.IsNullOrEmpty(options.SubjectName))
                {
                    resultCollection =  store.Certificates.Find(X509FindType.FindBySubjectName, options.SubjectName, validOnly: false);
                }

#if IS_DESKTOP
                store.Close();
#endif
            }

            return resultCollection;
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
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                            Strings.SignCommandFileNotFound,
                            CERTIFICATE_STORE,
                            store));
                }
            }
        }
    }
}
