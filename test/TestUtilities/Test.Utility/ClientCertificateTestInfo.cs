// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NuGet.Configuration;
using NuGet.Test.Utility;

namespace Test.Utility
{
    public sealed class ClientCertificateTestInfo : IDisposable
    {
        public ClientCertificateTestInfo()
        {
            WorkingPath = TestDirectory.Create();
            ConfigFile = Path.Combine(WorkingPath, "NuGet.config");
            PackageSourceName = "Foo";
            CertificateFileName = "contoso.pfx";
            CertificateAbsoluteFilePath = Path.Combine(WorkingPath, CertificateFileName);
            CertificateRelativeFilePath = ".\\" + CertificateFileName;
            CertificatePassword = "password";

            CertificateStoreLocation = StoreLocation.CurrentUser;
            CertificateStoreName = StoreName.My;
            CertificateFindBy = X509FindType.FindByIssuerName;
            CertificateFindValue = "Contoso";
            Certificate = GetCertificate();
            WriteConfigFile();
        }

        public void WriteConfigFile()
        {
            File.WriteAllText(ConfigFile, $@"<configuration>
    <packageSources>
        <add key=""{PackageSourceName}"" value=""https://contoso.com/v3/index.json"" />
    </packageSources>
</configuration>");
        }

        public X509Certificate2 Certificate { get; }

        public string CertificateAbsoluteFilePath { get; }
        public string CertificateFileName { get; }
        public X509FindType CertificateFindBy { get; }
        public object CertificateFindValue { get; }
        public string CertificatePassword { get; }
        public string CertificateRelativeFilePath { get; }
        public StoreLocation CertificateStoreLocation { get; }
        public StoreName CertificateStoreName { get; }
        public string ConfigFile { get; }

        public string PackageSourceName { get; }
        public TestDirectory WorkingPath { get; }

        public void Dispose()
        {
            WorkingPath.Dispose();
            RemoveCertificateFromStorage();
        }

        public ISettings LoadSettingsFromConfigFile()
        {
            var directory = Path.GetDirectoryName(ConfigFile);
            var filename = Path.GetFileName(ConfigFile);
            return Settings.LoadSpecificSettings(directory, filename);
        }

        public void SetupCertificateFile()
        {
            File.WriteAllBytes(CertificateAbsoluteFilePath, Certificate.RawData);
        }

        public void SetupCertificateInStorage()
        {
            using (var store = new X509Store(CertificateStoreName, CertificateStoreLocation))
            {
                store.Open(OpenFlags.ReadWrite);
                store.Add(Certificate);
            }
        }

        private byte[] CreateCertificate()
        {
            var rsa = RSA.Create(2048);
            var request = new CertificateRequest("cn=" + CertificateFindValue, rsa, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
            var start = DateTime.UtcNow.AddDays(-1);
            var end = start.AddYears(1);

            var cert = request.CreateSelfSigned(start, end);
            return cert.Export(X509ContentType.Pfx, CertificatePassword);
        }

        private X509Certificate2 GetCertificate()
        {
            return new X509Certificate2(CreateCertificate(), CertificatePassword);
        }

        private void RemoveCertificateFromStorage()
        {
            using (var store = new X509Store(CertificateStoreName, CertificateStoreLocation))
            {
                store.Open(OpenFlags.ReadWrite);
                var resultCertificates = store.Certificates.Find(CertificateFindBy, CertificateFindValue, false);
                foreach (var certificate in resultCertificates)
                {
                    store.Remove(certificate);
                }
            }
        }

        public void WriteConfig()
        {

        }
    }
}
