// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FluentAssertions;
using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGet.StaFact;
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

        // Verify PR only, packages.config is tested in InstallPackageFromPMCVerifyGetPackageDisplaysPackage
        [NuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public void InstallPackageFromPMCWithNoAutoRestoreVerifyAssetsFile(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                // Turn off auto restore
                pathContext.Settings.DisableAutoRestore();

                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                var project = Utils.CreateAndInitProject(projectTemplate, pathContext, solutionService);

                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion);

                var nugetConsole = GetConsole(project);

                var installed = nugetConsole.InstallPackageFromPMC(packageName, packageVersion);
                installed.Should().BeTrue("Install-Package should pass");

                // Verify install from project.assets.json
                var inAssetsFile = Utils.IsPackageInstalledInAssetsFile(nugetConsole, project.FullPath, packageName, packageVersion);
                inAssetsFile.Should().BeTrue("package was installed");

                solutionService.Save();
            }
        }

        // Verify packages.config and PackageReference
        [NuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void InstallPackageFromPMCVerifyGetPackageDisplaysPackage(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();
                var project = Utils.CreateAndInitProject(projectTemplate, pathContext, solutionService);

                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion);

                var nugetConsole = GetConsole(project);

                var installed = nugetConsole.InstallPackageFromPMC(packageName, packageVersion);
                installed.Should().BeTrue("Install-Package should pass");

                // Build before the install check to ensure that everything is up to date.
                project.Build();

                // Verify install from Get-Package
                GetNuGetTestService().Verify.PackageIsInstalled(project.UniqueName, packageName, packageVersion);

                VisualStudio.AssertNoErrors();

                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void InstallPackageFromPMCFromNuGetOrg(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                pathContext.Settings.DisableAutoRestore();

                var packageSources = SimpleTestSettingsContext.GetOrAddSection(pathContext.Settings.XML, "packageSources");
                SimpleTestSettingsContext.AddEntry(packageSources, "nugetOnline", "https://api.nuget.org/v3/index.json");
                pathContext.Settings.Save();

                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();
                var project = Utils.CreateAndInitProject(projectTemplate, pathContext, solutionService);
                var nugetConsole = GetConsole(project);

                var packageName = "newtonsoft.json";
                var packageVersion = "9.0.1";

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion)
                    .Should()
                    .BeTrue("Install-Package should return on time");

                project.Build();

                Utils.IsPackageInstalledInAssetsFile(nugetConsole, project.FullPath, packageName, packageVersion).Should().BeTrue("package should exist in the assets file");

                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void UninstallPackageFromPMC(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = Utils.CreateAndInitProject(projectTemplate, pathContext, solutionService);

                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion);

                var nugetConsole = GetConsole(project);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion).Should().BeTrue("Install-Package");
                project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(project.UniqueName, packageName, packageVersion);

                nugetConsole.UninstallPackageFromPMC(packageName).Should().BeTrue("Uninstall-Package");
                project.Build();

                GetNuGetTestService().Verify.PackageIsNotInstalled(project.UniqueName, packageName);

                VisualStudio.AssertNoErrors();

                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void UpdatePackageFromPMC(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = Utils.CreateAndInitProject(projectTemplate, pathContext, solutionService);

                var packageName = "TestPackage";
                var packageVersion1 = "1.0.0";
                var packageVersion2 = "2.0.0";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion1);
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion2);

                var nugetConsole = GetConsole(project);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion1).Should().BeTrue("Install-Package");
                project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(project.UniqueName, packageName, packageVersion1);

                nugetConsole.UpdatePackageFromPMC(packageName, packageVersion2).Should().BeTrue("UnInstall-Package");
                project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(project.UniqueName, packageName, packageVersion2);

                VisualStudio.AssertNoErrors();

                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void InstallMultiplePackagesFromPMC(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = Utils.CreateAndInitProject(projectTemplate, pathContext, solutionService);

                var packageName1 = "TestPackage1";
                var packageVersion1 = "1.0.0";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName1, packageVersion1);

                var packageName2 = "TestPackage2";
                var packageVersion2 = "1.2.3";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName2, packageVersion2);

                var nugetConsole = GetConsole(project);

                nugetConsole.InstallPackageFromPMC(packageName1, packageVersion1).Should().BeTrue("Install-Package 1");
                nugetConsole.InstallPackageFromPMC(packageName2, packageVersion2).Should().BeTrue("Install-Package 2");
                project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(project.UniqueName, packageName1, packageVersion1);
                GetNuGetTestService().Verify.PackageIsInstalled(project.UniqueName, packageName2, packageVersion2);

                VisualStudio.AssertNoErrors();

                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void UninstallMultiplePackagesFromPMC(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = Utils.CreateAndInitProject(projectTemplate, pathContext, solutionService);

                var packageName1 = "TestPackage1";
                var packageVersion1 = "1.0.0";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName1, packageVersion1);

                var packageName2 = "TestPackage2";
                var packageVersion2 = "1.2.3";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName2, packageVersion2);

                var nugetConsole = GetConsole(project);

                nugetConsole.InstallPackageFromPMC(packageName1, packageVersion1).Should().BeTrue("Install-Package 1");
                nugetConsole.InstallPackageFromPMC(packageName2, packageVersion2).Should().BeTrue("Install-Package 2");
                project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(project.UniqueName, packageName1, packageVersion1);
                GetNuGetTestService().Verify.PackageIsInstalled(project.UniqueName, packageName2, packageVersion2);

                VisualStudio.AssertNoErrors();

                Assert.True(nugetConsole.UninstallPackageFromPMC(packageName1));
                Assert.True(nugetConsole.UninstallPackageFromPMC(packageName2));
                project.Build();
                solutionService.SaveAll();

                GetNuGetTestService().Verify.PackageIsNotInstalled(project.UniqueName, packageName1);
                GetNuGetTestService().Verify.PackageIsNotInstalled(project.UniqueName, packageName2);

                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void DowngradePackageFromPMC(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = Utils.CreateAndInitProject(projectTemplate, pathContext, solutionService);

                var packageName = "TestPackage";
                var packageVersion1 = "1.0.0";
                var packageVersion2 = "2.0.0";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion1);
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion2);

                var nugetConsole = GetConsole(project);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion2).Should().BeTrue("Install-Package");
                project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(project.UniqueName, packageName, packageVersion2);

                nugetConsole.UpdatePackageFromPMC(packageName, packageVersion1).Should().BeTrue("Update-Package");
                project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(project.UniqueName, packageName, packageVersion1);

                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetNetCoreTemplates))]
        public void NetCoreTransitivePackageReference(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();


                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project1 = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject1");
                project1.Build();
                var project2 = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject2");
                project2.Build();
                var project3 = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject3");
                project3.Build();
                solutionService.Build();

                project1.References.Dte.AddProjectReference(project2);
                project2.References.Dte.AddProjectReference(project3);
                solutionService.SaveAll();
                solutionService.Build();

                var nugetConsole = GetConsole(project3);
                var packageName = "newtonsoft.json";
                var packageVersion = "9.0.1";

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion, "https://api.nuget.org/v3/index.json").Should().BeTrue("Install-Package");
                project1.Build();
                project2.Build();
                project3.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(project3.UniqueName, packageName, packageVersion);

                Assert.True(project1.References.TryFindReferenceByName("newtonsoft.json", out var result));
                Assert.NotNull(result);
                Assert.True(project2.References.TryFindReferenceByName("newtonsoft.json", out var result2));
                Assert.NotNull(result2);

                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetNetCoreTemplates))]
        public void NetCoreTransitivePackageReferenceLimit(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project1 = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject1");
                project1.Build();
                var project2 = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject2");
                project2.Build();
                var project3 = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject3");
                project3.Build();
                var projectX = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProjectX");
                projectX.Build();
                solutionService.Build();

                project1.References.Dte.AddProjectReference(project2);
                project1.References.Dte.AddProjectReference(projectX);
                project2.References.Dte.AddProjectReference(project3);
                solutionService.SaveAll();
                solutionService.Build();

                var nugetConsole = GetConsole(project3);

                var packageName = "newtonsoft.json";
                var packageVersion = "9.0.1";

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion, "https://api.nuget.org/v3/index.json").Should().BeTrue("Install-Package");
                project1.Build();
                project2.Build();
                project3.Build();
                projectX.Build();
                solutionService.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(project3.UniqueName, packageName, packageVersion);

                Assert.True(project1.References.TryFindReferenceByName("newtonsoft.json", out var result));
                Assert.NotNull(result);
                Assert.True(project2.References.TryFindReferenceByName("newtonsoft.json", out var result2));
                Assert.NotNull(result2);
                Assert.False(projectX.References.TryFindReferenceByName("newtonsoft.json", out var resultX));
                Assert.Null(resultX);

                solutionService.Save();
            }
        }

        public static IEnumerable<object[]> GetNetCoreTemplates()
        {
            for (var i = 0; i < Utils.GetIterations(); i++)
            {
                yield return new object[] { ProjectTemplate.NetCoreConsoleApp };
                yield return new object[] { ProjectTemplate.NetStandardClassLib };
            }
        }

        public static IEnumerable<object[]> GetPackageReferenceTemplates()
        {
            for (var i = 0; i < Utils.GetIterations(); i++)
            {
                yield return new object[] { ProjectTemplate.NetCoreConsoleApp };
                yield return new object[] { ProjectTemplate.NetStandardClassLib };
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
