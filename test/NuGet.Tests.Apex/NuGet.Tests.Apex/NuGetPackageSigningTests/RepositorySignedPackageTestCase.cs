// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Test.Apex.VisualStudio.Solution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet.Test.Utility;
using Test.Utility.Signing;

namespace NuGet.Tests.Apex
{
    [TestClass]
    public class RepositorySignedPackageTestCase : SharedVisualStudioHostTestClass
    {
        private const int Timeout = 5 * 60 * 1000; // 5 minutes

        private static SignedPackagesTestsApexFixture Fixture;

        public RepositorySignedPackageTestCase()
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
            var signedPackage = Fixture.RepositorySignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate))
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);

                CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version);
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        public async Task UninstallFromPMCForPC_SucceedAsync()
        {
            // Arrange
            EnsureVisualStudioHost();

            ProjectTemplate projectTemplate = ProjectTemplate.ClassLibrary;
            var signedPackage = Fixture.RepositorySignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate))
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);
                nugetConsole.UninstallPackageFromPMC(signedPackage.Id);

                CommonUtility.AssertPackageNotInPackagesConfig(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version);
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
            var signedPackage = Fixture.RepositorySignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate))
            {
                await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, signedPackage.Id, packageVersion09);
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, packageVersion09);
                nugetConsole.UpdatePackageFromPMC(signedPackage.Id, signedPackage.Version);

                CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version);
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        public async Task WithExpiredCertificate_InstallFromPMCForPC_WarnAsync()
        {
            // Arrange
            EnsureVisualStudioHost();

            ProjectTemplate projectTemplate = ProjectTemplate.ClassLibrary;
            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate))
            using (var trustedExpiringTestCert = SigningUtility.GenerateTrustedTestCertificateThatWillExpireSoon())
            {
                Trace.WriteLine("Creating package");
                var package = CommonUtility.CreatePackage("ExpiredTestPackage", "1.0.0");

                Trace.WriteLine("Signing package");
                var expiredTestPackage = CommonUtility.RepositorySignPackage(package, trustedExpiringTestCert.Source.Cert, new Uri("https://v3serviceIndexUrl.test/api/index.json"));
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, expiredTestPackage);

                Trace.WriteLine("Waiting for package to expire");
                SigningUtility.WaitForCertificateToExpire(trustedExpiringTestCert.Source.Cert);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(expiredTestPackage.Id, expiredTestPackage.Version);

                // TODO: Fix bug where no warnings are shown when package is untrusted but still installed
                //nugetConsole.IsMessageFoundInPMC("expired certificate").Should().BeTrue("expired certificate warning");
                CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, expiredTestPackage.Id, expiredTestPackage.Version);
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        public async Task Tampered_InstallFromPMCForPC_FailAsync()
        {
            // Arrange
            EnsureVisualStudioHost();

            ProjectTemplate projectTemplate = ProjectTemplate.ClassLibrary;
            var signedPackage = Fixture.RepositorySignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate))
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);
                SignedArchiveTestUtility.TamperWithPackage(Path.Combine(testContext.PackageSource, signedPackage.PackageName));

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);
                nugetConsole.IsMessageFoundInPMC("package integrity check failed").Should().BeTrue("Integrity failed message shown.");

                CommonUtility.AssertPackageNotInPackagesConfig(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version);
            }
        }
    }
}
