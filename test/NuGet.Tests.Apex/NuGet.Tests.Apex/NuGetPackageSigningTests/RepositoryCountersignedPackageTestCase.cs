// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGet.StaFact;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Tests.Apex
{
    public class RepositoryCountersignedPackageTestCase : SharedVisualStudioHostTestClass, IClassFixture<SignedPackagesTestsApexFixture>
    {
        private SignedPackagesTestsApexFixture _fixture;

        public RepositoryCountersignedPackageTestCase(SignedPackagesTestsApexFixture apexSigningFixture, ITestOutputHelper output)
            : base(apexSigningFixture, output)
        {
            _fixture = apexSigningFixture;
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public async Task InstallFromPMCForPR_SucceedAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var signedPackage = _fixture.RepositoryCountersignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);

                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                CommonUtility.AssertPackageReferenceExists(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public async Task InstallFromPMCForPC_SucceedAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var signedPackage = _fixture.RepositoryCountersignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);

                CommonUtility.AssetPackageInPackagesConfig(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public async Task UninstallFromPMCForPR_SucceedAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var signedPackage = _fixture.RepositoryCountersignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                nugetConsole.UninstallPackageFromPMC(signedPackage.Id);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                CommonUtility.AssertPackageReferenceDoesNotExist(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public async Task UninstallFromPMCForPC_SucceedAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var signedPackage = _fixture.RepositoryCountersignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);
                nugetConsole.UninstallPackageFromPMC(signedPackage.Id);

                CommonUtility.AssertPackageNotInPackagesConfig(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public async Task UpdateUnsignedToSignedVersionFromPMCForPR_SucceedAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var packageVersion09 = "0.9.0";
            var signedPackage = _fixture.RepositoryCountersignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, signedPackage.Id, packageVersion09);
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, packageVersion09);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                nugetConsole.UpdatePackageFromPMC(signedPackage.Id, signedPackage.Version);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                CommonUtility.AssertPackageReferenceExists(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public async Task UpdateUnsignedToSignedVersionFromPMCForPC_SucceedAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var packageVersion09 = "0.9.0";
            var signedPackage = _fixture.RepositoryCountersignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, signedPackage.Id, packageVersion09);
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, packageVersion09);
                nugetConsole.UpdatePackageFromPMC(signedPackage.Id, signedPackage.Version);

                CommonUtility.AssetPackageInPackagesConfig(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public async Task WithExpiredAuthorCertificateAtCountersigning_InstallFromPMCForPR_WarnAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            using (var trustedCert = _fixture.TrustedRepositoryTestCertificate)
            using (var trustedExpiringTestCert = SigningTestUtility.GenerateTrustedTestCertificateThatExpiresIn5Seconds())
            {
                XunitLogger.LogInformation("Creating package");
                var package = CommonUtility.CreatePackage("ExpiredTestPackage", "1.0.0");

                XunitLogger.LogInformation("Signing package");
                var expiredTestPackage = CommonUtility.AuthorSignPackage(package, trustedExpiringTestCert.Source.Cert);
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, expiredTestPackage);
                var packageFullName = Path.Combine(testContext.PackageSource, expiredTestPackage.PackageName);

                XunitLogger.LogInformation("Waiting for package to expire");
                SigningUtility.WaitForCertificateToExpire(trustedExpiringTestCert.Source.Cert);

                XunitLogger.LogInformation("Countersigning package");
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
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                // TODO: Fix bug where no warnings are shown when package is untrusted but still installed
                //nugetConsole.IsMessageFoundInPMC("expired certificate").Should().BeTrue("expired certificate warning");
                CommonUtility.AssertPackageReferenceExists(VisualStudio, testContext.Project, expiredTestPackage.Id, expiredTestPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public async Task WithExpiredAuthorCertificate_ExpiredRepoCertificate_InstallFromPMCForPR_WarnAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            using (var trustedExpiringTestCert = SigningTestUtility.GenerateTrustedTestCertificateThatExpiresIn5Seconds())
            {
                XunitLogger.LogInformation("Creating package");
                var package = CommonUtility.CreatePackage("ExpiredTestPackage", "1.0.0");

                XunitLogger.LogInformation("Signing and countersigning package");
                var expiredTestPackage = CommonUtility.AuthorSignPackage(package, trustedExpiringTestCert.Source.Cert);
                var countersignedPackage = CommonUtility.RepositoryCountersignPackage(
                    expiredTestPackage,
                    trustedExpiringTestCert.Source.Cert,
                    new Uri("https://v3serviceIndexUrl.test/api/index.json"));

                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, expiredTestPackage);

                XunitLogger.LogInformation("Waiting for package to expire");
                SigningUtility.WaitForCertificateToExpire(trustedExpiringTestCert.Source.Cert);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(expiredTestPackage.Id, expiredTestPackage.Version);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                // TODO: Fix bug where no warnings are shown when package is untrusted but still installed
                //nugetConsole.IsMessageFoundInPMC("expired certificate").Should().BeTrue("expired certificate warning");
                CommonUtility.AssertPackageReferenceExists(VisualStudio, testContext.Project, expiredTestPackage.Id, expiredTestPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public async Task WithExpiredAuthorCertificateAfterCountersigned_InstallFromPMCForPR_SucceedAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            using (var trustedCert = _fixture.TrustedRepositoryTestCertificate)
            using (var trustedExpiringTestCert = SigningTestUtility.GenerateTrustedTestCertificateThatExpiresIn5Seconds())
            {
                XunitLogger.LogInformation("Creating package");
                var package = CommonUtility.CreatePackage("ExpiredTestPackage", "1.0.0");

                XunitLogger.LogInformation("Signing and countersigning package");
                var expiredTestPackage = CommonUtility.AuthorSignPackage(package, trustedExpiringTestCert.Source.Cert);
                var countersignedPackage = CommonUtility.RepositoryCountersignPackage(
                    expiredTestPackage,
                    trustedCert.Source.Cert,
                    new Uri("https://v3serviceIndexUrl.test/api/index.json"),
                    packageOwners: null,
                    timestampProviderUrl: timestampService.Url);

                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, expiredTestPackage);

                XunitLogger.LogInformation("Waiting for package to expire");
                SigningUtility.WaitForCertificateToExpire(trustedExpiringTestCert.Source.Cert);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(expiredTestPackage.Id, expiredTestPackage.Version);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();
                
                CommonUtility.AssertPackageReferenceExists(VisualStudio, testContext.Project, expiredTestPackage.Id, expiredTestPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public async Task WithExpiredAuthorCertificateAtCountersigning_InstallFromPMCForPC_WarnAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            using (var trustedCert = _fixture.TrustedRepositoryTestCertificate)
            using (var trustedExpiringTestCert = SigningTestUtility.GenerateTrustedTestCertificateThatExpiresIn5Seconds())
            {
                XunitLogger.LogInformation("Creating package");
                var package = CommonUtility.CreatePackage("ExpiredTestPackage", "1.0.0");

                XunitLogger.LogInformation("Signing package");
                var expiredTestPackage = CommonUtility.AuthorSignPackage(package, trustedExpiringTestCert.Source.Cert);
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, expiredTestPackage);
                var packageFullName = Path.Combine(testContext.PackageSource, expiredTestPackage.PackageName);

                XunitLogger.LogInformation("Waiting for package to expire");
                SigningUtility.WaitForCertificateToExpire(trustedExpiringTestCert.Source.Cert);

                XunitLogger.LogInformation("Countersigning package");
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
                CommonUtility.AssetPackageInPackagesConfig(VisualStudio, testContext.Project, expiredTestPackage.Id, expiredTestPackage.Version, XunitLogger);
            }
        }


        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public async Task WithExpiredAuthorCertificate_ExpiredRepoCertificate_InstallFromPMCForPC_WarnAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            using (var trustedExpiringTestCert = SigningTestUtility.GenerateTrustedTestCertificateThatExpiresIn5Seconds())
            {
                XunitLogger.LogInformation("Creating package");
                var package = CommonUtility.CreatePackage("ExpiredTestPackage", "1.0.0");

                XunitLogger.LogInformation("Signing and countersigning package");
                var expiredTestPackage = CommonUtility.AuthorSignPackage(package, trustedExpiringTestCert.Source.Cert);
                var countersignedPackage = CommonUtility.RepositoryCountersignPackage(
                    expiredTestPackage,
                    trustedExpiringTestCert.Source.Cert,
                    new Uri("https://v3serviceIndexUrl.test/api/index.json"));

                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, expiredTestPackage);

                XunitLogger.LogInformation("Waiting for package to expire");
                SigningUtility.WaitForCertificateToExpire(trustedExpiringTestCert.Source.Cert);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(expiredTestPackage.Id, expiredTestPackage.Version);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                // TODO: Fix bug where no warnings are shown when package is untrusted but still installed
                //nugetConsole.IsMessageFoundInPMC("expired certificate").Should().BeTrue("expired certificate warning");
                CommonUtility.AssertPackageReferenceExists(VisualStudio, testContext.Project, expiredTestPackage.Id, expiredTestPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public async Task WithExpiredAuthorCertificateAfterCountersigning_InstallFromPMCForPC_SucceedAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            using (var trustedCert = _fixture.TrustedRepositoryTestCertificate)
            using (var trustedExpiringTestCert = SigningTestUtility.GenerateTrustedTestCertificateThatExpiresIn5Seconds())
            {
                XunitLogger.LogInformation("Creating package");
                var package = CommonUtility.CreatePackage("ExpiredTestPackage", "1.0.0");

                XunitLogger.LogInformation("Signing and countersigning package");
                var expiredTestPackage = CommonUtility.AuthorSignPackage(package, trustedExpiringTestCert.Source.Cert);
                var countersignedPackage = CommonUtility.RepositoryCountersignPackage(
                    expiredTestPackage,
                    trustedCert.Source.Cert,
                    new Uri("https://v3serviceIndexUrl.test/api/index.json"),
                    packageOwners: null,
                    timestampProviderUrl: timestampService.Url);

                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, expiredTestPackage);

                XunitLogger.LogInformation("Waiting for package to expire");
                SigningUtility.WaitForCertificateToExpire(trustedExpiringTestCert.Source.Cert);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(expiredTestPackage.Id, expiredTestPackage.Version);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                CommonUtility.AssertPackageReferenceExists(VisualStudio, testContext.Project, expiredTestPackage.Id, expiredTestPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public async Task Tampered_InstallFromPMCForPR_FailAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var signedPackage = _fixture.RepositoryCountersignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);
                SignedArchiveTestUtility.TamperWithPackage(Path.Combine(testContext.PackageSource, signedPackage.PackageName));

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);
                nugetConsole.IsMessageFoundInPMC("package integrity check failed").Should().BeTrue("Integrity failed message shown.");
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                CommonUtility.AssertPackageReferenceDoesNotExist(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public async Task Tampered_InstallFromPMCForPC_FailAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var signedPackage = _fixture.RepositoryCountersignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);
                SignedArchiveTestUtility.TamperWithPackage(Path.Combine(testContext.PackageSource, signedPackage.PackageName));

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);
                nugetConsole.IsMessageFoundInPMC("package integrity check failed").Should().BeTrue("Integrity failed message shown.");

                CommonUtility.AssertPackageNotInPackagesConfig(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        public static IEnumerable<object[]> GetPackagesConfigTemplates()
        {
            for (var i = 0; i < CommonUtility.GetIterations(); i++)
            {
                yield return new object[] { ProjectTemplate.ClassLibrary };
            }
        }

        public static IEnumerable<object[]> GetPackageReferenceTemplates()
        {
            for (var i = 0; i < CommonUtility.GetIterations(); i++)
            {
                yield return new object[] { ProjectTemplate.NetStandardClassLib };
            }
        }
    }
}
