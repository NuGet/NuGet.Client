// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.Test.Apex.VisualStudio.Solution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet.Test.Utility;

namespace NuGet.Tests.Apex
{
    public partial class NuGetConsoleTestCase
    {
        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task UpdatePackageFromPMCInMultipleProjects_KeepsOldVersionUntilUnusedAsync()
        {
            using var pathContext = new SimpleTestPathContext();
            pathContext.Settings.SetPackageFormatToPackagesConfig();
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ConsoleApplication, Logger, simpleTestPathContext: pathContext);
            var project2 = testContext.SolutionService.AddProject(
                ProjectLanguage.CSharp,
                ProjectTemplate.ClassLibrary,
                CommonUtility.DefaultTargetFramework,
                "TestProject2");
            testContext.SolutionService.SaveAll();
            var packageName = "SharedPackage";
            await CreatePackagesAsync(testContext.PackageSource, Package(packageName, "1.2.0"), Package(packageName, "2.5.1"));
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, packageName, "1.2.0", testContext.PackageSource);
            Install(console, project2, packageName, "1.2.0", testContext.PackageSource);
            Update(console, testContext.Project, packageName, testContext.PackageSource, "-Version 2.5.1");

            AssertInstalled(testContext.Project, packageName, "2.5.1");
            AssertInstalled(project2, packageName, "1.2.0");
            AssertSolutionPackage(pathContext, packageName, "1.2.0", exists: true);
            AssertSolutionPackage(pathContext, packageName, "2.5.1", exists: true);

            Update(console, project2, packageName, testContext.PackageSource, "-Version 2.5.1");

            AssertInstalled(project2, packageName, "2.5.1");
            AssertSolutionPackage(pathContext, packageName, "1.2.0", exists: false);
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task UpdatePackageFromPMCWithSharedDependency_UpdatesEntireGraphAsync()
        {
            using var testContext = CreatePackagesConfigContext();
            string prefix = "SharedGraph";
            await CreatePackagesAsync(
                testContext.PackageSource,
                Package($"{prefix}.A", "1.0.0"),
                Package($"{prefix}.A", "2.0.0"),
                Package($"{prefix}.A", "3.0.0"),
                Package($"{prefix}.B", "1.0.0", ($"{prefix}.A", "[1.0.0,)")),
                Package($"{prefix}.B", "2.0.0", ($"{prefix}.A", "[2.0.0,)")),
                Package($"{prefix}.C", "1.0.0", ($"{prefix}.A", "[2.0.0,)")),
                Package($"{prefix}.C", "2.0.0", ($"{prefix}.A", "[3.0.0,)")),
                Package($"{prefix}.D", "1.0.0", ($"{prefix}.B", "[1.0.0]"), ($"{prefix}.C", "[1.0.0]")),
                Package($"{prefix}.D", "2.0.0", ($"{prefix}.B", "[2.0.0]"), ($"{prefix}.C", "[2.0.0]")));
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, $"{prefix}.D", "1.0.0", testContext.PackageSource);
            AssertInstalled(testContext.Project, $"{prefix}.D", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.B", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.C", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.A", "2.0.0");
            AssertSolutionPackage(testContext, $"{prefix}.D", "1.0.0", exists: true);
            AssertSolutionPackage(testContext, $"{prefix}.B", "1.0.0", exists: true);
            AssertSolutionPackage(testContext, $"{prefix}.C", "1.0.0", exists: true);
            AssertSolutionPackage(testContext, $"{prefix}.A", "2.0.0", exists: true);
            AssertSolutionPackage(testContext, $"{prefix}.A", "1.0.0", exists: false);

            Update(console, testContext.Project, $"{prefix}.D", testContext.PackageSource);

            AssertInstalled(testContext.Project, $"{prefix}.D", "2.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.B", "2.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.C", "2.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.A", "3.0.0");
            AssertNotInstalled(testContext.Project, $"{prefix}.D", "1.0.0");
            AssertNotInstalled(testContext.Project, $"{prefix}.B", "1.0.0");
            AssertNotInstalled(testContext.Project, $"{prefix}.C", "1.0.0");
            AssertNotInstalled(testContext.Project, $"{prefix}.A", "2.0.0");
            AssertSolutionPackage(testContext, $"{prefix}.D", "2.0.0", exists: true);
            AssertSolutionPackage(testContext, $"{prefix}.B", "2.0.0", exists: true);
            AssertSolutionPackage(testContext, $"{prefix}.C", "2.0.0", exists: true);
            AssertSolutionPackage(testContext, $"{prefix}.A", "3.0.0", exists: true);
            AssertSolutionPackage(testContext, $"{prefix}.D", "1.0.0", exists: false);
            AssertSolutionPackage(testContext, $"{prefix}.B", "1.0.0", exists: false);
            AssertSolutionPackage(testContext, $"{prefix}.C", "1.0.0", exists: false);
            AssertSolutionPackage(testContext, $"{prefix}.A", "2.0.0", exists: false);
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task UpdatePackageFromPMCResolvesDependenciesAcrossEnabledSourcesAsync()
        {
            using var pathContext = new SimpleTestPathContext();
            pathContext.Settings.SetPackageFormatToPackagesConfig();
            var dependencySource = Path.Combine(pathContext.SolutionRoot, "DependencySource");
            Directory.CreateDirectory(dependencySource);
            pathContext.Settings.AddSource("DependencySource", dependencySource);
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ConsoleApplication, Logger, simpleTestPathContext: pathContext);
            var packageName = "CrossSource.Package";
            var dependencyName = "CrossSource.Dependency";
            await CreatePackagesAsync(
                testContext.PackageSource,
                Package(packageName, "1.0.0"),
                Package(packageName, "2.0.0", (dependencyName, "[1.0.0]")));
            await CreatePackagesAsync(dependencySource, Package(dependencyName, "1.0.0"));
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, packageName, "1.0.0", testContext.PackageSource);
            Update(console, testContext.Project, packageName, testContext.PackageSource);

            AssertInstalled(testContext.Project, packageName, "2.0.0");
            AssertInstalled(testContext.Project, dependencyName, "1.0.0");
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task UpdateDependencyFromPMC_UpdatesDependentPackageToCompatibleVersionAsync()
        {
            using var testContext = CreatePackagesConfigContext();
            var dependencyName = "DependentUpdate.Dependency";
            var packageName = "DependentUpdate.Package";
            await CreatePackagesAsync(
                testContext.PackageSource,
                Package(dependencyName, "1.4.1"),
                Package(dependencyName, "2.0.3"),
                Package(packageName, "1.8.0", (dependencyName, "[1.4.1,2.0.0)")),
                Package(packageName, "1.8.0.1", (dependencyName, "[2.0.0,)")));
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, packageName, "1.8.0", testContext.PackageSource);
            AssertInstalled(testContext.Project, packageName, "1.8.0");
            AssertInstalled(testContext.Project, dependencyName, "1.4.1");

