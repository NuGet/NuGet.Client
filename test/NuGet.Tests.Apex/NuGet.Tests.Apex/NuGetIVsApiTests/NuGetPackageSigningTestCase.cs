// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
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
            var signedPackage = _fixture.SignedTestPackage;

            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();
                var project = Utils.CreateAndInitProject(projectTemplate, pathContext, solutionService);

                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(project);

                var installed = nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version).Should().BeTrue("Install-Package should pass");

                // Build before the install check to ensure that everything is up to date.
                project.Build();

                // Verify install from Get-Package
                GetNuGetTestService().Verify.PackageIsInstalled(project.UniqueName, signedPackage.Id, signedPackage.Version);

                VisualStudio.AssertNoErrors();

                solutionService.Save();
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void UninstallSignedPackageFromPMC(ProjectTemplate projectTemplate)
        {
            var signedPackage = _fixture.SignedTestPackage;

            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();
                var project = Utils.CreateAndInitProject(projectTemplate, pathContext, solutionService);

                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version).Should().BeTrue("Install-Package");
                project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(project.UniqueName, signedPackage.Id, signedPackage.Version);

                nugetConsole.UninstallPackageFromPMC(signedPackage.Id).Should().BeTrue("Uninstall-Package");
                project.Build();

                GetNuGetTestService().Verify.PackageIsNotInstalled(project.UniqueName, signedPackage.Id);

                VisualStudio.AssertNoErrors();

                solutionService.Save();
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void UpdateUnsignedPackageToSignedVersionFromPMC(ProjectTemplate projectTemplate)
        {
            var packageVersion09 = "0.9.0";
            var signedPackage = _fixture.SignedTestPackage;

            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();
                var project = Utils.CreateAndInitProject(projectTemplate, pathContext, solutionService);

                Utils.CreatePackageInSource(pathContext.PackageSource, signedPackage.Id, packageVersion09);
                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, packageVersion09).Should().BeTrue("Install-Package");
                project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(project.UniqueName, signedPackage.Id, packageVersion09);

                nugetConsole.UpdatePackageFromPMC(signedPackage.Id, signedPackage.Version).Should().BeTrue("Update-Package");
                project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(project.UniqueName, signedPackage.Id, signedPackage.Version);
                GetNuGetTestService().Verify.PackageIsNotInstalled(project.UniqueName, signedPackage.Id, packageVersion09);

                VisualStudio.AssertNoErrors();

                solutionService.Save();
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void DowngradeSignedPackageToUnsignedVersionFromPMC(ProjectTemplate projectTemplate)
        {
            // This test is not considered an ideal behavior of the product but states the current behavior.
            // A package that is already installed as signed should be specailly treated and a user should not be
            // able to downgrade to an unsigned version. This test needs to be updated once this behavior gets
            // corrected in the product.

            var packageVersion09 = "0.9.0";
            var signedPackage = _fixture.SignedTestPackage;

            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();
                var project = Utils.CreateAndInitProject(projectTemplate, pathContext, solutionService);

                Utils.CreatePackageInSource(pathContext.PackageSource, signedPackage.Id, packageVersion09);
                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, signedPackage);

                var nugetConsole = GetConsole(project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version).Should().BeTrue("Install-Package");
                project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(project.UniqueName, signedPackage.Id, signedPackage.Version);

                nugetConsole.UpdatePackageFromPMC(signedPackage.Id, packageVersion09).Should().BeTrue("Update-Package");
                project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(project.UniqueName, signedPackage.Id, packageVersion09);
                GetNuGetTestService().Verify.PackageIsNotInstalled(project.UniqueName, signedPackage.Id, signedPackage.Version);

                VisualStudio.AssertNoErrors();

                solutionService.Save();
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void InstallSignedPackageWithExpiredCertificateFromPMC(ProjectTemplate projectTemplate)
        {
            var signedPackagePath = _fixture.ExpiredCertSignedTestPackagePath;
            var signedPackage = _fixture.ExpiredCertSignedTestPackage;

            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();
                var project = Utils.CreateAndInitProject(projectTemplate, pathContext, solutionService);

                File.Copy(signedPackagePath, Path.Combine(pathContext.PackageSource, signedPackage.PackageName));

                var nugetConsole = GetConsole(project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version).Should().BeTrue("Install-Package");
                project.Build();

                // TODO: Fix bug where no warnings are shwon when package is untrusted but still installed
                //Assert.True(nugetConsole.IsMessageFoundInPMC("expired certificate"));
                GetNuGetTestService().Verify.PackageIsInstalled(project.UniqueName, signedPackage.Id, signedPackage.Version);

                VisualStudio.AssertNoErrors();

                solutionService.Save();
            }
        }

        [CIOnlyNuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void InstallSignedTamperedPackageFromPMCAndFail(ProjectTemplate projectTemplate)
        {
            var signedPackage = _fixture.SignedTestPackage;

            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();
                var project = Utils.CreateAndInitProject(projectTemplate, pathContext, solutionService);

                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, signedPackage);
                SignedArchiveTestUtility.TamperWithPackage(Path.Combine(pathContext.PackageSource, signedPackage.PackageName));

                var nugetConsole = GetConsole(project);

                nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version).Should().BeTrue("Install-Package");
                nugetConsole.IsMessageFoundInPMC("package integrity check failed").Should().BeTrue("Integrity failed message shown.");
                project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(project.UniqueName, signedPackage.Id, signedPackage.Version);

                VisualStudio.AssertNoErrors();

                solutionService.Save();
            }
        }

        public static IEnumerable<object[]> GetTemplates()
        {
            for (var i = 0; i < Utils.GetIterations(); i++)
            {
                yield return new object[] { ProjectTemplate.ClassLibrary };
                yield return new object[] { ProjectTemplate.NetCoreConsoleApp };
            }
        }
    }
}