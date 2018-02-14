// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using FluentAssertions;
using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGet.StaFact;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Tests.Apex
{
    public class NuGetPackageSigningTestCase : SharedVisualStudioHostTestClass, IClassFixture<SignedPackagesTestsApexFixture>
    {
        private SignedPackagesTestsApexFixture _fixture;

        public NuGetPackageSigningTestCase(SignedPackagesTestsApexFixture apexSigningFixture, ITestOutputHelper output)
            : base(apexSigningFixture, output)
        {
            _fixture = apexSigningFixture;
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public void InstallSignedPackageFromPMCForPR(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var signedPackage = _fixture.SignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                SimpleTestPackageUtility.CreatePackages(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);

                // Build before the install check to ensure that everything is up to date.
                testContext.Project.Build();

                Utils.AssertPackageReferenceExists(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public void InstallSignedPackageFromPMCForPC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var signedPackage = _fixture.SignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                SimpleTestPackageUtility.CreatePackages(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);

                Utils.AssetPackageInPackagesConfig(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public void UninstallSignedPackageFromPMCForPR(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var signedPackage = _fixture.SignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                SimpleTestPackageUtility.CreatePackages(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                nugetConsole.UninstallPackageFromPMC(signedPackage.Id);
                testContext.Project.Build();

                Utils.AssertPackageReferenceDoesNotExist(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public void UninstallSignedPackageFromPMCForPC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var signedPackage = _fixture.SignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                SimpleTestPackageUtility.CreatePackages(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);
                nugetConsole.UninstallPackageFromPMC(signedPackage.Id);

                Utils.AssetPackageNotInPackagesConfig(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public void UpdateUnsignedPackageToSignedVersionFromPMCForPR(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var packageVersion09 = "0.9.0";
            var signedPackage = _fixture.SignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                Utils.CreatePackageInSource(testContext.PackageSource, signedPackage.Id, packageVersion09);
                SimpleTestPackageUtility.CreatePackages(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, packageVersion09);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                nugetConsole.UpdatePackageFromPMC(signedPackage.Id, signedPackage.Version);
                testContext.Project.Build();

                Utils.AssertPackageReferenceExists(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public void UpdateUnsignedPackageToSignedVersionFromPMCForPC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var packageVersion09 = "0.9.0";
            var signedPackage = _fixture.SignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                Utils.CreatePackageInSource(testContext.PackageSource, signedPackage.Id, packageVersion09);
                SimpleTestPackageUtility.CreatePackages(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, packageVersion09);
                nugetConsole.UpdatePackageFromPMC(signedPackage.Id, signedPackage.Version);
                testContext.Project.Build();

                Utils.AssetPackageInPackagesConfig(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public void DowngradeSignedPackageToUnsignedVersionFromPMCForPR(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            // This test is not considered an ideal behavior of the product but states the current behavior.
            // A package that is already installed as signed should be specailly treated and a user should not be
            // able to downgrade to an unsigned version. This test needs to be updated once this behavior gets
            // corrected in the product.

            var packageVersion09 = "0.9.0";
            var signedPackage = _fixture.SignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                Utils.CreatePackageInSource(testContext.PackageSource, signedPackage.Id, packageVersion09);
                SimpleTestPackageUtility.CreatePackages(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                nugetConsole.UpdatePackageFromPMC(signedPackage.Id, packageVersion09);
                testContext.Project.Build();

                Utils.AssertPackageReferenceExists(VisualStudio, testContext.Project, signedPackage.Id, packageVersion09, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public void DowngradeSignedPackageToUnsignedVersionFromPMCForPC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            // This test is not considered an ideal behavior of the product but states the current behavior.
            // A package that is already installed as signed should be specailly treated and a user should not be
            // able to downgrade to an unsigned version. This test needs to be updated once this behavior gets
            // corrected in the product.

            var packageVersion09 = "0.9.0";
            var signedPackage = _fixture.SignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                Utils.CreatePackageInSource(testContext.PackageSource, signedPackage.Id, packageVersion09);
                SimpleTestPackageUtility.CreatePackages(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);
                nugetConsole.UpdatePackageFromPMC(signedPackage.Id, packageVersion09);

                Utils.AssetPackageInPackagesConfig(VisualStudio, testContext.Project, signedPackage.Id, packageVersion09, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public void InstallSignedPackageWithExpiredCertificateFromPMCForPR(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var signedPackagePath = _fixture.ExpiredCertSignedTestPackagePath;
            var signedPackage = _fixture.ExpiredCertSignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                File.Copy(signedPackagePath, Path.Combine(testContext.PackageSource, signedPackage.PackageName));

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);
                testContext.Project.Build();

                // TODO: Fix bug where no warnings are shwon when package is untrusted but still installed
                //nugetConsole.IsMessageFoundInPMC("expired certificate").Should().BeTrue("expired certificate warning");
                Utils.AssertPackageReferenceExists(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public void InstallSignedPackageWithExpiredCertificateFromPMCForPC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

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
        public void InstallSignedTamperedPackageFromPMCAndFailForPR(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var signedPackage = _fixture.SignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                SimpleTestPackageUtility.CreatePackages(testContext.PackageSource, signedPackage);
                SignedArchiveTestUtility.TamperWithPackage(Path.Combine(testContext.PackageSource, signedPackage.PackageName));

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version);
                nugetConsole.IsMessageFoundInPMC("package integrity check failed").Should().BeTrue("Integrity failed message shown.");
                testContext.Project.Build();

                Utils.AssertPackageReferenceDoesNotExist(VisualStudio, testContext.Project, signedPackage.Id, signedPackage.Version, XunitLogger);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public void InstallSignedTamperedPackageFromPMCAndFailForPC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var signedPackage = _fixture.SignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                SimpleTestPackageUtility.CreatePackages(testContext.PackageSource, signedPackage);
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