// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
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
        [InlineData(ProjectTemplate.ClassLibrary)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
        public void InstallSignedPackageFromPMC(ProjectTemplate projectTemplate)
        {
            var signedPackage = _fixture.SignedTestPackage;

            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
                project.Build();

                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, signedPackage);

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.Name);

                Assert.True(nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, signedPackage.Id, signedPackage.Version));
                project.Build();
                Assert.True(VisualStudio.HasNoErrorsInErrorList());
                Assert.True(VisualStudio.HasNoErrorsInOutputWindows());

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [CIOnlyNuGetWpfTheory]
        [InlineData(ProjectTemplate.ClassLibrary)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
        public void UninstallSignedPackageFromPMC(ProjectTemplate projectTemplate)
        {
            var signedPackage = _fixture.SignedTestPackage;

            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
                project.Build();

                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, signedPackage);

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.Name);

                Assert.True(nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, signedPackage.Id, signedPackage.Version));
                project.Build();

                Assert.True(nugetConsole.UninstallPackageFromPMC(signedPackage.Id));
                Assert.False(Utils.IsPackageInstalled(nugetConsole, project.FullPath, signedPackage.Id, signedPackage.Version));

                solutionService.Save();
                project.Build();
                Assert.True(VisualStudio.HasNoErrorsInErrorList());
                Assert.True(VisualStudio.HasNoErrorsInOutputWindows());

                nugetConsole.Clear();
            }
        }

        [CIOnlyNuGetWpfTheory]
        [InlineData(ProjectTemplate.ClassLibrary)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
        public void UpdateUnsignedPackageToSignedVersionFromPMC(ProjectTemplate projectTemplate)
        {
            var packageVersion09 = "0.9.0";
            var signedPackage = _fixture.SignedTestPackage;

            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
                project.Build();

                Utils.CreatePackageInSource(pathContext.PackageSource, signedPackage.Id, packageVersion09);
                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, signedPackage);

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.UniqueName);

                Assert.True(nugetConsole.InstallPackageFromPMC(signedPackage.Id, packageVersion09));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, signedPackage.Id, packageVersion09));
                project.Build();

                Assert.True(nugetConsole.UpdatePackageFromPMC(signedPackage.Id, signedPackage.Version));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, signedPackage.Id, signedPackage.Version));
                Assert.False(Utils.IsPackageInstalled(nugetConsole, project.FullPath, signedPackage.Id, packageVersion09));
                project.Build();

                Assert.True(VisualStudio.HasNoErrorsInErrorList());
                Assert.True(VisualStudio.HasNoErrorsInOutputWindows());

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [CIOnlyNuGetWpfTheory]
        [InlineData(ProjectTemplate.ClassLibrary)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
        public void DowngradeSignedPackageToUnsignedVersionFromPMC(ProjectTemplate projectTemplate)
        {
            var packageVersion09 = "0.9.0";
            var signedPackage = _fixture.SignedTestPackage;

            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
                project.Build();

                Utils.CreatePackageInSource(pathContext.PackageSource, signedPackage.Id, packageVersion09);
                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, signedPackage);

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.UniqueName);

                Assert.True(nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version));
                project.Build();
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, signedPackage.Id, signedPackage.Version));

                Assert.True(nugetConsole.UpdatePackageFromPMC(signedPackage.Id, packageVersion09));
                project.Build();

                Assert.False(Utils.IsPackageInstalled(nugetConsole, project.FullPath, signedPackage.Id, signedPackage.Version));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, signedPackage.Id, packageVersion09));

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [CIOnlyNuGetWpfTheory]
        [InlineData(ProjectTemplate.ClassLibrary)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
        public void InstallSignedPackageWithExpiredCertificateFromPMC(ProjectTemplate projectTemplate)
        {
            var signedPackagePath = _fixture.ExpiredCertSignedTestPackagePath;
            var signedPackage = _fixture.ExpiredCertSignedTestPackage;

            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
                project.Build();

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                File.Copy(signedPackagePath, Path.Combine(pathContext.PackageSource, signedPackage.PackageName));

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.Name);

                Assert.True(nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, signedPackage.Id, signedPackage.Version));
                project.Build();
                Assert.True(VisualStudio.HasNoErrorsInErrorList());
                Assert.True(VisualStudio.HasNoErrorsInOutputWindows());

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [CIOnlyNuGetWpfTheory]
        [InlineData(ProjectTemplate.ClassLibrary)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
        public void InstallSignedTamperedPackageFromPMCAndFail(ProjectTemplate projectTemplate)
        {
            var signedPackage = _fixture.SignedTestPackage;

            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
                project.Build();

                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, signedPackage);
                SignedArchiveTestUtility.TamperWithPackage(Path.Combine(pathContext.PackageSource, signedPackage.PackageName));

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.UniqueName);

                Assert.True(nugetConsole.InstallPackageFromPMC(signedPackage.Id, signedPackage.Version));
                Assert.True(nugetConsole.IsMessageFoundInPMC("package integrity check failed"));
                project.Build();

                Assert.False(Utils.IsPackageInstalled(nugetConsole, project.FullPath, signedPackage.Id, signedPackage.Version));

                nugetConsole.Clear();
                solutionService.Save();
            }
        }
    }
}