            Update(console, testContext.Project, dependencyName, testContext.PackageSource, "-Version 2.0.3");

            AssertInstalled(testContext.Project, dependencyName, "2.0.3");
            AssertInstalled(testContext.Project, packageName, "1.8.0.1");
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task UpdatePackageFromPMCWithNewSharedDependency_UpdatesGraphAsync()
        {
            using var testContext = CreatePackagesConfigContext();
            string prefix = "NewSharedDependency";
            await CreatePackagesAsync(
                testContext.PackageSource,
                Package($"{prefix}.B", "1.0.0"),
                Package($"{prefix}.B", "2.0.0"),
                Package($"{prefix}.C", "2.0.0"),
                Package($"{prefix}.D", "1.0.0", ($"{prefix}.B", "[1.0.0]")),
                Package($"{prefix}.D", "2.0.0", ($"{prefix}.B", "[2.0.0]"), ($"{prefix}.C", "[2.0.0]")));
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, $"{prefix}.D", "1.0.0", testContext.PackageSource);
            AssertInstalled(testContext.Project, $"{prefix}.D", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.B", "1.0.0");
            AssertSolutionPackage(testContext, $"{prefix}.D", "1.0.0", exists: true);
            AssertSolutionPackage(testContext, $"{prefix}.B", "1.0.0", exists: true);

            Update(console, testContext.Project, $"{prefix}.D", testContext.PackageSource);

            AssertInstalled(testContext.Project, $"{prefix}.D", "2.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.B", "2.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.C", "2.0.0");
            AssertNotInstalled(testContext.Project, $"{prefix}.D", "1.0.0");
            AssertNotInstalled(testContext.Project, $"{prefix}.B", "1.0.0");
            AssertSolutionPackage(testContext, $"{prefix}.D", "2.0.0", exists: true);
            AssertSolutionPackage(testContext, $"{prefix}.B", "2.0.0", exists: true);
            AssertSolutionPackage(testContext, $"{prefix}.C", "2.0.0", exists: true);
            AssertSolutionPackage(testContext, $"{prefix}.D", "1.0.0", exists: false);
            AssertSolutionPackage(testContext, $"{prefix}.B", "1.0.0", exists: false);
            AssertNoErrors(console);
        }

