using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGet.StaFact;
using NuGet.Test.Utility;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Tests.Apex
{
    public class NetCoreProjectTestCase : SharedVisualStudioHostTestClass, IClassFixture<VisualStudioHostFixtureFactory>
    {
        public NetCoreProjectTestCase(VisualStudioHostFixtureFactory visualStudioHostFixtureFactory, ITestOutputHelper output)
            : base(visualStudioHostFixtureFactory, output)
        {
        }

        // basic create for .net core template
        [NuGetWpfTheory(Skip = "https://github.com/NuGet/Home/issues/11308")]
        [MemberData(nameof(GetNetCoreTemplates))]
        public void CreateNetCoreProject_RestoresNewProject(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger, addNetStandardFeeds: true))
            {
                VisualStudio.AssertNoErrors();
            }
        }

        // basic create for .net core template
        [NuGetWpfTheory(Skip = "https://github.com/NuGet/Home/issues/9410")]
        [MemberData(nameof(GetNetCoreTemplates))]
        public void CreateNetCoreProject_AddProjectReference(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger, addNetStandardFeeds: true))
            {
                var project2 = testContext.SolutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject2");
                project2.Build();

                testContext.Project.References.Dte.AddProjectReference(project2);
                testContext.SolutionService.SaveAll();

                testContext.SolutionService.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                VisualStudio.AssertNoErrors();
                CommonUtility.AssertPackageInAssetsFile(VisualStudio, testContext.Project, "TestProject2", "1.0.0", XunitLogger);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetNetCoreTemplates))]
        public async Task WithSourceMappingEnabled_InstallsPackageFromPMUIFromExpectedSource_Succeeds(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using var simpleTestPathContext = new SimpleTestPathContext();
            string solutionDirectory = simpleTestPathContext.SolutionRoot;
            var privateRepositoryPath = Path.Combine(solutionDirectory, "PrivateRepository");
            Directory.CreateDirectory(privateRepositoryPath);
            var externalRepositoryPath = Path.Combine(solutionDirectory, "ExternalRepository");
            Directory.CreateDirectory(externalRepositoryPath);

            var packageName = "Contoso.a";
            var packageVersion = "1.0.0";

            await CommonUtility.CreatePackageInSourceAsync(privateRepositoryPath, packageName, packageVersion);
            await CommonUtility.CreatePackageInSourceAsync(externalRepositoryPath, packageName, packageVersion);


            // Create nuget.config with Package source mapping filtering rules before project is created.
            CommonUtility.CreateConfigurationFile(Path.Combine(solutionDirectory, "NuGet.config"), $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
    <!--To inherit the global NuGet package sources remove the <clear/> line below -->
    <clear />
    <add key=""ExternalRepository"" value=""{externalRepositoryPath}"" />
    <add key=""PrivateRepository"" value=""{privateRepositoryPath}"" />
    </packageSources>
    <packageSourceMapping>
        <packageSource key=""externalRepository"">
            <package pattern=""External.*"" />
            <package pattern=""Others.*"" />
        </packageSource>
        <packageSource key=""PrivateRepository"">
            <package pattern=""contoso.*"" />
            <package pattern=""Test.*"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");

            using var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger, addNetStandardFeeds: true, simpleTestPathContext: simpleTestPathContext);

            VisualStudio.AssertNoErrors();

            // Act
            OpenNuGetPackageManagerWithDte();
            var nugetTestService = GetNuGetTestService();
            var uiwindow = nugetTestService.GetUIWindowfromProject(testContext.SolutionService.Projects[0]);
            uiwindow.InstallPackageFromUI(packageName, packageVersion);

            // Assert
            CommonUtility.AssertPackageReferenceExists(VisualStudio, testContext.SolutionService.Projects[0], packageName, packageVersion, XunitLogger);
            Assert.Contains($"Installed {packageName} {packageVersion} from {privateRepositoryPath}", GetPackageManagerOutputWindowPaneText());
        }

        // There  is a bug with VS or Apex where NetCoreConsoleApp and NetCoreClassLib create netcore 2.1 projects that are not supported by the sdk
        // Commenting out any NetCoreConsoleApp or NetCoreClassLib template and swapping it for NetStandardClassLib as both are package ref.

        public static IEnumerable<object[]> GetNetCoreTemplates()
        {
            yield return new object[] { ProjectTemplate.NetCoreClassLib };
        }

        private void OpenNuGetPackageManagerWithDte()
        {
            VisualStudio.ObjectModel.Solution.WaitForOperationsInProgress(TimeSpan.FromMinutes(3));
            WaitForCommandAvailable("Project.ManageNuGetPackages", TimeSpan.FromMinutes(1));
            VisualStudio.Dte.ExecuteCommand("Project.ManageNuGetPackages");
        }

        private void WaitForCommandAvailable(string commandName, TimeSpan timeout)
        {
            WaitForCommandAvailable(VisualStudio.Dte.Commands.Item(commandName), timeout);
        }

        private void WaitForCommandAvailable(Command cmd, TimeSpan timeout)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            while (stopWatch.Elapsed < timeout)
            {
                if (cmd.IsAvailable)
                {
                    return;
                }
                System.Threading.Thread.Sleep(250);
            }

            XunitLogger.LogWarning($"Timed out waiting for {cmd.Name} to be available");
        }
    }
}
