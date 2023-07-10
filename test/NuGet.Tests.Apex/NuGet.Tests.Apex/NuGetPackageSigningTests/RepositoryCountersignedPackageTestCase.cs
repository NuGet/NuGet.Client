// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Test.Apex.VisualStudio.Solution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet.Test.Utility;
using Test.Utility.Signing;

namespace NuGet.Tests.Apex
{
    [TestClass]
    public class RepositoryCountersignedPackageTestCase : SharedVisualStudioHostTestClass
    {
        private const int Timeout = 5 * 60 * 1000; // 5 minutes

        private static SignedPackagesTestsApexFixture Fixture;

        public RepositoryCountersignedPackageTestCase()
            : base()
        {
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            Fixture = new SignedPackagesTestsApexFixture();
        }

        [TestMethod]
        [Timeout(Timeout)]
        public async Task InstallFromPMCForPC_SucceedAsync()
        {
            // Arrange
            EnsureVisualStudioHost();

            ProjectTemplate projectTemplate = ProjectTemplate.ClassLibrary;
            var signedPackage = Fixture.RepositoryCountersignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, Logger))
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);

                CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, Logger);
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        public async Task UninstallFromPMCForPC_SucceedAsync()
        {
            // Arrange
            EnsureVisualStudioHost();

            ProjectTemplate projectTemplate = ProjectTemplate.ClassLibrary;
            var signedPackage = Fixture.RepositoryCountersignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, Logger))
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);
                nugetConsole.UninstallPackageFromPMC(signedPackage.Id);

                CommonUtility.AssertPackageNotInPackagesConfig(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, Logger);
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        public async Task UpdateUnsignedToSignedVersionFromPMCForPC_SucceedAsync()
        {
            // Arrange
            EnsureVisualStudioHost();

            ProjectTemplate projectTemplate = ProjectTemplate.ClassLibrary;
            var packageVersion09 = "0.9.0";
            var signedPackage = Fixture.RepositoryCountersignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, Logger))
            {
                await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, signedPackage.Id, packageVersion09);
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, packageVersion09);
                nugetConsole.UpdatePackageFromPMC(signedPackage.Id, signedPackage.Version);

                CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, Logger);
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        public async Task WithExpiredAuthorCertificateAtCountersigning_InstallFromPMCForPC_WarnAsync()
        {
            // Arrange
            EnsureVisualStudioHost();

            ProjectTemplate projectTemplate = ProjectTemplate.ClassLibrary;
            var timestampService = await Fixture.GetDefaultTrustedTimestampServiceAsync();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, Logger))
            using (var trustedCert = Fixture.TrustedRepositoryTestCertificate)
            using (var trustedExpiringTestCert = SigningUtility.GenerateTrustedTestCertificateThatWillExpireSoon())
            {
                Logger.WriteMessage("Creating package");
                var package = CommonUtility.CreatePackage("ExpiredTestPackage", "1.0.0");

                Logger.WriteMessage("Signing package");
                var expiredTestPackage = CommonUtility.AuthorSignPackage(package, trustedExpiringTestCert.Source.Cert);
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, expiredTestPackage);
                var packageFullName = Path.Combine(testContext.PackageSource, expiredTestPackage.PackageName);

                Logger.WriteMessage("Waiting for package to expire");
                SigningUtility.WaitForCertificateToExpire(trustedExpiringTestCert.Source.Cert);

                Logger.WriteMessage("Countersigning package");
                var countersignedPackage = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    new X509Certificate2(trustedCert.Source.Cert),
                    packageFullName,
                    testContext.PackageSource,
                    new Uri("https://v3serviceIndexUrl.test/api/index.json"),
                    timestampService.Url);
                File.Copy(countersignedPackage, packageFullName, overwrite: true);
                File.Delete(countersignedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(expiredTestPackage.Id, expiredTestPackage.Version);

                // TODO: Fix bug where no warnings are shown when package is untrusted but still installed
                //nugetConsole.IsMessageFoundInPMC("expired certificate").Should().BeTrue("expired certificate warning");
                CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, expiredTestPackage.Id, expiredTestPackage.Version, Logger);
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        public async Task Tampered_InstallFromPMCForPC_FailAsync()
        {
            // Arrange
            EnsureVisualStudioHost();

            ProjectTemplate projectTemplate = ProjectTemplate.ClassLibrary;
            var signedPackage = Fixture.RepositoryCountersignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, Logger))
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);
                SignedArchiveTestUtility.TamperWithPackage(Path.Combine(testContext.PackageSource, signedPackage.PackageName));

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);
                nugetConsole.IsMessageFoundInPMC("package integrity check failed").Should().BeTrue("Integrity failed message shown.");

                CommonUtility.AssertPackageNotInPackagesConfig(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, Logger);
            }
        }
    }
}
