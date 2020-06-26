// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using FluentAssertions;
using Microsoft.Test.Apex.VisualStudio.Solution;
using Microsoft.VisualStudio.OLE.Interop;
using NuGet.Test.Utility;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Tests.Apex
{
    public class IVsServicesTestCase : SharedVisualStudioHostTestClass, IClassFixture<VisualStudioHostFixtureFactory>
    {
        public IVsServicesTestCase(VisualStudioHostFixtureFactory visualStudioHostFixtureFactory, ITestOutputHelper output)
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
            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, projExt, "newtonsoft.json", XunitLogger);
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


        [StaFact]
        public void IVSPathContextProvider2_WithEmptySolution_WhenTryCreateUserWideContextIsCalled_SolutionWideConfigurationIsNotIncluded()
        {
            // Arrange
            using (var testContext = new SimpleTestPathContext())
            {
                EnsureVisualStudioHost();
                var dte = VisualStudio.Dte;
                var solutionService = VisualStudio.Get<SolutionService>();
                var nugetTestService = GetNuGetTestService();

                solutionService.CreateEmptySolution("project", testContext.SolutionRoot);

                // Act
                var userPackagesFolder = nugetTestService.GetUserPackagesFolderFromUserWideContext();

                // Assert
                // The global packages folder should not be the one configured by the test context!
                userPackagesFolder.Should().NotBe(testContext.UserPackagesFolder);
            }
        }

        [StaFact]
        public void IVSPathContextProvider2_WhenTryCreateUserWideContextIsCalled_SolutionWideConfigurationIsNotIncluded()
        {
            // Arrange
            using (var testContext = new SimpleTestPathContext())
            {
                EnsureVisualStudioHost();
                var dte = VisualStudio.Dte;
                var solutionService = VisualStudio.Get<SolutionService>();
                var nugetTestService = GetNuGetTestService();

                solutionService.CreateEmptySolution("project", testContext.SolutionRoot);
                var projExt = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");

                // Act
                var userPackagesFolder = nugetTestService.GetUserPackagesFolderFromUserWideContext();

                // Assert
                // The global packages folder should not be the one configured by the test context!
                userPackagesFolder.Should().NotBe(testContext.UserPackagesFolder);
            }
        }
    }
}
