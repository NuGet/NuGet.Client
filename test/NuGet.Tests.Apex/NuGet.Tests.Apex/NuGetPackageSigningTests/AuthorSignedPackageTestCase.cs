// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
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
    public class AuthorSignedPackageTestCase : SharedVisualStudioHostTestClass, IClassFixture<SignedPackagesTestsApexFixture>
    {
        private SignedPackagesTestsApexFixture _fixture;

        public AuthorSignedPackageTestCase(SignedPackagesTestsApexFixture apexSigningFixture, ITestOutputHelper output)
            : base(apexSigningFixture, output)
        {
            _fixture = apexSigningFixture;
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public async Task AuthorSignedPackage_InstallFromPMCForPR_SucceedAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var signedPackage = _fixture.AuthorSignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);

                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                Utils.AssertPackageReferenceExists(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public async Task AuthorSignedPackage_InstallFromPMCForPC_SucceedAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var signedPackage = _fixture.AuthorSignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);

                Utils.AssetPackageInPackagesConfig(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public async Task AuthorSignedPackage_UninstallFromPMCForPR_SucceedAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var signedPackage = _fixture.AuthorSignedTestPackage;

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

                Utils.AssertPackageReferenceDoesNotExist(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public async Task AuthorSignedPackage_UninstallFromPMCForPC_SucceedAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var signedPackage = _fixture.AuthorSignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);
                nugetConsole.UninstallPackageFromPMC(signedPackage.Id);

                Utils.AssetPackageNotInPackagesConfig(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public async Task AuthorSignedPackage_UpdateUnsignedToSignedVersionFromPMCForPR_SucceedAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var packageVersion09 = "0.9.0";
            var signedPackage = _fixture.AuthorSignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                await Utils.CreatePackageInSourceAsync(testContext.PackageSource, signedPackage.Id, packageVersion09);
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, packageVersion09);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                nugetConsole.UpdatePackageFromPMC(signedPackage.Id, signedPackage.Version);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                Utils.AssertPackageReferenceExists(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public async Task AuthorSignedPackage_UpdateUnsignedToSignedVersionFromPMCForPC_SucceedAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var packageVersion09 = "0.9.0";
            var signedPackage = _fixture.AuthorSignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                await Utils.CreatePackageInSourceAsync(testContext.PackageSource, signedPackage.Id, packageVersion09);
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, packageVersion09);
                nugetConsole.UpdatePackageFromPMC(signedPackage.Id, signedPackage.Version);

                Utils.AssetPackageInPackagesConfig(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public async Task AuthorSignedPackage_DowngradeSignedToUnsignedVersionFromPMCForPR_SucceedAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            // This test is not considered an ideal behavior of the product but states the current behavior.
            // A package that is already installed as signed should be specailly treated and a user should not be
            // able to downgrade to an unsigned version. This test needs to be updated once this behavior gets
            // corrected in the product.

            var packageVersion09 = "0.9.0";
            var signedPackage = _fixture.AuthorSignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                await Utils.CreatePackageInSourceAsync(testContext.PackageSource, signedPackage.Id, packageVersion09);
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                nugetConsole.UpdatePackageFromPMC(signedPackage.Id, packageVersion09);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                Utils.AssertPackageReferenceExists(VisualStudio, testContext.Project, signedPackage.Id, packageVersion09, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public async Task AuthorSignedPackage_DowngradeSignedToUnsignedVersionFromPMCForPC_SucceedAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            // This test is not considered an ideal behavior of the product but states the current behavior.
            // A package that is already installed as signed should be specailly treated and a user should not be
            // able to downgrade to an unsigned version. This test needs to be updated once this behavior gets
            // corrected in the product.

            var packageVersion09 = "0.9.0";
            var signedPackage = _fixture.AuthorSignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                await Utils.CreatePackageInSourceAsync(testContext.PackageSource, signedPackage.Id, packageVersion09);
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);
                nugetConsole.UpdatePackageFromPMC(signedPackage.Id, packageVersion09);

                Utils.AssetPackageInPackagesConfig(VisualStudio, testContext.Project, signedPackage.Id, packageVersion09, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public async Task AuthorSignedPackage_WithExpiredCertificate_InstallFromPMCForPR_SucceedAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            await _fixture.CreateSignedPackageWithExpiredCertificateAsync();
            var signedPackagePath = _fixture.ExpiredCertSignedTestPackagePath;
            var signedPackage = _fixture.ExpiredCertSignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                File.Copy(signedPackagePath, Path.Combine(testContext.PackageSource, signedPackage.PackageName));

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                // TODO: Fix bug where no warnings are shwon when package is untrusted but still installed
                //nugetConsole.IsMessageFoundInPMC("expired certificate").Should().BeTrue("expired certificate warning");
                Utils.AssertPackageReferenceExists(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public async Task AuthorSignedPackage_WithExpiredCertificate_InstallFromPMCForPC_SucceedAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            await _fixture.CreateSignedPackageWithExpiredCertificateAsync();
            var signedPackagePath = _fixture.ExpiredCertSignedTestPackagePath;
            var signedPackage = _fixture.ExpiredCertSignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                File.Copy(signedPackagePath, Path.Combine(testContext.PackageSource, signedPackage.PackageName));

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);

                // TODO: Fix bug where no warnings are shwon when package is untrusted but still installed
                //nugetConsole.IsMessageFoundInPMC("expired certificate").Should().BeTrue("expired certificate warning");
                Utils.AssetPackageInPackagesConfig(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public async Task AuthorSignedPackage_Tampered_InstallFromPMCForPR_FailAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var signedPackage = _fixture.AuthorSignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);
                SignedArchiveTestUtility.TamperWithPackage(Path.Combine(testContext.PackageSource, signedPackage.PackageName));

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);
                nugetConsole.IsMessageFoundInPMC("package integrity check failed").Should().BeTrue("Integrity failed message shown.");
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                Utils.AssertPackageReferenceDoesNotExist(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public async Task AuthorSignedPackage_Tampered_InstallFromPMCForPC_FailAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var signedPackage = _fixture.AuthorSignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, signedPackage);
                SignedArchiveTestUtility.TamperWithPackage(Path.Combine(testContext.PackageSource, signedPackage.PackageName));

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);
                nugetConsole.IsMessageFoundInPMC("package integrity check failed").Should().BeTrue("Integrity failed message shown.");

                Utils.AssetPackageNotInPackagesConfig(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        public static IEnumerable<object[]> GetPackagesConfigTemplates()
        {
            for (var i = 0; i < Utils.GetIterations(); i++)
            {
                yield return new object[] { ProjectTemplate.ClassLibrary };
            }
        }

        public static IEnumerable<object[]> GetPackageReferenceTemplates()
        {
            for (var i = 0; i < Utils.GetIterations(); i++)
            {
                yield return new object[] { ProjectTemplate.NetStandardClassLib };
            }
        }
    }
}