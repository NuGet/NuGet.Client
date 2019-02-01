// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using NuGet.Common;
using NuGet.Test.Utility;

namespace Test.Utility.Signing
{
    public static class TrustedTestCert
    {
        public static TrustedTestCert<X509Certificate2> Create(
            X509Certificate2 cert,
            StoreName storeName,
            StoreLocation storeLocation,
            TestDirectory dir,
            TimeSpan? maximumValidityPeriod = null)
        {
            return new TrustedTestCert<X509Certificate2>(
                cert,
                x => x,
                storeName,
                storeLocation,
                dir,
                maximumValidityPeriod);
        }
    }

    /// <summary>
    /// Give a certificate full trust for the life of the object.
    /// </summary>
    public class TrustedTestCert<T> : IDisposable
    {
        private X509Store _store;

        public X509Certificate2 TrustedCert { get; }

        public T Source { get; }

        public StoreName StoreName { get; }

        public StoreLocation StoreLocation { get; }

        private bool _isDisposed;

        private string _systemTrustedCertPath;

        public TrustedTestCert(T source,
            Func<T, X509Certificate2> getCert,
            StoreName storeName,
            StoreLocation storeLocation,
            TestDirectory dir,
            TimeSpan? maximumValidityPeriod = null)
        {
            Source = source;
            TrustedCert = getCert(source);

            if (!maximumValidityPeriod.HasValue)
            {
                maximumValidityPeriod = TimeSpan.FromHours(2);
            }

            if (TrustedCert.NotAfter - TrustedCert.NotBefore > maximumValidityPeriod.Value)
            {
                throw new InvalidOperationException($"The certificate used is valid for more than {maximumValidityPeriod}.");
            }

            StoreName = GetPlatformSpecificStoreName(storeName, dir);
            StoreLocation = GetPlatformSpecificStoreLocation(storeLocation);
            AddCertificateToStore();

            ExportCrl();
        }

        private StoreLocation GetPlatformSpecificStoreLocation(StoreLocation requestedLocation)
        {
            if (requestedLocation == StoreLocation.LocalMachine &&
                (RuntimeEnvironmentHelper.IsMacOSX || RuntimeEnvironmentHelper.IsLinux))
            {
                return StoreLocation.CurrentUser;
            }

            return requestedLocation;
        }

        private StoreName GetPlatformSpecificStoreName(StoreName requestedStoreName, TestDirectory dir)
        {
            if (RuntimeEnvironmentHelper.IsMacOSX)
            {
                if (requestedStoreName == StoreName.Root || requestedStoreName == StoreName.CertificateAuthority)
                {
                    TrustCertInMac(dir);
                }

                return StoreName.My;
            }

            if (requestedStoreName == StoreName.CertificateAuthority && RuntimeEnvironmentHelper.IsLinux)
            {
                return StoreName.Root;
            }

            return requestedStoreName;
        }

        private void AddCertificateToStore()
        {
            _store = new X509Store(StoreName, StoreLocation);
            _store.Open(OpenFlags.ReadWrite);
            _store.Add(TrustedCert);
        }

        private void ExportCrl()
        {
            var testCertificate = Source as TestCertificate;

            if (testCertificate != null && testCertificate.Crl != null)
            {
                testCertificate.Crl.ExportCrl();
            }
        }

        private void TrustCertInMac(TestDirectory dir)
        {
            var exportedCert = TrustedCert.Export(X509ContentType.Cert);

            var tempCertFileName = GetCertificateName() + ".cer";

            _systemTrustedCertPath = Path.Combine(dir, tempCertFileName);
            File.WriteAllBytes(_systemTrustedCertPath, exportedCert);

            var trustProcess = Process.Start(@"/usr/bin/sudo", $@"security -v add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain {_systemTrustedCertPath}");
            trustProcess.WaitForExit();
        }

        private string GetCertificateName()
        {
            var parts = TrustedCert.SubjectName.Name.Split('=');

            for (var i = 0; i < parts.Length; i++)
            {
                if (string.Equals(parts[i], "CN") && i + 1 < parts.Length)
                {
                    return parts[i + 1];
                }
            }

            return "NuGetTest-" + Guid.NewGuid().ToString();
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
                if (_systemTrustedCertPath != null && RuntimeEnvironmentHelper.IsMacOSX)
                {
                    var untrustProcess = Process.Start(@"/usr/bin/sudo", $@"security -v remove-trusted-cert -d {_systemTrustedCertPath}");
                    untrustProcess.WaitForExit();
                }

                using (_store)
                {
                    _store.Remove(TrustedCert);
                }

                DisposeCrl();

                _isDisposed = true;
            }
        }
    }
}