// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
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
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");

                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion);

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.Name);

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName, packageVersion));
                Assert.True(nugetConsole.IsPackageInstalled(packageName, packageVersion));

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [StaFact]
        public void InstallPackageFromPMCFromNuGetOrg()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.UniqueName);

                var packageName = "newtonsoft.json";
                var packageVersion = "9.0.1";

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName, packageVersion, "https://api.nuget.org/v3/index.json"));
                Assert.True(nugetConsole.IsPackageInstalled(packageName, packageVersion));

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [StaFact]
        public void UninstallPackageFromPMC()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");

                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion);

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.Name);

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName, packageVersion));
                Assert.True(nugetConsole.IsPackageInstalled(packageName, packageVersion));

                Assert.True(nugetConsole.UninstallPackageFromPMC(packageName));
                Assert.False(nugetConsole.IsPackageInstalled(packageName, packageVersion));

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [StaFact]
        public void UpdatePackageFromPMC()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");

                var packageName = "TestPackage";
                var packageVersion1 = "1.0.0";
                var packageVersion2 = "2.0.0";
                CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion1);
                CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion2);

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.UniqueName);

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName, packageVersion1));
                Assert.True(nugetConsole.IsPackageInstalled(packageName, packageVersion1));

                Assert.True(nugetConsole.UpdatePackageFromPMC(packageName, packageVersion2));
                Assert.False(nugetConsole.IsPackageInstalled(packageName, packageVersion1));
                Assert.True(nugetConsole.IsPackageInstalled(packageName, packageVersion2));

                nugetConsole.Clear();
                solutionService.Save();
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
