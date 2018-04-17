// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Test.Apex.VisualStudio.Solution;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Tests.Apex
{
    public class IVsPackageInstallerTestCase : SharedVisualStudioHostTestClass, IClassFixture<VisualStudioHostFixtureFactory>
    {
        public IVsPackageInstallerTestCase(VisualStudioHostFixtureFactory visualStudioHostFixtureFactory, ITestOutputHelper output)
            : base(visualStudioHostFixtureFactory, output)
        {
        }

        [StaFact]
        public void SimpleInstallFromIVsInstaller()
        {
            // Arrange
            EnsureVisualStudioHost();
            var dte = VisualStudio.Dte;
            var solutionService = VisualStudio.Get<SolutionService>();
            var nugetTestService = GetNuGetTestService();

            solutionService.CreateEmptySolution();
            var projExt = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");

            var project = dte.Solution.Projects.Item(1);

            // Act
            nugetTestService.InstallPackage(project.UniqueName, "newtonsoft.json");

            // Assert
            CommonUtility.AssetPackageInPackagesConfig(VisualStudio, projExt, "newtonsoft.json", XunitLogger);
        }

        [StaFact]
        public void SimpleUninstallFromIVsInstaller()
        {
            // Arrange
            EnsureVisualStudioHost();
            var solutionService = VisualStudio.Get<SolutionService>();
            var nugetTestService = GetNuGetTestService();
            solutionService.CreateEmptySolution();
            var projExt = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");
            var dte = VisualStudio.Dte;
            var project = dte.Solution.Projects.Item(1);
            nugetTestService.InstallPackage(project.UniqueName, "newtonsoft.json");

            // Act
            nugetTestService.UninstallPackage(project.UniqueName, "newtonsoft.json");

            // Assert
            CommonUtility.AssertPackageNotInPackagesConfig(VisualStudio, projExt, "newtonsoft.json", XunitLogger);
        }
    }
}