        [DataTestMethod]
        [DataRow("F")]
        [DataRow("E")]
        [Timeout(DefaultTimeout)]
        public async Task UpdateSubtreeFromPMC_UpdatesOnlySelectedSubtreeAsync(string packageToUpdate)
        {
            using var testContext = CreatePackagesConfigContext();
            string prefix = $"Subtree{packageToUpdate}";
            await CreatePackagesAsync(
                testContext.PackageSource,
                Package($"{prefix}.D", "1.0.0"),
                Package($"{prefix}.G", "1.0.0"),
                Package($"{prefix}.E", "1.0.0"),
                Package($"{prefix}.E", "2.0.0", ($"{prefix}.G", "[1.0.0]")),
                Package($"{prefix}.F", "1.0.0"),
                Package($"{prefix}.F", "2.0.0", ($"{prefix}.G", "[1.0.0]")),
                Package($"{prefix}.B", "1.0.0", ($"{prefix}.E", "[1.0.0,)"), ($"{prefix}.F", "[1.0.0,)")),
                Package($"{prefix}.C", "1.0.0", ($"{prefix}.D", "[1.0.0,)")),
                Package($"{prefix}.A", "1.0.0", ($"{prefix}.B", "[1.0.0,)"), ($"{prefix}.C", "[1.0.0,)")));
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, $"{prefix}.A", "1.0.0", testContext.PackageSource);
            AssertInstalled(testContext.Project, $"{prefix}.A", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.B", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.C", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.D", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.E", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.F", "1.0.0");
            Update(console, testContext.Project, $"{prefix}.{packageToUpdate}", testContext.PackageSource);

            AssertInstalled(testContext.Project, $"{prefix}.A", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.B", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.C", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.D", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.E", packageToUpdate == "E" ? "2.0.0" : "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.F", packageToUpdate == "F" ? "2.0.0" : "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.G", "1.0.0");
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task UpdateSubtreeFromPMCWithConflict_DoesNotChangePackagesAsync()
        {
            using var testContext = CreatePackagesConfigContext();
            string prefix = "Conflict";
            await CreatePackagesAsync(
                testContext.PackageSource,
                Package($"{prefix}.B", "1.0.0"),
                Package($"{prefix}.C", "1.0.0"),
                Package($"{prefix}.C", "2.0.0"),
                Package($"{prefix}.D", "1.0.0"),
                Package($"{prefix}.A", "1.0.0", ($"{prefix}.B", "[1.0.0,)"), ($"{prefix}.C", "[1.0.0,)"), ($"{prefix}.D", "[1.0.0,)")),
                Package($"{prefix}.H", "1.0.0", ($"{prefix}.C", "[1.0.0]")));
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, $"{prefix}.A", "1.0.0", testContext.PackageSource);
            Install(console, testContext.Project, $"{prefix}.H", "1.0.0", testContext.PackageSource);
            Update(console, testContext.Project, $"{prefix}.C", testContext.PackageSource);
            string output = console.GetText();

            output.Should().Contain(
                $"Unable to resolve dependencies. '{prefix}.C 2.0.0' is not compatible with " +
                $"'{prefix}.H 1.0.0 constraint: {prefix}.C (= 1.0.0)'.",
                because: output);
            AssertInstalled(testContext.Project, $"{prefix}.A", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.B", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.C", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.D", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.H", "1.0.0");
            AssertNotInstalled(testContext.Project, $"{prefix}.C", "2.0.0");
            AssertSolutionPackage(testContext, $"{prefix}.C", "2.0.0", exists: false);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task UpdatePackageFromPMCWithOlderSharedDependencyInUse_UpdatesToCompatibleVersionAsync()
        {
            using var testContext = CreatePackagesConfigContext();
            string prefix = "OlderShared";
            await CreatePackagesAsync(
                testContext.PackageSource,
                Package($"{prefix}.A", "1.0.0"),
                Package($"{prefix}.A", "2.0.0"),
                Package($"{prefix}.B", "1.0.0", ($"{prefix}.A", "[1.0.0,)")),
                Package($"{prefix}.B", "2.0.0", ($"{prefix}.A", "[2.0.0,)")),
                Package($"{prefix}.C", "1.0.0"),
                Package($"{prefix}.C", "2.0.0"),
                Package($"{prefix}.G", "1.0.0"),
                Package($"{prefix}.K", "1.0.0", ($"{prefix}.A", "[1.0.0,)")),
                Package($"{prefix}.D", "1.0.0", ($"{prefix}.B", "[1.0.0]"), ($"{prefix}.C", "[1.0.0]")),
                Package($"{prefix}.D", "2.0.0", ($"{prefix}.B", "[2.0.0]"), ($"{prefix}.C", "[2.0.0]"), ($"{prefix}.G", "[1.0.0]")));
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, $"{prefix}.K", "1.0.0", testContext.PackageSource);
            AssertInstalled(testContext.Project, $"{prefix}.K", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.A", "1.0.0");
            AssertSolutionPackage(testContext, $"{prefix}.K", "1.0.0", exists: true);
            AssertSolutionPackage(testContext, $"{prefix}.A", "1.0.0", exists: true);
            Install(console, testContext.Project, $"{prefix}.D", "1.0.0", testContext.PackageSource);
            AssertInstalled(testContext.Project, $"{prefix}.D", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.B", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.C", "1.0.0");
            Update(console, testContext.Project, $"{prefix}.D", testContext.PackageSource);

            AssertInstalled(testContext.Project, $"{prefix}.K", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.D", "2.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.B", "2.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.C", "2.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.G", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.A", "2.0.0");
            AssertNotInstalled(testContext.Project, $"{prefix}.A", "1.0.0");
            foreach (var package in new[] { "K", "D", "B", "C", "G", "A" })
            {
                AssertSolutionPackage(
                    testContext,
                    $"{prefix}.{package}",
                    package == "K" || package == "G" ? "1.0.0" : "2.0.0",
                    exists: true);
            }
            foreach (var package in new[] { "D", "B", "C", "A" })
            {
                AssertSolutionPackage(testContext, $"{prefix}.{package}", "1.0.0", exists: false);
            }
            AssertNoErrors(console);
        }

        [DataTestMethod]
        [DataRow("child")]
        [DataRow("parent")]
        [Timeout(DefaultTimeout)]
        public async Task UpdatePackageFromPMCWithRelativeSource_UpdatesPackageAsync(string relativeSourceKind)
        {
            using var pathContext = new SimpleTestPathContext();
            pathContext.Settings.SetPackageFormatToPackagesConfig();
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ConsoleApplication, Logger, simpleTestPathContext: pathContext);
            var packageName = "RelativeSourcePackage";
            string parentDirectory = Path.GetDirectoryName(testContext.PackageSource)!;
            string actualSource = relativeSourceKind == "child" ? testContext.PackageSource : parentDirectory;
            await CreatePackagesAsync(
                actualSource,
                Package(packageName, "1.0.0"),
                Package(packageName, relativeSourceKind == "child" ? "2.0.0" : "3.0.0"));
            var console = GetConsole(testContext.Project);
            Install(console, testContext.Project, packageName, "1.0.0", actualSource);

            string currentDirectory;
            string relativeSource;
            if (relativeSourceKind == "child")
            {
                currentDirectory = parentDirectory;
                relativeSource = Path.GetFileName(testContext.PackageSource);
            }
            else
            {
                currentDirectory = testContext.PackageSource;
                relativeSource = @"..\";
            }

            console.Execute($"Push-Location '{currentDirectory}'; Update-Package {packageName} -ProjectName {testContext.Project.Name} -Source '{relativeSource}'; Pop-Location");
            string output = console.GetText();

            output.Should().NotContain("FullyQualifiedErrorId", because: output);
            AssertInstalled(testContext.Project, packageName, relativeSourceKind == "child" ? "2.0.0" : "3.0.0");
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task UpdatePackageFromPMCInAllProjects_UpdatesEveryInstalledVersionAsync()
        {
            using var pathContext = new SimpleTestPathContext();
            pathContext.Settings.SetPackageFormatToPackagesConfig();
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ConsoleApplication, Logger, simpleTestPathContext: pathContext);
            var project2 = AddProject(testContext, ProjectTemplate.ClassLibrary, "TestProject2");
            var project3 = AddProject(testContext, ProjectTemplate.ClassLibrary, "TestProject3");
            var project4 = AddProject(testContext, ProjectTemplate.WebSiteEmpty, "TestProject4");
            var projects = new[] { testContext.Project, project2, project3, project4 };
            var versions = new[] { "2.0.1", "2.1.0.76", "2.2.0", "2.2.1" };
            var packageName = "AllProjectsPackage";
            await CreatePackagesAsync(
                testContext.PackageSource,
                Package(packageName, versions[0]),
                Package(packageName, versions[1]),
                Package(packageName, versions[2]),
                Package(packageName, versions[3]),
                Package(packageName, "3.2.2"));
            var console = GetConsole(testContext.Project);

            for (var index = 0; index < projects.Length; index++)
            {
                Install(console, projects[index], packageName, versions[index], testContext.PackageSource);
                AssertInstalled(projects[index], packageName, versions[index]);
                AssertSolutionPackage(pathContext, packageName, versions[index], exists: true);
            }

            console.Execute($"Update-Package {packageName} -Source '{testContext.PackageSource}'");

            foreach (var project in projects)
            {
                AssertInstalled(project, packageName, "3.2.2");
            }
            foreach (var version in versions)
            {
                AssertSolutionPackage(pathContext, packageName, version, exists: false);
            }
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task UpdateAllPackagesFromPMC_UpdatesGraphsAcrossSolutionAsync()
        {
            using var pathContext = new SimpleTestPathContext();
            pathContext.Settings.SetPackageFormatToPackagesConfig();
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ConsoleApplication, Logger, simpleTestPathContext: pathContext);
            var project2 = AddProject(testContext, ProjectTemplate.ClassLibrary, "TestProject2");
            string prefix = "SolutionUpdate";
            await CreatePackagesAsync(
                testContext.PackageSource,
                Package($"{prefix}.B", "1.0.0"),
                Package($"{prefix}.B", "2.0.0"),
                Package($"{prefix}.A", "1.0.0", ($"{prefix}.B", "[1.0.0]")),
                Package($"{prefix}.A", "2.0.0", ($"{prefix}.B", "[2.0.0]")),
                Package($"{prefix}.D", "2.0.0"),
                Package($"{prefix}.D", "4.0.0", ($"{prefix}.E", "[3.0.0]")),
                Package($"{prefix}.E", "3.0.0"),
                Package($"{prefix}.C", "1.0.0", ($"{prefix}.D", "[2.0.0,)")));
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, $"{prefix}.A", "1.0.0", testContext.PackageSource);
            Install(console, project2, $"{prefix}.C", "1.0.0", testContext.PackageSource);
            AssertInstalled(testContext.Project, $"{prefix}.A", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.B", "1.0.0");
            AssertInstalled(project2, $"{prefix}.C", "1.0.0");
            AssertInstalled(project2, $"{prefix}.D", "2.0.0");
            console.Execute($"Update-Package -Source '{testContext.PackageSource}'");

            AssertInstalled(testContext.Project, $"{prefix}.A", "2.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.B", "2.0.0");
            AssertInstalled(project2, $"{prefix}.C", "1.0.0");
            AssertInstalled(project2, $"{prefix}.D", "4.0.0");
            AssertInstalled(project2, $"{prefix}.E", "3.0.0");
            AssertNotInstalled(testContext.Project, $"{prefix}.A", "1.0.0");
            AssertNotInstalled(project2, $"{prefix}.D", "2.0.0");
            AssertSolutionPackage(testContext, $"{prefix}.A", "1.0.0", exists: false);
            AssertSolutionPackage(testContext, $"{prefix}.B", "1.0.0", exists: false);
            AssertSolutionPackage(testContext, $"{prefix}.D", "2.0.0", exists: false);
            AssertSolutionPackage(testContext, $"{prefix}.A", "2.0.0", exists: true);
            AssertSolutionPackage(testContext, $"{prefix}.B", "2.0.0", exists: true);
            AssertSolutionPackage(testContext, $"{prefix}.C", "1.0.0", exists: true);
            AssertSolutionPackage(testContext, $"{prefix}.D", "4.0.0", exists: true);
            AssertSolutionPackage(testContext, $"{prefix}.E", "3.0.0", exists: true);
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task UpdateBottomDependencyFromPMC_UpdatesDiamondGraphAsync()
        {
            using var testContext = CreatePackagesConfigContext();
            string prefix = "DiamondUpdate";
            await CreatePackagesAsync(
                testContext.PackageSource,
                Package($"{prefix}.D", "1.0.0"),
                Package($"{prefix}.D", "2.0.0"),
                Package($"{prefix}.B", "1.0.0", ($"{prefix}.D", "[1.0.0]")),
                Package($"{prefix}.B", "2.0.0", ($"{prefix}.D", "[2.0.0]")),
                Package($"{prefix}.C", "1.0.0", ($"{prefix}.D", "[1.0.0]")),
                Package($"{prefix}.C", "2.0.0", ($"{prefix}.D", "[2.0.0]")),
                Package($"{prefix}.A", "1.0.0", ($"{prefix}.B", "[1.0.0]"), ($"{prefix}.C", "[1.0.0]")),
                Package($"{prefix}.A", "2.0.0", ($"{prefix}.B", "[2.0.0]"), ($"{prefix}.C", "[2.0.0]")));
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, $"{prefix}.A", "1.0.0", testContext.PackageSource);
            Update(console, testContext.Project, $"{prefix}.D", testContext.PackageSource);

            foreach (var package in new[] { "A", "B", "C", "D" })
            {
                AssertInstalled(testContext.Project, $"{prefix}.{package}", "2.0.0");
                AssertNotInstalled(testContext.Project, $"{prefix}.{package}", "1.0.0");
                AssertSolutionPackage(testContext, $"{prefix}.{package}", "2.0.0", exists: true);
                AssertSolutionPackage(testContext, $"{prefix}.{package}", "1.0.0", exists: false);
            }
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task UpdateDependencyFromPMC_SelectsLowestCompatibleDependentVersionAsync()
        {
            using var testContext = CreatePackagesConfigContext();
            string prefix = "LowestCompatible";
            await CreatePackagesAsync(
                testContext.PackageSource,
                Package($"{prefix}.B", "1.0.0"),
                Package($"{prefix}.B", "2.0.0"),
                Package($"{prefix}.A", "1.0.0", ($"{prefix}.B", "[1.0.0]")),
                Package($"{prefix}.A", "1.5.0", ($"{prefix}.B", "[2.0.0,)")),
                Package($"{prefix}.A", "2.0.0", ($"{prefix}.B", "[2.0.0,)")));
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, $"{prefix}.A", "1.0.0", testContext.PackageSource);
            Update(console, testContext.Project, $"{prefix}.B", testContext.PackageSource);

            AssertInstalled(testContext.Project, $"{prefix}.A", "1.5.0");
            AssertInstalled(testContext.Project, $"{prefix}.B", "2.0.0");
            AssertSolutionPackage(testContext, $"{prefix}.A", "1.5.0", exists: true);
            AssertSolutionPackage(testContext, $"{prefix}.B", "2.0.0", exists: true);
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task UpdatePackageFromPMCWithPackagesConfigConstraints_DoesNotChangePackagesAsync()
        {
            using var testContext = CreatePackagesConfigContext();
            var consoleApp = AddProject(testContext, ProjectTemplate.ConsoleApplication, "ConsoleApp");
            var webSite = AddProject(testContext, ProjectTemplate.WebSiteEmpty, "WebSite");
            var classLibrary = testContext.Project;
            await CreatePackagesAsync(
                testContext.PackageSource,
                Package("Constrained.A", "1.0.0", ("Constrained.B", "[1.0.0]")),
                Package("Constrained.A", "2.0.0", ("Constrained.B", "[2.0.0]")),
                Package("Constrained.B", "1.0.0"),
                Package("Constrained.B", "2.0.0"),
                Package("Constrained.C", "1.0.0", ("Constrained.D", "[1.0.0]")),
                Package("Constrained.C", "2.0.0", ("Constrained.D", "[2.0.0]")),
                Package("Constrained.D", "1.0.0"),
                Package("Constrained.D", "2.0.0"),
                Package("Constrained.E", "1.0.0", ("Constrained.F", "[1.0.0]")),
                Package("Constrained.F", "1.0.0"),
                Package("Constrained.F", "2.0.0"));
            var console = GetConsole(testContext.Project);

            Install(console, consoleApp, "Constrained.A", "1.0.0", testContext.PackageSource);
            Install(console, classLibrary, "Constrained.C", "1.0.0", testContext.PackageSource);
            Install(console, webSite, "Constrained.E", "1.0.0", testContext.PackageSource);
            AddPackageConstraint(consoleApp, "Constrained.A", "[1.0.0,2.0.0)");
            AddPackageConstraint(classLibrary, "Constrained.D", "[1.0.0]");
            AddPackageConstraint(webSite, "Constrained.E", "[1.0.0]");

            Update(console, consoleApp, "Constrained.A", testContext.PackageSource, "-Version 2.0.0");
            console.GetText().Should().Contain("additional constraint", because: console.GetText());
            Update(console, classLibrary, "Constrained.C", testContext.PackageSource);
            console.GetText().Should().Contain("Constrained.D", because: console.GetText());
            Update(console, webSite, "Constrained.F", testContext.PackageSource);
            console.GetText().Should().Contain("Constrained.E", because: console.GetText());

            AssertInstalled(consoleApp, "Constrained.A", "1.0.0");
            AssertInstalled(consoleApp, "Constrained.B", "1.0.0");
            AssertInstalled(classLibrary, "Constrained.C", "1.0.0");
            AssertInstalled(classLibrary, "Constrained.D", "1.0.0");
            AssertInstalled(webSite, "Constrained.E", "1.0.0");
            AssertInstalled(webSite, "Constrained.F", "1.0.0");
            AssertNotInstalled(consoleApp, "Constrained.A", "2.0.0");
            AssertNotInstalled(classLibrary, "Constrained.D", "2.0.0");
            AssertNotInstalled(webSite, "Constrained.F", "2.0.0");
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task UpdatePackageFromPMCAfterPackageFolderDeleted_UpdatesPackageAsync()
        {
            using var pathContext = new SimpleTestPathContext();
            pathContext.Settings.SetPackageFormatToPackagesConfig();
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ClassLibrary, Logger, simpleTestPathContext: pathContext);
            var packageName = "MissingPackage";
            await CreatePackagesAsync(testContext.PackageSource, Package(packageName, "1.0.0"), Package(packageName, "2.0.0"));
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, packageName, "1.0.0", testContext.PackageSource);
            Directory.Delete(pathContext.PackagesV2, recursive: true);
            console.Clear();

            Update(console, testContext.Project, packageName, testContext.PackageSource);
            string output = console.GetText();

            output.Should().NotContain(
                "Some NuGet packages are missing from the solution. The packages need to be restored in order to build the dependency graph.",
                because: output);
            AssertInstalled(testContext.Project, packageName, "2.0.0");
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task UpdatePackageFromPMCAfterPackageFolderDeletedWithoutRestoreConsent_FailsAsync()
        {
            using var pathContext = new SimpleTestPathContext();
            pathContext.Settings.SetPackageFormatToPackagesConfig();
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ClassLibrary, Logger, simpleTestPathContext: pathContext);
            var packageName = "MissingPackage";
            await CreatePackagesAsync(testContext.PackageSource, Package(packageName, "1.0.0"), Package(packageName, "2.0.0"));
            var console = GetConsole(testContext.Project);

            try
            {
                console.Execute(
                    "[NuGet.PackageManagement.VisualStudio.SettingsHelper]::Set('PackageRestoreConsentGranted', 'false');" +
                    "[NuGet.PackageManagement.VisualStudio.SettingsHelper]::Set('PackageRestoreIsAutomatic', 'false')");
                Install(console, testContext.Project, packageName, "1.0.0", testContext.PackageSource);
                Directory.Delete(pathContext.PackagesV2, recursive: true);
                console.Clear();

                Update(console, testContext.Project, packageName, testContext.PackageSource);
                string output = console.GetText();

                output.Should().Contain(
                    "Some NuGet packages are missing from the solution. The packages need to be restored in order to build the dependency graph.",
                    because: output);
                AssertInstalled(testContext.Project, packageName, "1.0.0");
            }
            finally
            {
                console.Execute(
                    "[NuGet.PackageManagement.VisualStudio.SettingsHelper]::Set('PackageRestoreConsentGranted', 'true');" +
                    "[NuGet.PackageManagement.VisualStudio.SettingsHelper]::Set('PackageRestoreIsAutomatic', 'true')");
            }
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task UpdatePackageFromPMCWithContentInLicenseBlocks_ReplacesProjectContentAsync()
        {
            using var pathContext = new SimpleTestPathContext();
            pathContext.Settings.SetPackageFormatToPackagesConfig();
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ClassLibrary, Logger, simpleTestPathContext: pathContext);
            var packageName = "ContentLicenseBlocks";
            var packageV1 = Package(packageName, "1.0.0");
            packageV1.AddFile("content/text", "This is a text file 1.0");
            var packageV2 = Package(packageName, "2.0.0");
            packageV2.AddFile("content/text", "This is a text file 2.0");
            await CreatePackagesAsync(testContext.PackageSource, packageV1, packageV2);
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, packageName, "1.0.0", testContext.PackageSource);
            var packageContentPath = Path.Combine(pathContext.PackagesV2, $"{packageName}.1.0.0", "content", "text");
            File.WriteAllText(
                packageContentPath,
                "***************NUget: Begin License Text ---------dsafdsafdas" + System.Environment.NewLine +
                "sdaflkjdsal;fj;ldsafdsa" + System.Environment.NewLine +
                "dsaflkjdsa;lkfj;ldsafas" + System.Environment.NewLine +
                "dsafdsafdsafsdaNuGet: End License Text-------------" + System.Environment.NewLine +
                "This is a text file 1.0");

