// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;
using NuGet.Configuration;

namespace NuGet.Configuration.Test
{
    public class ClientCertificateProviderTests
    {
        [PlatformFact(Platform.Windows)]
        public void CertificateFromFile_Success_ParsedAndAddedToAssociatedPackageSource()
        {
            using (var testInfo = new ClientCertificateTestInfo())
            {
                // Arrange
                testInfo.SetupCertificateFile();

                // Act
                var settings = testInfo.LoadSettingsFromConfigFile();
                var clientCertificateProvider = new ClientCertificateProvider(settings);
                clientCertificateProvider.AddOrUpdate(new FileClientCertItem(testInfo.PackageSourceName,
                                                                             testInfo.CertificateAbsoluteFilePath,
                                                                             testInfo.CertificatePassword,
                                                                             false,
                                                                             testInfo.ConfigFile));

                // Assert
                var packageSourceProvider = new PackageSourceProvider(settings);
                var packageSourceList = packageSourceProvider.LoadPackageSources().ToList();
                Assert.Equal(1, packageSourceList.Count);
                Assert.Equal(1, packageSourceList[0].ClientCertificates.Count);
                Assert.Equal(testInfo.Certificate, packageSourceList[0].ClientCertificates[0]);
            }
        }

        [PlatformFact(Platform.Windows, Platform.Linux)]
        public void CertificateFromStore_Success_ParsedAndAddedToAssociatedPackageSource()
        {
            using (var testInfo = new ClientCertificateTestInfo())
            {
                // Arrange
                testInfo.SetupCertificateInStorage();

                // Act
                var settings = testInfo.LoadSettingsFromConfigFile();
                var clientCertificateProvider = new ClientCertificateProvider(settings);
                clientCertificateProvider.AddOrUpdate(new StoreClientCertItem(testInfo.PackageSourceName,
                                                                              testInfo.CertificateFindValue.ToString(),
                                                                              testInfo.CertificateStoreLocation,
                                                                              testInfo.CertificateStoreName,
                                                                              testInfo.CertificateFindBy));

                // Assert
                var packageSourceProvider = new PackageSourceProvider(settings);
                var packageSourceList = packageSourceProvider.LoadPackageSources().ToList();
                Assert.Equal(1, packageSourceList.Count);
                Assert.Equal(1, packageSourceList[0].ClientCertificates.Count);
                Assert.Equal(testInfo.Certificate, packageSourceList[0].ClientCertificates[0]);
            }
        }
    }
}
