// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Test.Apex.VisualStudio.Solution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet.Test.Utility;

namespace NuGet.Tests.Apex
{
    [TestClass]
    public class GetPackageTestCase : SharedVisualStudioHostTestClass
    {
        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task GetPackageFromPMC_ListsInstalledPackagesAsync()
        {
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ConsoleApplication, Logger);

            var packageName1 = "TestPackageA";
            var packageVersion1 = "1.0.0";
            var packageName2 = "TestPackageB";
            var packageVersion2 = "2.0.0";
            await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName1, packageVersion1);
            await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName2, packageVersion2);

            var nugetConsole = GetConsole(testContext.Project);

            // Assert no packages are installed initially
            nugetConsole.Clear();
            nugetConsole.Execute("Get-Package");
            string emptyText = nugetConsole.GetText();
            ParseGetPackageTableOutput(emptyText).Should().BeEmpty(because: emptyText);

            nugetConsole.InstallPackageFromPMC(packageName1, packageVersion1);
            nugetConsole.InstallPackageFromPMC(packageName2, packageVersion2);

            nugetConsole.Clear();
            nugetConsole.Execute("Get-Package");

            string pmcText = nugetConsole.GetText();
            var packages = ParseGetPackageTableOutput(pmcText);
            packages.Select(p => p.Id).Should().Contain(packageName1, because: pmcText);
            packages.Select(p => p.Id).Should().Contain(packageName2, because: pmcText);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task GetPackageFromPMCForProject_ReturnsCorrectPackagesAsync()
        {
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ConsoleApplication, Logger);

            var packageName = "TestPackage";
            var packageVersion = "1.5.0";
            await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName, packageVersion);

            var nugetConsole = GetConsole(testContext.Project);

            nugetConsole.InstallPackageFromPMC(packageName, packageVersion);

            nugetConsole.Clear();
            nugetConsole.Execute($"Get-Package -ProjectName {testContext.Project.Name}");

            string pmcText = nugetConsole.GetText();
            var packages = ParseGetPackageTableOutput(pmcText);
            packages.Select(p => p.Id).Should().Contain(packageName, because: pmcText);
            packages.Select(p => p.Versions).Should().Contain(v => v.Contains(packageVersion), because: pmcText);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task GetPackageFromPMCForProject_ReturnsEmptyForOtherProjectAsync()
        {
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ConsoleApplication, Logger);

            var project2 = testContext.SolutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, CommonUtility.DefaultTargetFramework, "TestProject2");
            testContext.SolutionService.SaveAll();

            var packageName = "TestPackage";
            var packageVersion = "1.0.0";
            await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName, packageVersion);

            var nugetConsole = GetConsole(testContext.Project);

            nugetConsole.InstallPackageFromPMC(packageName, packageVersion);

            // Get-Package for the other project which has no packages should return empty
            nugetConsole.Clear();
            nugetConsole.Execute($"Get-Package -ProjectName {project2.Name}");

            string pmcText = nugetConsole.GetText();
            var packages = ParseGetPackageTableOutput(pmcText);
            packages.Should().BeEmpty(because: pmcText);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task GetPackageFromPMCWithFilter_ReturnsMatchingPackageAsync()
        {
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ClassLibrary, Logger);

            var packageName = "TestFilterPackage";
            var packageVersion = "1.0.0";
            await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName, packageVersion);

            var nugetConsole = GetConsole(testContext.Project);

            nugetConsole.InstallPackageFromPMC(packageName, packageVersion);

            nugetConsole.Clear();
            nugetConsole.Execute("Get-Package 'TestFilter'");

            string pmcText = nugetConsole.GetText();
            var packages = ParseGetPackageTableOutput(pmcText);
            packages.Should().ContainSingle(because: pmcText)
                .Which.Id.Should().Be(packageName, because: pmcText);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task GetPackageFromPMCWithListAvailable_ReturnsAvailablePackagesAsync()
        {
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ConsoleApplication, Logger);

            var packages = new[]
            {
                ("TestPackageA", "1.0.0"),
                ("TestPackageB", "1.0.0"),
                ("TestPackageC", "1.0.0"),
                ("TestPackageD", "1.0.0"),
                ("TestPackageE", "1.0.0"),
            };

            foreach (var (name, version) in packages)
            {
                await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, name, version);
            }

            var nugetConsole = GetConsole(testContext.Project);
            nugetConsole.Clear();

            nugetConsole.Execute($"Get-Package -ListAvailable -Source {testContext.PackageSource}");

            string pmcText = nugetConsole.GetText();
            var availablePackages = ParseGetPackageTableOutput(pmcText);
            availablePackages.Should().HaveCount(packages.Length, because: pmcText);
            foreach (var (name, _) in packages)
            {
                availablePackages.Select(p => p.Id).Should().Contain(name, because: pmcText);
            }
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task GetPackageFromPMCWithPathSource_ReturnsAvailablePackagesAsync()
        {
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ConsoleApplication, Logger);

            var packageName = "PathSourcePackage";
            var packageVersion = "1.0.0";
            await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName, packageVersion);

            var nugetConsole = GetConsole(testContext.Project);
            nugetConsole.Clear();

            DirectoryInfo? parentDirectory = Directory.GetParent(testContext.PackageSource);
            Assert.IsNotNull(parentDirectory, "Package source path must have a parent directory for this test scenario.");
            string escapedParentDirectory = EscapePowerShellSingleQuotedString(parentDirectory.FullName);
            string escapedSourceLeafName = EscapePowerShellSingleQuotedString(Path.GetFileName(testContext.PackageSource));
            string escapedPackageName = EscapePowerShellSingleQuotedString(packageName);
            nugetConsole.Execute($"Set-Location '{escapedParentDirectory}'");
            nugetConsole.Execute($"Get-Package -ListAvailable -Source '{escapedSourceLeafName}' -Filter '{escapedPackageName}'");

            string pmcText = nugetConsole.GetText();
            ParseGetPackageTableOutput(pmcText).Should().ContainSingle(p => p.Id == packageName, because: pmcText);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task GetPackageFromPMCWithListAvailableFilter_FindsReleaseNotesPackageAsync()
        {
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ConsoleApplication, Logger);

            var packageName = "ReleaseNotesPackage";
            await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName, "1.0.0");

            var nugetConsole = GetConsole(testContext.Project);
            nugetConsole.Clear();

            string escapedSource = EscapePowerShellSingleQuotedString(testContext.PackageSource);
            nugetConsole.Execute($"Get-Package -ListAvailable -Source '{escapedSource}' -Filter '{packageName}'");

            string pmcText = nugetConsole.GetText();
            ParseGetPackageTableOutput(pmcText).Should().ContainSingle(p => p.Id == packageName, because: pmcText);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task GetPackageFromPMCWithoutPrereleaseSwitch_HidesPrereleaseVersionsAsync()
        {
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ConsoleApplication, Logger);

            var packageName = "PreReleaseListPackage";
            await CreatePrereleaseTestPackageSetAsync(testContext, packageName);

            var nugetConsole = GetConsole(testContext.Project);
            nugetConsole.Clear();

            string escapedSource = EscapePowerShellSingleQuotedString(testContext.PackageSource);
            nugetConsole.Execute($"Get-Package -ListAvailable -Source '{escapedSource}' -Filter '{packageName}'");

            string pmcText = nugetConsole.GetText();
            var packageEntry = ParseGetPackageTableOutput(pmcText).Single(p => p.Id == packageName);
            packageEntry.Versions.Should().Contain("1.0.0", because: pmcText);
            packageEntry.Versions.Should().NotContain("1.0.1-a", because: pmcText);
            packageEntry.Versions.Should().NotContain("1.0.0-a", because: pmcText);
            packageEntry.Versions.Should().NotContain("1.0.0-b", because: pmcText);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task GetPackageFromPMCWithAllVersionsWithoutPrereleaseSwitch_HidesPrereleaseVersionsAsync()
        {
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ConsoleApplication, Logger);

            var packageName = "PreReleaseAllVersionsPackage";
            await CreatePrereleaseTestPackageSetAsync(testContext, packageName);

            var nugetConsole = GetConsole(testContext.Project);
            nugetConsole.Clear();

            string escapedSource = EscapePowerShellSingleQuotedString(testContext.PackageSource);
            nugetConsole.Execute($"Get-Package -ListAvailable -AllVersions -Source '{escapedSource}' -Filter '{packageName}'");

            string pmcText = nugetConsole.GetText();
            var packageEntry = ParseGetPackageTableOutput(pmcText).Single(p => p.Id == packageName);
            packageEntry.Versions.Should().Contain("1.0.0", because: pmcText);
            packageEntry.Versions.Should().NotContain("1.0.1-a", because: pmcText);
            packageEntry.Versions.Should().NotContain("1.0.0-a", because: pmcText);
            packageEntry.Versions.Should().NotContain("1.0.0-b", because: pmcText);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task GetPackageFromPMCWithPrereleaseSwitch_ShowsPrereleaseVersionAsync()
        {
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ConsoleApplication, Logger);

            var packageName = "PreReleaseEnabledPackage";
            await CreatePrereleaseTestPackageSetAsync(testContext, packageName);

            var nugetConsole = GetConsole(testContext.Project);
            nugetConsole.Clear();

            string escapedSource = EscapePowerShellSingleQuotedString(testContext.PackageSource);
            nugetConsole.Execute($"Get-Package -ListAvailable -Prerelease -Source '{escapedSource}' -Filter '{packageName}'");

            string pmcText = nugetConsole.GetText();
            var packageEntry = ParseGetPackageTableOutput(pmcText).Single(p => p.Id == packageName);
            packageEntry.Versions.Should().Contain("1.0.1-a", because: pmcText);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task GetPackageFromPMCWithAllVersionsAndPrereleaseSwitch_ShowsAllVersionsAsync()
        {
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ConsoleApplication, Logger);

            var packageName = "PreReleaseAllWithFlagPackage";
            await CreatePrereleaseTestPackageSetAsync(testContext, packageName);

            var nugetConsole = GetConsole(testContext.Project);
            nugetConsole.Clear();

            string escapedSource = EscapePowerShellSingleQuotedString(testContext.PackageSource);
            nugetConsole.Execute($"Get-Package -ListAvailable -AllVersions -Prerelease -Source '{escapedSource}' -Filter '{packageName}'");

            string pmcText = nugetConsole.GetText();
            var packageEntry = ParseGetPackageTableOutput(pmcText).Single(p => p.Id == packageName);
            packageEntry.Versions.Should().Contain("1.0.1-a", because: pmcText);
            packageEntry.Versions.Should().Contain("1.0.0", because: pmcText);
            packageEntry.Versions.Should().Contain("1.0.0-a", because: pmcText);
            packageEntry.Versions.Should().Contain("1.0.0-b", because: pmcText);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task GetPackageUpdatesFromPMCWithoutPrereleaseSwitch_DoesNotReturnPrereleaseUpdateAsync()
        {
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ClassLibrary, Logger);

            var packageName = "PreReleaseUpdatePackageNoFlag";
            await CreatePrereleaseTestPackageSetAsync(testContext, packageName);

            var nugetConsole = GetConsole(testContext.Project);
            nugetConsole.InstallPackageFromPMC(packageName, "1.0.0-b");

            nugetConsole.Clear();
            string escapedSource = EscapePowerShellSingleQuotedString(testContext.PackageSource);
            nugetConsole.Execute($"Get-Package -Updates -Source '{escapedSource}'");

            string pmcText = nugetConsole.GetText();
            var packageEntry = ParseGetPackageTableOutput(pmcText).Single(p => p.Id == packageName);
            packageEntry.Versions.Should().Contain("1.0.0", because: pmcText);
            packageEntry.Versions.Should().NotContain("1.0.1-a", because: pmcText);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task GetPackageUpdatesFromPMCWithPrereleaseSwitch_ReturnsPrereleaseUpdateAsync()
        {
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ClassLibrary, Logger);

            var packageName = "PreReleaseUpdatePackageWithFlag";
            await CreatePrereleaseTestPackageSetAsync(testContext, packageName);

            var nugetConsole = GetConsole(testContext.Project);
            nugetConsole.InstallPackageFromPMC(packageName, "1.0.0-a");

            nugetConsole.Clear();
            string escapedSource = EscapePowerShellSingleQuotedString(testContext.PackageSource);
            nugetConsole.Execute($"Get-Package -Updates -Prerelease -Source '{escapedSource}'");

            string pmcText = nugetConsole.GetText();
            var packageEntry = ParseGetPackageTableOutput(pmcText).Single(p => p.Id == packageName);
            packageEntry.Versions.Should().Contain("1.0.1-a", because: pmcText);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task GetPackageUpdatesFromPMCWithAllVersionsSwitch_ReturnsStableVersionAsync()
        {
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ClassLibrary, Logger);

            var packageName = "PreReleaseUpdatePackageAllVersions";
            await CreatePrereleaseTestPackageSetAsync(testContext, packageName);

            var nugetConsole = GetConsole(testContext.Project);
            nugetConsole.InstallPackageFromPMC(packageName, "1.0.0-a");

            nugetConsole.Clear();
            string escapedSource = EscapePowerShellSingleQuotedString(testContext.PackageSource);
            nugetConsole.Execute($"Get-Package -Updates -AllVersions -Source '{escapedSource}'");

            string pmcText = nugetConsole.GetText();
            var packageEntry = ParseGetPackageTableOutput(pmcText).Single(p => p.Id == packageName);
            packageEntry.Versions.Should().Contain("1.0.0", because: pmcText);
            packageEntry.Versions.Should().NotContain("1.0.1-a", because: pmcText);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task GetPackageUpdatesFromPMCWithAllVersionsAndPrereleaseSwitch_ReturnsAllEligibleUpdatesAsync()
        {
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ClassLibrary, Logger);

            var packageName = "PreReleaseUpdatePackageAllFlags";
            await CreatePrereleaseTestPackageSetAsync(testContext, packageName);

            var nugetConsole = GetConsole(testContext.Project);
            nugetConsole.InstallPackageFromPMC(packageName, "1.0.0-b");

            nugetConsole.Clear();
            string escapedSource = EscapePowerShellSingleQuotedString(testContext.PackageSource);
            nugetConsole.Execute($"Get-Package -Updates -AllVersions -Prerelease -Source '{escapedSource}'");

            string pmcText = nugetConsole.GetText();
            var packageEntry = ParseGetPackageTableOutput(pmcText).Single(p => p.Id == packageName);
            packageEntry.Versions.Should().Contain("1.0.1-a", because: pmcText);
            packageEntry.Versions.Should().Contain("1.0.0", because: pmcText);
        }

        private static NuGetConsoleTestExtension GetConsole(ProjectTestExtension project)
        {
            var nugetConsole = project.GetComponentModelService<NuGetApexTestService>().NuGetConsole;
            nugetConsole.WaitForInitialize();
            return nugetConsole;
        }

        /// <summary>
        /// Holds a single row from the Get-Package PMC table output.
        /// </summary>
        private sealed class PmcPackageEntry
        {
            public string Id { get; }
            public string Versions { get; }
            public string ProjectName { get; }

            public PmcPackageEntry(string id, string versions, string projectName)
            {
                Id = id;
                Versions = versions;
                ProjectName = projectName;
            }
        }

        /// <summary>
        /// Parses the tabular output of a Get-Package PMC command into a list of package entries.
        /// The expected output format is a fixed-width table with a separator row of dashes:
        /// <code>
        /// Id                 Versions    ProjectName
        /// --                 --------    -----------
        /// TestPackageA       {1.0.0}     MyProject
        /// TestPackageB       {2.0.0}     MyProject
        /// </code>
        /// Parsing begins after the separator row and stops at the first empty line.
        /// Column boundaries are inferred from the positions of the dash groups in the separator row.
        /// </summary>
        private static List<PmcPackageEntry> ParseGetPackageTableOutput(string pmcText)
        {
            var entries = new List<PmcPackageEntry>();
            bool pastSeparator = false;
            int[]? columnStarts = null;

            foreach (string rawLine in pmcText.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');

                if (!pastSeparator)
                {
                    // The separator row contains only dashes and spaces (e.g. "--  --------  -----------")
                    string stripped = line.Replace("-", "").Replace(" ", "");
                    if (stripped.Length == 0 && line.Contains("--"))
                    {
                        columnStarts = FindColumnStarts(line);
                        pastSeparator = true;
                    }
                    continue;
                }

                if (line.Trim().Length == 0)
                {
                    break; // End of table
                }

                if (columnStarts != null)
                {
                    string id = ExtractColumn(line, columnStarts, 0).Trim();
                    string versions = columnStarts.Length >= 2 ? ExtractColumn(line, columnStarts, 1).Trim() : string.Empty;
                    string projectName = columnStarts.Length >= 3 ? ExtractColumn(line, columnStarts, 2).Trim() : string.Empty;

                    if (id.Length > 0)
                    {
                        entries.Add(new PmcPackageEntry(id, versions, projectName));
                    }
                }
            }

            return entries;
        }

        /// <summary>
        /// Returns the start index of each group of dashes in the separator row,
        /// giving the column start positions for the fixed-width table.
        /// </summary>
        private static int[] FindColumnStarts(string separatorLine)
        {
            var starts = new List<int>();
            bool inDashes = false;

            for (int i = 0; i < separatorLine.Length; i++)
            {
                if (separatorLine[i] == '-' && !inDashes)
                {
                    starts.Add(i);
                    inDashes = true;
                }
                else if (separatorLine[i] != '-')
                {
                    inDashes = false;
                }
            }

            return starts.ToArray();
        }

        /// <summary>
        /// Extracts the text for a specific column from a data row, using the column-start
        /// positions obtained from the separator row.
        /// </summary>
        private static string ExtractColumn(string line, int[] columnStarts, int columnIndex)
        {
            int start = columnStarts[columnIndex];
            if (start >= line.Length)
            {
                return string.Empty;
            }

            if (columnIndex + 1 < columnStarts.Length)
            {
                int end = columnStarts[columnIndex + 1];
                if (end > line.Length)
                {
                    end = line.Length;
                }
                return line.Substring(start, end - start);
            }

            return line.Substring(start);
        }

        /// <summary>
        /// Creates a stable and prerelease package set used by Get-Package list and update scenarios.
        /// Versions created: 1.0.0-a, 1.0.0-b, 1.0.0, 1.0.1-a.
        /// </summary>
        private static async Task CreatePrereleaseTestPackageSetAsync(ApexTestContext testContext, string packageName)
        {
            await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName, "1.0.0-a");
            await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName, "1.0.0-b");
            await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName, "1.0.0");
            await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName, "1.0.1-a");
        }

        /// <summary>
        /// Escapes single quotes for inclusion in PowerShell single-quoted strings.
        /// </summary>
        private static string EscapePowerShellSingleQuotedString(string value)
        {
            return value.Replace("'", "''");
        }
    }
}
