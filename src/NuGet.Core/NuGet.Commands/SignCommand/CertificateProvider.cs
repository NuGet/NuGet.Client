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

        // "The specified network password is not correct." (ERROR_INVALID_PASSWORD)
        private const int ERROR_INVALID_PASSWORD_HRESULT = unchecked((int)0x80070056);

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
                        throw new ArgumentException("invalid password or certificate");
                    }
                }
            }
            else
            {
                X509Store store;
                // check store name
                if (!string.IsNullOrEmpty(options.StoreName))
                {
                    store = GetStore(options.StoreName, options.StoreLocation);
                }
                else
                {
                    store = GetStore(options.StoreLocation);
                }

                OpenStore(store);

                // check fingerprint
                if (!string.IsNullOrEmpty(options.Fingerprint))
                {
                    return store.Certificates.Find(X509FindType.FindByThumbprint, options.Fingerprint, validOnly: true);
                }

                // check subject name
                if (!string.IsNullOrEmpty(options.SubjectName))
                {
                    return store.Certificates.Find(X509FindType.FindBySubjectName, options.SubjectName, validOnly: true);
                }
            }

            // return empty certificate collection
            return new X509Certificate2Collection();
        }

        private static X509Store GetStore(string storeName, string storeLocation)
        {
            var location = storeLocation.Equals("LocalMachine", StringComparison.InvariantCultureIgnoreCase) ?
                StoreLocation.LocalMachine :
                StoreLocation.CurrentUser;

            return new X509Store(storeName, location);
        }

        private static X509Store GetStore(string storeLocation)
        {
            var location = storeLocation.Equals("LocalMachine", StringComparison.InvariantCultureIgnoreCase) ?
                StoreLocation.LocalMachine :
                StoreLocation.CurrentUser;

            return new X509Store(location);
        }

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
                    throw new ArgumentException("certificate store not found");
                }
            }
        }
    }
}
