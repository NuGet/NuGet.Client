// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;

namespace Test.Utility.Signing
{
    public sealed class X509StoreCertificate : IX509StoreCertificate, IDisposable
    {
        private bool _isDisposed;

        public StoreLocation StoreLocation { get; }
        public StoreName StoreName { get; }
        public X509Certificate2 Certificate { get; }

        public X509StoreCertificate(StoreLocation storeLocation, StoreName storeName, X509Certificate2 certificate)
        {
            if (certificate is null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            StoreLocation = storeLocation;
            StoreName = storeName;
            Certificate = certificate;

            X509StoreUtilities.AddCertificateToStore(storeLocation, storeName, certificate);
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                X509StoreUtilities.RemoveCertificateFromStore(StoreLocation, StoreName, Certificate);

                Certificate.Dispose();

                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }
    }
}
