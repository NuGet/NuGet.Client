// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Tests.Apex
{
    public class NuGetConsoleTestCase : SharedVisualStudioHostTestClass, IClassFixture<VisualStudioHostFixtureFactory>
    {
        public NuGetConsoleTestCase(VisualStudioHostFixtureFactory visualStudioHostFixtureFactory)
            : base(visualStudioHostFixtureFactory)
        {
        }

        [StaFact]
        public void InstallPackageFromPMC()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var dte = VisualStudio.Dte;
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                dte.ExecuteCommand("View.PackageManagerConsole");
                var project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");

                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion);

                var nugetTestService = GetNuGetTestService();
                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.Name);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion);

                Assert.True(nugetTestService.Verify.PackageIsInstalled(project.UniqueName, packageName));
                Assert.True(nugetConsole.IsPackageInstalled(packageName, packageVersion));

                nugetConsole.Clear();
                solutionService.Close();
            }
        }

        [StaFact]
        public void InstallPackageFromPMCFromNuGetOrg()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var dte = VisualStudio.Dte;
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                dte.ExecuteCommand("View.PackageManagerConsole");
                var project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");

                var nugetTestService = GetNuGetTestService();
                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.UniqueName);

                var packageName = "newtonsoft.json";
                var packageVersion = "9.0.1";
                nugetConsole.InstallPackageFromPMC(packageName, packageVersion, "https://api.nuget.org/v3/index.json");

                nugetTestService.Verify.PackageIsInstalled(project.UniqueName, packageName);
                Assert.True(nugetConsole.IsPackageInstalled(packageName, packageVersion));

                nugetConsole.Clear();
                solutionService.Close();
            }
        }

        [StaFact]
        public void UninstallPackageFromPMC()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var dte = VisualStudio.Dte;
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                dte.ExecuteCommand("View.PackageManagerConsole");
                var project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");

                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion);

                var nugetTestService = GetNuGetTestService();
                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.Name);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion);
                nugetTestService.Verify.PackageIsInstalled(project.UniqueName, packageName);
                Assert.True(nugetConsole.IsPackageInstalled(packageName, packageVersion));

                nugetConsole.UninstallPackageFromPMC(packageName);
                nugetTestService.Verify.PackageIsNotInstalled(project.UniqueName, packageName);
                Assert.False(nugetConsole.IsPackageInstalled(packageName, packageVersion));

                nugetConsole.Clear();
                solutionService.Close();
            }
        }

        [StaFact]
        public void UpdatePackageFromPMC()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var dte = VisualStudio.Dte;
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                dte.ExecuteCommand("View.PackageManagerConsole");
                var project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");

                var packageName = "TestPackage";
                var packageVersion1 = "1.0.0";
                var packageVersion2 = "2.0.0";
                CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion1);
                CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion2);

                var nugetTestService = GetNuGetTestService();
                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.UniqueName);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion1);
                nugetTestService.Verify.PackageIsInstalled(project.UniqueName, packageName, packageVersion1);
                Assert.True(nugetConsole.IsPackageInstalled(packageName, packageVersion1));

                nugetConsole.UpdatePackageFromPMC(packageName, packageVersion2);
                nugetTestService.Verify.PackageIsInstalled(project.UniqueName, packageName, packageVersion2);
                Assert.False(nugetConsole.IsPackageInstalled(packageName, packageVersion1));
                Assert.True(nugetConsole.IsPackageInstalled(packageName, packageVersion2));

                nugetConsole.Clear();
                solutionService.Close();
            }
        }


        private static void CreatePackageInSource(string packageSource, string packageName, string packageVersion)
        {
            var package = new SimpleTestPackageContext(packageName, packageVersion);
            package.Files.Clear();
            package.AddFile("lib/net45/_._");
            SimpleTestPackageUtility.CreatePackages(packageSource, package);
        }
    }
}