            Update(console, testContext.Project, packageName, testContext.PackageSource);

            AssertInstalled(testContext.Project, packageName, "2.0.0");
            File.ReadAllText(Path.Combine(Path.GetDirectoryName(testContext.Project.FullPath)!, "text"))
                .Should()
                .Be("This is a text file 2.0");
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task UpdatePackageFromPMCPreservesRenamedPackagesConfigAsync()
        {
            using var testContext = CreatePackagesConfigContext();
            var packageName = "RenamedPackagesConfig";
            await CreatePackagesAsync(testContext.PackageSource, Package(packageName, "1.0.0"), Package(packageName, "2.0.0"));
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, packageName, "1.0.0", testContext.PackageSource);
            var projectDirectory = Path.GetDirectoryName(testContext.Project.FullPath)!;
            var renamedPackagesConfig = $"packages.{testContext.Project.Name}.config";
            VisualStudio.Dte.Solution.Projects.Item(1).ProjectItems.Item("packages.config").Name = renamedPackagesConfig;
            testContext.SolutionService.SaveAll();

            Update(console, testContext.Project, packageName, testContext.PackageSource);

            AssertInstalled(testContext.Project, packageName, "2.0.0");
            File.Exists(Path.Combine(projectDirectory, renamedPackagesConfig)).Should().BeTrue();
            File.Exists(Path.Combine(projectDirectory, "packages.config")).Should().BeFalse();
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public void UpdatePackageFromPMCWithNonStrongNamedAssemblies_DoesNotAddBindingRedirect()
        {
            using var pathContext = new SimpleTestPathContext();
            pathContext.Settings.SetPackageFormatToPackagesConfig();
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ConsoleApplication, Logger, simpleTestPathContext: pathContext);
            var source = GetEndToEndPackagesPath();
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, "NonStrongNameB", "1.0.0.0", source);
            Install(console, testContext.Project, "NonStrongNameA", "1.0.0.0", source);
            Update(console, testContext.Project, "NonStrongNameB", source);

            AssertInstalled(testContext.Project, "NonStrongNameB", "2.0.0.0");
            var appConfigPath = Path.Combine(Path.GetDirectoryName(testContext.Project.FullPath)!, "app.config");
            if (File.Exists(appConfigPath))
            {
                File.ReadAllText(appConfigPath).Should().NotContain("bindingRedirect");
            }

            AssertNoErrors(console);
        }

        [DataTestMethod]
        [DataRow(false, "1.6.1", "1.8.13")]
        [DataRow(true, "1.5.2", "1.8.13")]
        [Timeout(DefaultTimeout)]
        public async Task UpdateAllPackagesFromPMCInOneProject_LeavesOtherProjectUnchangedAsync(
            bool safe,
            string expectedPackageVersion,
            string expectedUiVersion)
        {
            using var pathContext = new SimpleTestPathContext();
            pathContext.Settings.SetPackageFormatToPackagesConfig();
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ConsoleApplication, Logger, simpleTestPathContext: pathContext);
            var project2 = AddProject(testContext, ProjectTemplate.ConsoleApplication, "TestProject2");
            var packageName = "ProjectScopedPackage";
            var uiPackageName = "ProjectScopedUiPackage";
            await CreatePackagesAsync(
                testContext.PackageSource,
                Package(packageName, "1.5.1"),
                Package(packageName, "1.5.2"),
                Package(packageName, "1.6.1"),
                Package(uiPackageName, "1.8.11"),
                Package(uiPackageName, "1.8.13"));
            var console = GetConsole(testContext.Project);

            foreach (var project in new[] { testContext.Project, project2 })
            {
                Install(console, project, packageName, "1.5.1", testContext.PackageSource);
                Install(console, project, uiPackageName, "1.8.11", testContext.PackageSource);
            }

            console.Execute($"Update-Package -ProjectName {testContext.Project.Name} -Source '{testContext.PackageSource}'{(safe ? " -Safe" : string.Empty)}");

            AssertInstalled(testContext.Project, packageName, expectedPackageVersion);
            AssertInstalled(testContext.Project, uiPackageName, expectedUiVersion);
            AssertInstalled(project2, packageName, "1.5.1");
            AssertInstalled(project2, uiPackageName, "1.8.11");
            AssertSolutionPackage(pathContext, packageName, "1.5.1", exists: true);
            AssertSolutionPackage(pathContext, packageName, expectedPackageVersion, exists: true);
            AssertSolutionPackage(pathContext, uiPackageName, "1.8.11", exists: true);
            AssertSolutionPackage(pathContext, uiPackageName, expectedUiVersion, exists: true);
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task UpdatePackageFromPMCWhenDependentHasNoCompatibleUpdate_FailsAsync()
        {
            using var testContext = CreatePackagesConfigContext();
            string prefix = "NoCompatibleUpdate";
            await CreatePackagesAsync(
                testContext.PackageSource,
                Package($"{prefix}.B", "1.0.0"),
                Package($"{prefix}.B", "2.0.0"),
                Package($"{prefix}.A", "1.0.0", ($"{prefix}.B", "[1.0.0]")));
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, $"{prefix}.A", "1.0.0", testContext.PackageSource);
            Update(console, testContext.Project, $"{prefix}.B", testContext.PackageSource);
            string output = console.GetText();

            output.Should().Contain(
                $"Unable to resolve dependencies. '{prefix}.B 2.0.0' is not compatible with " +
                $"'{prefix}.A 1.0.0 constraint: {prefix}.B (= 1.0.0)'.",
                because: output);
            AssertInstalled(testContext.Project, $"{prefix}.A", "1.0.0");
            AssertInstalled(testContext.Project, $"{prefix}.B", "1.0.0");
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task UpdateAllPackagesFromPMCInEmptyProject_DoesNotUpdateOtherProjectsAsync()
        {
            using var pathContext = new SimpleTestPathContext();
            pathContext.Settings.SetPackageFormatToPackagesConfig();
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ConsoleApplication, Logger, simpleTestPathContext: pathContext);
            var emptyProject = AddProject(testContext, ProjectTemplate.ConsoleApplication, "EmptyProject");
            var packageName = "OtherProjectPackage";
            await CreatePackagesAsync(testContext.PackageSource, Package(packageName, "1.5.1"), Package(packageName, "1.6.1"));
            var console = GetConsole(testContext.Project);
            Install(console, testContext.Project, packageName, "1.5.1", testContext.PackageSource);

            console.Execute($"Update-Package -ProjectName {emptyProject.Name} -Source '{testContext.PackageSource}'");

            AssertInstalled(testContext.Project, packageName, "1.5.1");
            AssertNotInstalled(emptyProject, packageName);
            AssertSolutionPackage(pathContext, packageName, "1.5.1", exists: true);
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task ReinstallPackageFromPMC_InvokesUninstallAndInstallScriptsAsync()
        {
            using var testContext = CreatePackagesConfigContext();
            var packageName = "ReinstallScripts";
            await CreatePackagesAsync(testContext.PackageSource, ScriptPackage(packageName, "1.0.0", "InstallScriptCount", "UninstallScriptCount"));
            var console = GetConsole(testContext.Project);
            Install(console, testContext.Project, packageName, "1.0.0", testContext.PackageSource);

            console.Execute("$global:InstallScriptCount = 0; $global:UninstallScriptCount = 4");
            Update(console, testContext.Project, packageName, testContext.PackageSource, "-Reinstall");
            console.Execute("'Install=' + $global:InstallScriptCount + ';Uninstall=' + $global:UninstallScriptCount");
            string output = console.GetText();

            output.Should().Contain("Install=1;Uninstall=5", because: output);
            AssertInstalled(testContext.Project, packageName, "1.0.0");
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task ReinstallAllPackagesFromPMCInProject_InvokesEveryUninstallAndInstallScriptAsync()
        {
            using var testContext = CreatePackagesConfigContext();
            await CreatePackagesAsync(
                testContext.PackageSource,
                ScriptPackage("ReinstallScripts", "1.0.0", "InstallScriptCount", "UninstallScriptCount"),
                ScriptPackage("MagicScripts", "1.0.0", "InstallMagicScript", "UninstallMagicScript"));
            var console = GetConsole(testContext.Project);
            Install(console, testContext.Project, "ReinstallScripts", "1.0.0", testContext.PackageSource);
            Install(console, testContext.Project, "MagicScripts", "1.0.0", testContext.PackageSource);

            console.Execute("$global:InstallScriptCount = 7; $global:UninstallScriptCount = 3; $global:InstallMagicScript = 4; $global:UninstallMagicScript = 6");
            console.Execute($"Update-Package -Reinstall -ProjectName {testContext.Project.Name} -Source '{testContext.PackageSource}'");
            console.Execute("'First=' + $global:InstallScriptCount + ',' + $global:UninstallScriptCount + ';Second=' + $global:InstallMagicScript + ',' + $global:UninstallMagicScript");
            string output = console.GetText();

            output.Should().Contain("First=8,4;Second=5,7", because: output);
            AssertInstalled(testContext.Project, "ReinstallScripts", "1.0.0");
            AssertInstalled(testContext.Project, "MagicScripts", "1.0.0");
        }

        [DataTestMethod]
        [DataRow(false, "2.0.0", "")]
        [DataRow(true, "1.0.1", " safe")]
        [Timeout(DefaultTimeout)]
        public async Task UpdatePackageFromPMCInAllProjects_InvokesScriptsForEveryProjectAsync(
            bool safe,
            string expectedVersion,
            string installMessageSuffix)
        {
            using var testContext = CreatePackagesConfigContext();
            var project1 = AddProject(testContext, ProjectTemplate.ClassLibrary, "Project1");
            var project2 = AddProject(testContext, ProjectTemplate.ClassLibrary, "Project2");
            var packageName = "MultiProjectScripts";
            await CreatePackagesAsync(
                testContext.PackageSource,
                ProjectMessageScriptPackage(packageName, "1.0.0", string.Empty),
                ProjectMessageScriptPackage(packageName, "1.0.1", " safe"),
                ProjectMessageScriptPackage(packageName, "2.0.0", string.Empty));
            var console = GetConsole(testContext.Project);
            Install(console, project1, packageName, "1.0.0", testContext.PackageSource);
            Install(console, project2, packageName, "1.0.0", testContext.PackageSource);

            console.Execute("$global:InstallPackageMessages = @(); $global:UninstallPackageMessages = @()");
            console.Execute($"Update-Package {packageName} -Source '{testContext.PackageSource}'{(safe ? " -Safe" : string.Empty)}");
            console.Execute(
                "'Install=' + (($global:InstallPackageMessages | Sort-Object) -join ',') + " +
                "';Uninstall=' + (($global:UninstallPackageMessages | Sort-Object) -join ',')");
            string output = console.GetText();

            output.Should().Contain(
                $"Install=Project1{installMessageSuffix},Project2{installMessageSuffix};" +
                "Uninstall=UninstallProject1,UninstallProject2",
                because: output);
            AssertInstalled(project1, packageName, expectedVersion);
            AssertInstalled(project2, packageName, expectedVersion);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task UpdateAllPackagesFromPMCInAllProjects_InvokesEveryInstallAndUninstallScriptAsync()
        {
            using var testContext = CreatePackagesConfigContext();
            var project1 = AddProject(testContext, ProjectTemplate.ClassLibrary, "Project1");
            var project2 = AddProject(testContext, ProjectTemplate.ClassLibrary, "Project2");
            await CreatePackagesAsync(
                testContext.PackageSource,
                ProjectMessageScriptPackage("MultiProjectScripts", "1.0.0", string.Empty),
                ProjectMessageScriptPackage("MultiProjectScripts", "2.0.0", string.Empty),
                ProjectMessageScriptPackage("SecondMultiProjectScripts", "1.0.0", "second"),
                ProjectMessageScriptPackage("SecondMultiProjectScripts", "2.0.0", "second"));
            var console = GetConsole(testContext.Project);
            foreach (var project in new[] { project1, project2 })
            {
                Install(console, project, "MultiProjectScripts", "1.0.0", testContext.PackageSource);
                Install(console, project, "SecondMultiProjectScripts", "1.0.0", testContext.PackageSource);
            }

            console.Execute("$global:InstallPackageMessages = @(); $global:UninstallPackageMessages = @()");
            console.Execute($"Update-Package -Source '{testContext.PackageSource}'");
            console.Execute(
                "'Install=' + (($global:InstallPackageMessages | Sort-Object) -join ',') + " +
                "';Uninstall=' + (($global:UninstallPackageMessages | Sort-Object) -join ',')");
            string output = console.GetText();

            output.Should().Contain(
                "Install=Project1,Project1second,Project2,Project2second;" +
                "Uninstall=UninstallProject1,UninstallProject1second,UninstallProject2,UninstallProject2second",
                because: output);
            foreach (var project in new[] { project1, project2 })
            {
                AssertInstalled(project, "MultiProjectScripts", "2.0.0");
                AssertInstalled(project, "SecondMultiProjectScripts", "2.0.0");
            }
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task ReinstallPackageFromPMCInAllProjects_InvokesScriptsForEveryProjectAsync()
        {
            using var testContext = CreatePackagesConfigContext();
            var project1 = AddProject(testContext, ProjectTemplate.ClassLibrary, "Project1");
            var project2 = AddProject(testContext, ProjectTemplate.ConsoleApplication, "Project2");
            var packageName = "ReinstallScripts";
            await CreatePackagesAsync(
                testContext.PackageSource,
                ScriptPackage(packageName, "1.0.0", "InstallScriptCount", "UninstallScriptCount"));
            var console = GetConsole(testContext.Project);
            Install(console, project1, packageName, "1.0.0", testContext.PackageSource);
            Install(console, project2, packageName, "1.0.0", testContext.PackageSource);

            console.Execute("$global:InstallScriptCount = 2; $global:UninstallScriptCount = 9");
            console.Execute($"Update-Package {packageName} -Reinstall -Source '{testContext.PackageSource}'");
            console.Execute("'Install=' + $global:InstallScriptCount + ';Uninstall=' + $global:UninstallScriptCount");
            string output = console.GetText();

            output.Should().Contain("Install=4;Uninstall=11", because: output);
            AssertInstalled(project1, packageName, "1.0.0");
            AssertInstalled(project2, packageName, "1.0.0");
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task ReinstallAllPackagesFromPMCInAllProjects_InvokesEveryUninstallAndInstallScriptAsync()
        {
            using var testContext = CreatePackagesConfigContext();
            var project1 = AddProject(testContext, ProjectTemplate.ClassLibrary, "Project1");
            var project2 = AddProject(testContext, ProjectTemplate.ConsoleApplication, "Project2");
            await CreatePackagesAsync(
                testContext.PackageSource,
                ScriptPackage("ReinstallScripts", "1.0.0", "InstallScriptCount", "UninstallScriptCount"),
                ScriptPackage("MagicScripts", "1.0.0", "InstallMagicScript", "UninstallMagicScript"));
            var console = GetConsole(testContext.Project);
            foreach (var project in new[] { project1, project2 })
            {
                Install(console, project, "ReinstallScripts", "1.0.0", testContext.PackageSource);
                Install(console, project, "MagicScripts", "1.0.0", testContext.PackageSource);
            }

            console.Execute(
                "$global:InstallScriptCount = 7; $global:UninstallScriptCount = 3; " +
                "$global:InstallMagicScript = 4; $global:UninstallMagicScript = 6");
            console.Execute($"Update-Package -Reinstall -Source '{testContext.PackageSource}'");
            console.Execute(
                "'First=' + $global:InstallScriptCount + ',' + $global:UninstallScriptCount + " +
                "';Second=' + $global:InstallMagicScript + ',' + $global:UninstallMagicScript");
            string output = console.GetText();

            output.Should().Contain("First=9,5;Second=6,8", because: output);
            foreach (var project in new[] { project1, project2 })
            {
                AssertInstalled(project, "ReinstallScripts", "1.0.0");
                AssertInstalled(project, "MagicScripts", "1.0.0");
            }
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task UpdatePackageFromPMCForFSharpProjectWithMultiplePackages_UpdatesPackageAsync()
        {
            using var testContext = CreateFSharpContext();
            await CreatePackagesAsync(
                testContext.PackageSource,
                Package("SkypePackage", "1.0"),
                Package("SkypePackage", "3.0"),
                Package("netfx-Guard", "1.2.0.0"));
            var console = GetConsole(testContext.Project);

            InstallByUniqueName(console, testContext.Project, "SkypePackage", "1.0", testContext.PackageSource);
            testContext.NuGetApexTestService.WaitForAutoRestore();
            InstallByUniqueName(console, testContext.Project, "netfx-Guard", "1.2.0.0", testContext.PackageSource);
            testContext.NuGetApexTestService.WaitForAutoRestore();
            CommonUtility.AssertPackageReferenceExists(testContext.Project, "SkypePackage", "1.0", Logger);
            CommonUtility.AssertPackageInAssetsFile(VisualStudio, testContext.Project, "SkypePackage", "1.0", Logger);

            console.Execute($"Update-Package -Source '{testContext.PackageSource}' -ProjectName '{testContext.Project.UniqueName}'");
            testContext.NuGetApexTestService.WaitForAutoRestore();

            CommonUtility.AssertPackageInAssetsFile(VisualStudio, testContext.Project, "SkypePackage", "3.0", Logger);
            CommonUtility.AssertPackageInAssetsFile(VisualStudio, testContext.Project, "netfx-Guard", "1.2.0.0", Logger);
            AssertNoErrors(console);
        }

        private ApexTestContext CreatePackagesConfigContext()
        {
            var pathContext = new SimpleTestPathContext();
            pathContext.Settings.SetPackageFormatToPackagesConfig();
            return new ApexTestContext(
                VisualStudio,
                ProjectTemplate.ClassLibrary,
                Logger,
                simpleTestPathContext: pathContext);
        }

        private static ProjectTestExtension AddProject(ApexTestContext testContext, ProjectTemplate template, string name)
        {
            var project = testContext.SolutionService.AddProject(
                ProjectLanguage.CSharp,
                template,
                CommonUtility.DefaultTargetFramework,
                name);
            testContext.SolutionService.SaveAll();
            return project;
        }

        private static SimpleTestPackageContext Package(
            string id,
            string version,
            params (string Id, string VersionRange)[] dependencies)
        {
            var package = CommonUtility.CreatePackage(id, version);
            foreach ((string dependencyId, string versionRange) in dependencies)
            {
                package.Dependencies.Add(new SimpleTestPackageContext(dependencyId, versionRange));
            }
            return package;
        }

        private static SimpleTestPackageContext ScriptPackage(
            string id,
            string version,
            string installVariable,
            string uninstallVariable)
        {
            var package = Package(id, version);
            package.AddFile("tools/install.ps1", $"$global:{installVariable}++");
            package.AddFile("tools/uninstall.ps1", $"$global:{uninstallVariable}++");
            return package;
        }

        private static SimpleTestPackageContext ProjectMessageScriptPackage(
            string id,
            string version,
            string messageSuffix)
        {
            var package = Package(id, version);
            package.AddFile(
                "tools/install.ps1",
                $"param ($rootPath, $toolsPath, $package, $project){System.Environment.NewLine}" +
                $"$global:InstallPackageMessages += $project.Name + '{messageSuffix}'");
            package.AddFile(
                "tools/uninstall.ps1",
                $"param ($rootPath, $toolsPath, $package, $project){System.Environment.NewLine}" +
                $"$global:UninstallPackageMessages += 'Uninstall' + $project.Name + '{messageSuffix}'");
            return package;
        }

        private static void AddPackageConstraint(ProjectTestExtension project, string packageName, string versionConstraint)
        {
            var projectDirectory = Directory.Exists(project.FullPath)
                ? project.FullPath
                : Path.GetDirectoryName(project.FullPath)!;
            string packagesConfigPath = Path.Combine(projectDirectory, "packages.config");
            var document = XDocument.Load(packagesConfigPath);
            var updated = false;
            foreach (var package in document.Root!.Elements("package"))
            {
                if ((string?)package.Attribute("id") == packageName)
                {
                    package.SetAttributeValue("allowedVersions", versionConstraint);
                    updated = true;
                    break;
                }
            }

            if (!updated)
            {
                throw new System.InvalidOperationException($"Package '{packageName}' was not found in '{packagesConfigPath}'.");
            }

            document.Save(packagesConfigPath);
        }

        private static string GetEndToEndPackagesPath()
        {
            foreach (var startPath in new[] { Directory.GetCurrentDirectory(), typeof(NuGetConsoleTestCase).Assembly.Location })
            {
                var directory = new DirectoryInfo(File.Exists(startPath) ? Path.GetDirectoryName(startPath)! : startPath);
                while (directory != null)
                {
                    var packagesPath = Path.Combine(directory.FullName, "test", "EndToEnd", "Packages");
                    if (Directory.Exists(packagesPath))
                    {
                        return packagesPath;
                    }

                    directory = directory.Parent;
                }
            }

            throw new System.InvalidOperationException("Unable to locate test\\EndToEnd\\Packages.");
        }

        private static Task CreatePackagesAsync(string source, params SimpleTestPackageContext[] packages)
        {
            return SimpleTestPackageUtility.CreatePackagesWithoutDependenciesAsync(source, packages);
        }

        private static void Install(
            NuGetConsoleTestExtension console,
            ProjectTestExtension project,
            string packageName,
            string version,
            string source)
        {
            console.Execute(
                $"Install-Package {packageName} -ProjectName {project.Name} -Version {version} -Source '{source}'");
        }

        private static void Update(
            NuGetConsoleTestExtension console,
            ProjectTestExtension project,
            string packageName,
            string source,
            string arguments = "")
        {
            console.Execute(
                $"Update-Package {packageName} -ProjectName {project.Name} -Source '{source}' {arguments}");
        }

        private void AssertInstalled(ProjectTestExtension project, string packageName, string version)
        {
            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, project, packageName, version, Logger);
        }

        private void AssertNotInstalled(ProjectTestExtension project, string packageName, string version)
        {
            CommonUtility.AssertPackageNotInPackagesConfig(VisualStudio, project, packageName, version, Logger);
        }

        private void AssertNotInstalled(ProjectTestExtension project, string packageName)
        {
            CommonUtility.AssertPackageNotInPackagesConfig(VisualStudio, project, packageName, Logger);
        }

        private static void AssertSolutionPackage(
            SimpleTestPathContext pathContext,
            string packageName,
            string version,
            bool exists)
        {
            string packagePath = Path.Combine(pathContext.PackagesV2, $"{packageName}.{version}");
            if (exists)
            {
                CommonUtility.WaitForDirectoryExists(packagePath);
            }
            else
            {
                CommonUtility.WaitForDirectoryNotExists(packagePath);
            }
        }

        private static void AssertSolutionPackage(
            ApexTestContext testContext,
            string packageName,
            string version,
            bool exists)
        {
            string packagePath = Path.Combine(testContext.SolutionRoot, "packages", $"{packageName}.{version}");
            if (exists)
            {
                CommonUtility.WaitForDirectoryExists(packagePath);
            }
            else
            {
                CommonUtility.WaitForDirectoryNotExists(packagePath);
            }
        }

        private static void AssertNoErrors(NuGetConsoleTestExtension console)
        {
            string output = console.GetText();
            output.Should().NotContain("FullyQualifiedErrorId", because: output);
        }
    }
}
