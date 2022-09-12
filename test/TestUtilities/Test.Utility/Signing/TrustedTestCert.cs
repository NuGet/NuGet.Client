// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using NuGet.Packaging.Signing;

namespace Test.Utility.Signing
{
    public static class TrustedTestCert
    {
        public static TrustedTestCert<X509Certificate2> Create(
            X509Certificate2 cert,
            X509StorePurpose storePurpose,
            StoreName storeName = StoreName.TrustedPeople,
            StoreLocation storeLocation = StoreLocation.CurrentUser,
            TimeSpan? maximumValidityPeriod = null)
        {
            return new TrustedTestCert<X509Certificate2>(
                cert,
                x => x,
                storePurpose,
                storeName,
                storeLocation,
                maximumValidityPeriod);
        }
    }

    /// <summary>
    /// Give a certificate full trust for the life of the object.
    /// </summary>
    public class TrustedTestCert<T> : IDisposable
    {
        public X509Certificate2 TrustedCert { get; }

        public T Source { get; }

        public StoreName StoreName { get; }

        public StoreLocation StoreLocation { get; }

        private readonly X509StorePurpose _storePurpose;
        private bool _isDisposed;

        [Obsolete("Use the constructor that takes an X.509 store purpose.")]
        public TrustedTestCert(
            T source,
            Func<T, X509Certificate2> getCert,
            StoreName storeName = StoreName.TrustedPeople,
            StoreLocation storeLocation = StoreLocation.CurrentUser,
            TimeSpan? maximumValidityPeriod = null)
            : this(source, getCert, X509StorePurpose.CodeSigning, storeName, storeLocation, maximumValidityPeriod)
        {
        }

        public TrustedTestCert(
            T source,
            Func<T, X509Certificate2> getCert,
            X509StorePurpose storePurpose,
            StoreName storeName = StoreName.TrustedPeople,
            StoreLocation storeLocation = StoreLocation.CurrentUser,
            TimeSpan? maximumValidityPeriod = null)
        {
            Source = source;
            TrustedCert = getCert(source);
            _storePurpose = storePurpose;

            if (!maximumValidityPeriod.HasValue)
            {
                maximumValidityPeriod = TimeSpan.FromHours(2);
            }

#if IS_SIGNING_SUPPORTED
            if (TrustedCert.NotAfter - TrustedCert.NotBefore > maximumValidityPeriod.Value)
            {
                throw new InvalidOperationException($"The certificate used is valid for more than {maximumValidityPeriod}.");
            }
#endif
            StoreName = storeName;
            StoreLocation = storeLocation;

            X509StoreUtilities.AddCertificateToStore(StoreLocation, StoreName, TrustedCert, storePurpose);

            ExportCrl();
        }

        private void ExportCrl()
        {
            var testCertificate = Source as TestCertificate;

            if (testCertificate != null && testCertificate.Crl != null)
            {
                testCertificate.Crl.ExportCrl();
            }
        }

        private void DisposeCrl()
        {
            var testCertificate = Source as TestCertificate;

            if (testCertificate != null && testCertificate.Crl != null)
            {
                testCertificate.Crl.Dispose();
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                X509StoreUtilities.RemoveCertificateFromStore(StoreLocation, StoreName, TrustedCert, _storePurpose);

                DisposeCrl();

                TrustedCert.Dispose();

                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }
    }
}
