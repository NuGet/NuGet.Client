// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Packaging;
using NuGet.Test.Utility;
using NuGet.XPlat.FuncTest;
using Xunit;
using Strings = NuGet.CommandLine.XPlat.Strings;

namespace Dotnet.Integration.Test
{
    [Collection("Dotnet Integration Tests")]
    public class DotnetListPackageTests
    {
        private static readonly string ProjectName = "test_project_listpkg";

        private readonly MsbuildIntegrationTestFixture _fixture;

        public DotnetListPackageTests(MsbuildIntegrationTestFixture fixture)
        {
            _fixture = fixture;
        }

        [PlatformFact(Platform.Windows)]
        public async Task DotnetListPackage_Succeed()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = XPlatTestUtils.CreateProject(ProjectName, pathContext, "net46");

                var packageX = XPlatTestUtils.CreatePackage();

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var addResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"add {projectA.ProjectPath} package packageX --version 1.0.0 --no-restore");
                Assert.True(addResult.Success);

                var restoreResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"restore {projectA.ProjectName}.csproj");
                Assert.True(restoreResult.Success);

                var listResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"list {projectA.ProjectPath} package");

                Assert.True(ContainsIgnoringSpaces(listResult.AllOutput, "packageX1.0.01.0.0"));
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task DotnetListPackage_NoRestore_Fail()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = XPlatTestUtils.CreateProject(ProjectName, pathContext, "net46");

                var packageX = XPlatTestUtils.CreatePackage();

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);


                var addResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"add {projectA.ProjectPath} package packageX --no-restore");
                Assert.True(addResult.Success);

                var listResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"list {projectA.ProjectPath} package");

                Assert.True(ContainsIgnoringSpaces(listResult.AllOutput, "No assets file was found".Replace(" ", "")));
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task DotnetListPackage_Transitive()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = XPlatTestUtils.CreateProject(ProjectName, pathContext, "net46");

                var packageX = XPlatTestUtils.CreatePackage();
                var packageY = XPlatTestUtils.CreatePackage(packageId: "packageY");
                packageX.Dependencies.Add(packageY);

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageY);


                var addResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"add {projectA.ProjectPath} package packageX --no-restore");
                Assert.True(addResult.Success);

                var restoreResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"restore {projectA.ProjectName}.csproj");
                Assert.True(restoreResult.Success);

                var listResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"list {projectA.ProjectPath} package");

                Assert.False(ContainsIgnoringSpaces(listResult.AllOutput, "packageY"));

                listResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"list {projectA.ProjectPath} package --include-transitive");

                Assert.True(ContainsIgnoringSpaces(listResult.AllOutput, "packageY"));

            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("", "net46", null)]
        [InlineData("", "net451", null)]
        [InlineData("--framework net451 --framework net46", "net46", null)]
        [InlineData("--framework net451 --framework net46", "net451", null)]
        [InlineData("--framework net451", "net451", "net46")]
        [InlineData("--framework net46", "net46", "net451")]
        public async Task DotnetListPackage_FrameworkSpecific_Success(string args, string shouldInclude, string shouldntInclude)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = XPlatTestUtils.CreateProject(ProjectName, pathContext, "net46;net451");

                var packageX = XPlatTestUtils.CreatePackage(frameworkString: "net46;net451");

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);


                var addResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"add {projectA.ProjectPath} package packageX --no-restore");
                Assert.True(addResult.Success);

                var restoreResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"restore {projectA.ProjectName}.csproj");
                Assert.True(restoreResult.Success);

                var listResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"list {projectA.ProjectPath} package {args}");

                Assert.True(ContainsIgnoringSpaces(listResult.AllOutput, shouldInclude.Replace(" ", "")));

                if (shouldntInclude != null)
                {
                    Assert.False(ContainsIgnoringSpaces(listResult.AllOutput, shouldntInclude.Replace(" ", "")));
                }

            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task DotnetListPackage_InvalidFramework_Fail()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = XPlatTestUtils.CreateProject(ProjectName, pathContext, "net46");

                var packageX = XPlatTestUtils.CreatePackage();

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);


                var addResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"add {projectA.ProjectPath} package packageX --no-restore");
                Assert.True(addResult.Success);

                var restoreResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"restore {projectA.ProjectName}.csproj");
                Assert.True(restoreResult.Success);

                var listResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"list {projectA.ProjectPath} package --framework net46 --framework invalidFramework",
                    true);

                Assert.False(listResult.Success);

            }
        }

        [PlatformFact(Platform.Windows)]
        public void DotnetListPackage_DeprecatedAndOutdated_Fail()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = XPlatTestUtils.CreateProject(ProjectName, pathContext, "net46");

                var listResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"list {projectA.ProjectPath} package --deprecated --outdated",
                    true);

                Assert.False(listResult.Success);
                Assert.Contains(Strings.ListPkg_InvalidOptionsOutdatedAndDeprecated, listResult.Errors);
            }
        }

        // In 2.2.100 of CLI. DotNet list package would show a section for each TFM and for each TFM/RID.
        // This is testing to ensure that it only shows one section for each TFM.
        [PlatformFact(Platform.Windows)]
        public async Task DotnetListPackage_ShowFrameworksOnly_SDK()
        {
            using (var pathContext = new SimpleTestPathContext())
            {

                var projectA = XPlatTestUtils.CreateProject(ProjectName, pathContext, "net461");

                projectA.Properties.Add("RuntimeIdentifiers", "win;win-x86;win-x64");

                var packageX = XPlatTestUtils.CreatePackage();

                projectA.AddPackageToAllFrameworks(packageX);

                projectA.Save();

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var addResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"add {projectA.ProjectPath} package packageX --no-restore");
                Assert.True(addResult.Success);

                var restoreResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"restore {projectA.ProjectName}.csproj");
                Assert.True(restoreResult.Success);

                //the assets file should generate 4 sections in Targets: 1 for TFM only , and 3 for TFM + RID combinations 
                var assetsFile = projectA.AssetsFile;
                Assert.Equal(4, assetsFile.Targets.Count);

                var listResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"list {projectA.ProjectPath} package",
                    true);

                //make sure there is no duplicate in output
                Assert.True(NoDuplicateSection(listResult.AllOutput), listResult.AllOutput);

            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("1.0.0", "", "2.1.0")]
        [InlineData("1.0.0", "--include-prerelease", "2.2.0-beta")]
        [InlineData("1.0.0-beta", "", "2.2.0-beta")]
        [InlineData("1.0.0", "--highest-patch", "1.0.9")]
        [InlineData("1.0.0", "--highest-minor", "1.9.0")]
        [InlineData("1.0.0", "--highest-patch --include-prerelease", "1.0.10-beta")]
        [InlineData("1.0.0", "--highest-minor --include-prerelease", "1.10.0-beta")]
        public async Task DotnetListPackage_Outdated_Succeed(string currentVersion, string args, string expectedVersion)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = XPlatTestUtils.CreateProject(ProjectName, pathContext, "net472");
                var versions = new List<string> { "1.0.0-beta", "1.0.0", "1.0.9", "1.0.10-beta", "1.9.0", "1.10.0-beta", "2.1.0", "2.2.0-beta" };
                foreach (var version in versions)
                {
                    var packageX = XPlatTestUtils.CreatePackage(packageId: "packageX", packageVersion: version);

                    // Generate Package
                    await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                        pathContext.PackageSource,
                        PackageSaveMode.Defaultv3,
                        packageX);
                }

                var addResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"add {projectA.ProjectPath} package packageX --version {currentVersion} --no-restore");
                Assert.True(addResult.Success);

                var restoreResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"restore {projectA.ProjectName}.csproj");
                Assert.True(restoreResult.Success);

                var listResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"list {projectA.ProjectPath} package --outdated {args}");

                Assert.True(ContainsIgnoringSpaces(listResult.AllOutput, $"packageX{currentVersion}{currentVersion}{expectedVersion}"));

            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void DotnetListPackage_ProjectReference_Succeeds(bool includeTransitive, bool outdated)
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = XPlatTestUtils.CreateProject("ProjectA", pathContext, "net46");
                var projectB = XPlatTestUtils.CreateProject("ProjectB", pathContext, "net46");

                var addResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"add {projectA.ProjectPath} reference {projectB.ProjectPath}");
                Assert.True(addResult.Success);

                var restoreResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"restore {projectA.ProjectName}.csproj");
                Assert.True(restoreResult.Success);

                var argsBuilder = new StringBuilder();
                if (includeTransitive)
                {
                    argsBuilder.Append(" --include-transitive");
                }
                if (outdated)
                {
                    argsBuilder.Append(" --outdated");
                }

                // Act
                var listResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"list {projectA.ProjectPath} package {argsBuilder}");

                // Assert
                if (outdated)
                {
                    Assert.Contains("The given project `ProjectA` has no updates given the current sources.", listResult.AllOutput);
                }
                else if (includeTransitive)
                {
                    Assert.Contains("ProjectB", listResult.AllOutput);
                }
                else
                {
                    Assert.Contains("No packages were found for this framework.", listResult.AllOutput);
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task DotnetListPackage_OutdatedWithNoVersionsFound_Succeeds()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = XPlatTestUtils.CreateProject("ProjectA", pathContext, "net46");
                var packageX = XPlatTestUtils.CreatePackage(packageId: "packageX", packageVersion: "1.0.0");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                        pathContext.PackageSource,
                        PackageSaveMode.Defaultv3,
                        packageX);

                var addResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"add {projectA.ProjectPath} package packageX --version 1.0.0 --no-restore");
                Assert.True(addResult.Success);

                var restoreResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"restore {projectA.ProjectName}.csproj");
                Assert.True(restoreResult.Success);

                foreach (var nupkg in Directory.EnumerateDirectories(pathContext.PackageSource))
                {
                    Directory.Delete(nupkg, recursive: true);
                }

                // Act
                var listResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"list {projectA.ProjectPath} package --outdated");

                // Assert
                var lines = listResult.AllOutput.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                Assert.True(lines.Any(l => l.Contains("packageX") && l.Contains("Not found at the sources")), "Line containing 'packageX' and 'Not found at the sources' not found: " + listResult.AllOutput);
            }
        }

        // We read the original PackageReference items by calling the CollectPackageReference target.
        // If a project has InitialTargets we need to deal with the response properly in the C# based invocation.
        [PlatformFact(Platform.Windows)]
        public async Task DotnetListPackage_ProjectWithInitialTargets_Succeeds()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = XPlatTestUtils.CreateProject(ProjectName, pathContext, "net46");

                var doc = XDocument.Parse(File.ReadAllText(projectA.ProjectPath));

                doc.Root.Add(new XAttribute("InitialTargets", "FirstTarget"));

                doc.Root.Add(new XElement(XName.Get("Target"),
                    new XAttribute(XName.Get("Name"), "FirstTarget"),
                    new XElement(XName.Get("Message"),
                        new XAttribute(XName.Get("Text"), "I am the first target invoked every time a target is called on this project!"))));

                File.WriteAllText(projectA.ProjectPath, doc.ToString());

                var packageX = XPlatTestUtils.CreatePackage();

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var addResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"add {projectA.ProjectPath} package packageX --version 1.0.0 --no-restore");
                Assert.True(addResult.Success);

                var restoreResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"restore {projectA.ProjectName}.csproj");
                Assert.True(restoreResult.Success);

                var listResult = _fixture.RunDotnet(Directory.GetParent(projectA.ProjectPath).FullName,
                    $"list {projectA.ProjectPath} package");

                Assert.True(ContainsIgnoringSpaces(listResult.AllOutput, "packageX1.0.01.0.0"));
            }
        }

        private static bool ContainsIgnoringSpaces(string output, string pattern)
        {
            var commandResultNoSpaces = output.Replace(" ", "");

            return commandResultNoSpaces.ToLowerInvariant().Contains(pattern.ToLowerInvariant());
        }

        private static bool NoDuplicateSection(string output)
        {
            var sections = output.Replace(" ", "").Replace("\r", "").Replace("\n", "").Split("[");
            if (sections.Length == 1)
            {
                return false;
            }

            for (var i = 1; i <= sections.Length - 2; i++)
            {
                for (var j = i + 1; j <= sections.Length - 1; j++)
                {
                    if (sections[i].Equals(sections[j]))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
