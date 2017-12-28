// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
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

        [NuGetWpfTheory]
        [InlineData(ProjectTemplate.ClassLibrary)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
        public void InstallPackageFromPMC(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();
            
                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
                project.Build();

                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion);

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.Name);
                
                Assert.True(nugetConsole.InstallPackageFromPMC(packageName, packageVersion));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion));
                project.Build();
                Assert.True(VisualStudio.HasNoErrorsInErrorList());
                Assert.True(VisualStudio.HasNoErrorsInOutputWindows());

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [InlineData(ProjectTemplate.ClassLibrary)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
        public void InstallPackageFromPMCFromNuGetOrg(ProjectTemplate projectTemplate)
        {
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

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.UniqueName);

                var packageName = "newtonsoft.json";
                var packageVersion = "9.0.1";

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName, packageVersion, "https://api.nuget.org/v3/index.json"));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion));
                project.Build();
                Assert.True(VisualStudio.HasNoErrorsInErrorList());
                Assert.True(VisualStudio.HasNoErrorsInOutputWindows());
                nugetConsole.Clear();

                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [InlineData(ProjectTemplate.ClassLibrary)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
        public void UninstallPackageFromPMC(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
                project.Build();

                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion);

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.Name);

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName, packageVersion));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion));
                project.Build();

                Assert.True(nugetConsole.UninstallPackageFromPMC(packageName));
                Assert.False(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion));

                solutionService.Save();
                project.Build();
                Assert.True(VisualStudio.HasNoErrorsInErrorList());
                Assert.True(VisualStudio.HasNoErrorsInOutputWindows());

                nugetConsole.Clear();
            }
        }

        [NuGetWpfTheory]
        [InlineData(ProjectTemplate.ClassLibrary)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
        public void UpdatePackageFromPMC(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
                project.Build();

                var packageName = "TestPackage";
                var packageVersion1 = "1.0.0";
                var packageVersion2 = "2.0.0";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion1);
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion2);

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.UniqueName);

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName, packageVersion1));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion1));
                project.Build();
                
                Assert.True(nugetConsole.UpdatePackageFromPMC(packageName, packageVersion2));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion2));
                Assert.False(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion1));
                project.Build();

                Assert.True(VisualStudio.HasNoErrorsInErrorList());
                Assert.True(VisualStudio.HasNoErrorsInOutputWindows());

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [InlineData(ProjectTemplate.ClassLibrary)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
        public void InstallMultiplePackagesFromPMC(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
                project.Build();

                var packageName1 = "TestPackage1";
                var packageVersion1 = "1.0.0";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName1, packageVersion1);

                var packageName2 = "TestPackage2";
                var packageVersion2 = "1.2.3";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName2, packageVersion2);

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.Name);

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName1, packageVersion1));
                Assert.True(nugetConsole.InstallPackageFromPMC(packageName2, packageVersion2));
                project.Build();
                Assert.True(VisualStudio.HasNoErrorsInErrorList());
                Assert.True(VisualStudio.HasNoErrorsInOutputWindows());
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName1, packageVersion1));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName2, packageVersion2));

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [InlineData(ProjectTemplate.ClassLibrary)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
        public void UninstallMultiplePackagesFromPMC(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
                project.Build();

                var packageName1 = "TestPackage1";
                var packageVersion1 = "1.0.0";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName1, packageVersion1);

                var packageName2 = "TestPackage2";
                var packageVersion2 = "1.2.3";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName2, packageVersion2);

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.Name);

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName1, packageVersion1));
                Assert.True(nugetConsole.InstallPackageFromPMC(packageName2, packageVersion2));
                project.Build();
                Assert.True(VisualStudio.HasNoErrorsInErrorList());
                Assert.True(VisualStudio.HasNoErrorsInOutputWindows());
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName1, packageVersion1));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName2, packageVersion2));

                Assert.True(nugetConsole.UninstallPackageFromPMC(packageName1));
                Assert.True(nugetConsole.UninstallPackageFromPMC(packageName2));
                project.Build();
                solutionService.SaveAll();

                Assert.False(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName1, packageVersion1));
                Assert.False(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName2, packageVersion2));

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [InlineData(ProjectTemplate.ClassLibrary)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
        public void DowngradePackageFromPMC(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
                project.Build();

                var packageName = "TestPackage";
                var packageVersion1 = "1.0.0";
                var packageVersion2 = "2.0.0";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion1);
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion2);

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.UniqueName);

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName, packageVersion2));
                project.Build();
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion2));

                Assert.True(nugetConsole.UpdatePackageFromPMC(packageName, packageVersion1));
                project.Build();

                Assert.False(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion2));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion1));

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
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
                
                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                project1.References.Dte.AddProjectReference(project2);
                project2.References.Dte.AddProjectReference(project3);
                solutionService.SaveAll();
                solutionService.Build();


                var nugetConsole = nugetTestService.GetPackageManagerConsole(project3.UniqueName);
                var packageName = "newtonsoft.json";
                var packageVersion = "9.0.1";

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName, packageVersion, "https://api.nuget.org/v3/index.json"));
                project1.Build();
                project2.Build();
                project3.Build();

                Assert.True(Utils.IsPackageInstalled(nugetConsole, project3.FullPath, packageName, packageVersion));

                Assert.True(project1.References.TryFindReferenceByName("newtonsoft.json", out var result));
                Assert.NotNull(result);
                Assert.True(project2.References.TryFindReferenceByName("newtonsoft.json", out var result2));
                Assert.NotNull(result2);

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
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

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                project1.References.Dte.AddProjectReference(project2);
                project1.References.Dte.AddProjectReference(projectX);
                project2.References.Dte.AddProjectReference(project3);
                solutionService.SaveAll();
                solutionService.Build();


                var nugetConsole = nugetTestService.GetPackageManagerConsole(project3.UniqueName);
                var packageName = "newtonsoft.json";
                var packageVersion = "9.0.1";

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName, packageVersion, "https://api.nuget.org/v3/index.json"));
                project1.Build();
                project2.Build();
                project3.Build();
                projectX.Build();
                solutionService.Build();

                Assert.True(Utils.IsPackageInstalled(nugetConsole, project3.FullPath, packageName, packageVersion));

                Assert.True(project1.References.TryFindReferenceByName("newtonsoft.json", out var result));
                Assert.NotNull(result);
                Assert.True(project2.References.TryFindReferenceByName("newtonsoft.json", out var result2));
                Assert.NotNull(result2);
                Assert.False(projectX.References.TryFindReferenceByName("newtonsoft.json", out var resultX));
                Assert.Null(resultX);

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

    }
}
