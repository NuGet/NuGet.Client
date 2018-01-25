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

namespace NuGet.Tests.Apex
{
    public class NuGetPackageSigningTestCase : SharedVisualStudioHostTestClass, IClassFixture<SignedPackagesTestsApexFixture>
    {
        private SignedPackagesTestsApexFixture _fixture;

        public NuGetPackageSigningTestCase(SignedPackagesTestsApexFixture apexSigningFixture)
            : base(apexSigningFixture)
        {
            _fixture = apexSigningFixture;
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void InstallSignedPackageFromPMC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var signedPackage = _fixture.SignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate))
            {
                SimpleTestPackageUtility.CreatePackages(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version).Should().BeTrue("Install-Package should pass");

                // Build before the install check to ensure that everything is up to date.
                testContext.Project.Build();

                // Verify install from Get-Package
                GetNuGetTestService().Verify.PackageIsInstalled(testContext.Project.UniqueName, signedPackage.Id, signedPackage.Version);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void UninstallSignedPackageFromPMC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var signedPackage = _fixture.SignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate))
            {
                SimpleTestPackageUtility.CreatePackages(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version).Should().BeTrue("Install-Package");
                testContext.Project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(testContext.Project.UniqueName, signedPackage.Id, signedPackage.Version);

                nugetConsole.UninstallPackageFromPMC(signedPackage.Id).Should().BeTrue("Uninstall-Package");
                testContext.Project.Build();

                GetNuGetTestService().Verify.PackageIsNotInstalled(testContext.Project.UniqueName, signedPackage.Id);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void UpdateUnsignedPackageToSignedVersionFromPMC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var packageVersion09 = "0.9.0";
            var signedPackage = _fixture.SignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate))
            {
                Utils.CreatePackageInSource(testContext.PackageSource, signedPackage.Id, packageVersion09);
                SimpleTestPackageUtility.CreatePackages(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, packageVersion09).Should().BeTrue("Install-Package");
                testContext.Project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(testContext.Project.UniqueName, signedPackage.Id, packageVersion09);

                nugetConsole.UpdatePackageFromPMC(signedPackage.Id, signedPackage.Version).Should().BeTrue("Update-Package");
                testContext.Project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(testContext.Project.UniqueName, signedPackage.Id, signedPackage.Version);
                GetNuGetTestService().Verify.PackageIsNotInstalled(testContext.Project.UniqueName, signedPackage.Id, packageVersion09);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void DowngradeSignedPackageToUnsignedVersionFromPMC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            // This test is not considered an ideal behavior of the product but states the current behavior.
            // A package that is already installed as signed should be specailly treated and a user should not be
            // able to downgrade to an unsigned version. This test needs to be updated once this behavior gets
            // corrected in the product.

            var packageVersion09 = "0.9.0";
            var signedPackage = _fixture.SignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate))
            {
                Utils.CreatePackageInSource(testContext.PackageSource, signedPackage.Id, packageVersion09);
                SimpleTestPackageUtility.CreatePackages(testContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version).Should().BeTrue("Install-Package");
                testContext.Project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(testContext.Project.UniqueName, signedPackage.Id, signedPackage.Version);

                nugetConsole.UpdatePackageFromPMC(signedPackage.Id, packageVersion09).Should().BeTrue("Update-Package");
                testContext.Project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(testContext.Project.UniqueName, signedPackage.Id, packageVersion09);
                GetNuGetTestService().Verify.PackageIsNotInstalled(testContext.Project.UniqueName, signedPackage.Id, signedPackage.Version);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void InstallSignedPackageWithExpiredCertificateFromPMC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var signedPackagePath = _fixture.ExpiredCertSignedTestPackagePath;
            var signedPackage = _fixture.ExpiredCertSignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate))
            {
                File.Copy(signedPackagePath, Path.Combine(testContext.PackageSource, signedPackage.PackageName));

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version).Should().BeTrue("Install-Package");
                testContext.Project.Build();

                // TODO: Fix bug where no warnings are shwon when package is untrusted but still installed
                //nugetConsole.IsMessageFoundInPMC("expired certificate").Should().BeTrue("expired certificate warning");
                GetNuGetTestService().Verify.PackageIsInstalled(testContext.Project.UniqueName, signedPackage.Id, signedPackage.Version);
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void InstallSignedTamperedPackageFromPMCAndFail(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            var signedPackage = _fixture.SignedTestPackage;

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate))
            {
                SimpleTestPackageUtility.CreatePackages(testContext.PackageSource, signedPackage);
                SignedArchiveTestUtility.TamperWithPackage(Path.Combine(testContext.PackageSource, signedPackage.PackageName));

                var nugetConsole = GetConsole(testContext.Project);

                // TODO: install should fail, therefore Should().BeFalse()
                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version).Should().BeTrue("Install-Package");
                nugetConsole.IsMessageFoundInPMC("package integrity check failed").Should().BeTrue("Integrity failed message shown.");
                testContext.Project.Build();

                GetNuGetTestService().Verify.PackageIsNotInstalled(testContext.Project.UniqueName, signedPackage.Id);
            }
        }

        public static IEnumerable<object[]> GetTemplates()
        {
            for (var i = 0; i < Utils.GetIterations(); i++)
            {
                yield return new object[] { ProjectTemplate.ClassLibrary };
                yield return new object[] { ProjectTemplate.NetStandardClassLib };
            }
        }
    }
}