// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using NuGet.Common;
using Test.Utility.Signing;

namespace Dotnet.Integration.Test
{
    public sealed class X509StoreCertificate : IX509StoreCertificate, IDisposable
    {
        private bool _isDisposed;

        private readonly FileInfo _certificateBundle;
        private readonly bool _useCertificateBundle;

        public StoreLocation StoreLocation { get; }
        public StoreName StoreName { get; }
        public X509Certificate2 Certificate { get; }

        public X509StoreCertificate(StoreLocation storeLocation, StoreName storeName, X509Certificate2 certificate, FileInfo certificateBundle = null)
        {
            if (certificate is null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            StoreLocation = storeLocation;
            StoreName = storeName;
            Certificate = certificate;
            _certificateBundle = certificateBundle;

            _useCertificateBundle = storeName == StoreName.Root && !RuntimeEnvironmentHelper.IsWindows;

            if (_useCertificateBundle)
            {
                CertificateBundleUtilities.AddCertificateToBundle(certificateBundle, certificate);
            }

            // Some tests perform in-process signing and verification.
            // Other tests first create a signed package in-process and then verify out of process using the dotnet CLI.
            // So, the in-process test fallback certificate store must always be updated.
            X509StoreUtilities.AddCertificateToStore(storeLocation, storeName, certificate);
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                if (_useCertificateBundle)
                {
                    CertificateBundleUtilities.RemoveCertificateFromBundle(_certificateBundle, Certificate);
                }

                X509StoreUtilities.RemoveCertificateFromStore(StoreLocation, StoreName, Certificate);

                Certificate.Dispose();

                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }
    }
}
