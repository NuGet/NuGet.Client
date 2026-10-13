// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Microsoft.VisualStudio.Shell;
using Moq;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.PackageManagement;
using NuGet.PackageManagement.PowerShellCmdlets;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using NuGet.VisualStudio;
using Test.Utility;
using Xunit;

namespace NuGetConsole.Host.PowerShell.Test
{
    [Collection(MockedVS.Collection)]
    public class PackageInitScriptCommandTests : IAsyncServiceProvider
    {
        private readonly Mock<IComponentModel> _componentModel = new Mock<IComponentModel>();
        private readonly Mock<IVsSolutionManager> _solutionManager = new Mock<IVsSolutionManager>();

        public PackageInitScriptCommandTests(GlobalServiceProvider globalServiceProvider)
        {
            globalServiceProvider.Reset();
            _solutionManager.SetupGet(manager => manager.IsSolutionOpen).Returns(true);
            _solutionManager.Setup(manager => manager.IsSolutionAvailableAsync()).ReturnsAsync(true);
            _componentModel.Setup(model => model.GetService<IVsSolutionManager>()).Returns(_solutionManager.Object);
            _componentModel.Setup(model => model.GetService<ISourceControlManagerProvider>()).Returns(Mock.Of<ISourceControlManagerProvider>());
            _componentModel.Setup(model => model.GetService<ICommonOperations>()).Returns(Mock.Of<ICommonOperations>());
            _componentModel.Setup(model => model.GetService<IPackageRestoreManager>()).Returns(Mock.Of<IPackageRestoreManager>());
            _componentModel.Setup(model => model.GetService<IDeleteOnRestartManager>()).Returns(Mock.Of<IDeleteOnRestartManager>());
            _componentModel.Setup(model => model.GetService<IRestoreProgressReporter>()).Returns(Mock.Of<IRestoreProgressReporter>());
            globalServiceProvider.AddService(typeof(SComponentModel), _componentModel.Object);
            ServiceLocator.InitializePackageServiceProvider(this);
        }

        [Fact]
        public async Task GetPackageInitScript_ReturnsOrderedUniqueScriptsWithoutExecutingAsync()
        {
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);

            var results = fixture.InvokeScript("Get-PackageInitScript");

            fixture.Errors.Should().BeEmpty();
            var scripts = results.Select(result => (PowerShellPackageInitScript)result.BaseObject).ToList();
            scripts.Select(script => script.Id).Should().Equal("Dependency", "Root");
            scripts.Select(script => script.Version).Should().OnlyContain(version => version == NuGetVersion.Parse("1.0.0"));
            scripts.Select(script => script.FullName).Should().Equal(
                ScriptPath(context, "Dependency"), ScriptPath(context, "Root"));
            fixture.InvokeScript("$null -eq $global:InitOrder").Single().BaseObject.Should().Be(true);
        }

        [Theory]
        [InlineData("Import-PackageInitScript")]
        [InlineData("Import-PackageInitScript (Get-PackageInitScript)")]
        [InlineData("Get-PackageInitScript | Import-PackageInitScript")]
        [InlineData("Get-PackageInitScript | Select-Object -ExpandProperty FullName | Import-PackageInitScript")]
        [InlineData("Get-PackageInitScript | Sort-Object Id -Descending | Import-PackageInitScript")]
        [InlineData("Import-PackageInitScript -Packages Root,Dependency,root")]
        [InlineData("Get-PackageInitScript | ForEach-Object { Get-Item -LiteralPath $_.FullName } | Import-PackageInitScript")]
        [InlineData("$s = @(Get-PackageInitScript); Import-PackageInitScript @($s[1],$s[0],$s[1])")]
        public async Task ImportPackageInitScript_ImportsInDependencyOrderOncePerInvocationAsync(string command)
        {
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);

            fixture.InvokeScript(command);
            fixture.Errors.Should().BeEmpty();
            fixture.InvokeScript("$global:InitOrder").Select(result => result.BaseObject).Should().Equal("Dependency", "Root");
            fixture.InvokeScript("Get-InitGreeting").Single().BaseObject.Should().Be("hello");
            fixture.InvokeScript(command);
            fixture.Errors.Should().BeEmpty();
            fixture.InvokeScript("$global:InitOrder").Select(result => result.BaseObject)
                .Should().Equal("Dependency", "Root", "Dependency", "Root");
        }

        [Theory]
        [InlineData("Import-PackageInitScript -Packages root", "Root")]
        [InlineData("Import-PackageInitScript -Packages Dependency@1.0", "Dependency")]
        [InlineData("Get-PackageInitScript -Packages Root | Import-PackageInitScript", "Root")]
        [InlineData("Import-PackageInitScript (Get-PackageInitScript -Packages Dependency).FullName", "Dependency")]
        public async Task ImportPackageInitScript_SelectsPackagesAsync(string command, string expected)
        {
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);

            fixture.InvokeScript(command);

            fixture.Errors.Should().BeEmpty();
            fixture.InvokeScript("$global:InitOrder").Single().BaseObject.Should().Be(expected);
        }

        [Theory]
        [InlineData("Import-PackageInitScript $null")]
        [InlineData("Import-PackageInitScript @()")]
        [InlineData("@() | Import-PackageInitScript")]
        [InlineData("$null | Import-PackageInitScript")]
        public async Task ImportPackageInitScript_EmptyInputDoesNothingAsync(string command)
        {
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);

            fixture.InvokeScript(command);

            fixture.Errors.Should().BeEmpty();
            fixture.InvokeScript("$null -eq $global:InitOrder").Single().BaseObject.Should().Be(true);
        }

        [Theory]
        [InlineData("tools\\install.ps1")]
        [InlineData("tools\\uninstall.ps1")]
        [InlineData("tools\\net45\\init.ps1")]
        [InlineData("tools\\init.ps1.bak")]
        [InlineData("other\\init.ps1")]
        public async Task ImportPackageInitScript_RejectsOtherFilesAsync(string relativePath)
        {
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);
            var path = Path.Combine(context.PackagesV2, "Root.1.0.0", relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "$global:UnexpectedScript = $true");

            fixture.InvokeScript($"Import-PackageInitScript '{Escape(path)}'");

            fixture.Errors.Should().ContainSingle().Which.FullyQualifiedErrorId.Should().StartWith("NuGetInvalidPackageInitScript");
            fixture.InvokeScript("($null -eq $global:InitOrder) -and ($null -eq $global:UnexpectedScript)")
                .Single().BaseObject.Should().Be(true);
        }

        [Fact]
        public async Task ImportPackageInitScript_RejectsUninstalledInitScriptAsync()
        {
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);
            var path = Path.Combine(context.WorkingDirectory, "tools", "init.ps1");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "throw 'must not run'");

            fixture.InvokeScript($"Import-PackageInitScript '{Escape(path)}'");

            fixture.Errors.Should().ContainSingle().Which.FullyQualifiedErrorId.Should().StartWith("NuGetInvalidPackageInitScript");
        }

        [Theory]
        [InlineData("Missing")]
        [InlineData("Root@2.0.0")]
        [InlineData("Root@invalid")]
        [InlineData("@1.0.0")]
        public async Task ImportPackageInitScript_InvalidPackageProducesErrorAsync(string selector)
        {
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);

            fixture.InvokeScript($"Import-PackageInitScript -Packages '{selector}'");

            fixture.Errors.Should().ContainSingle();
            fixture.InvokeScript("$null -eq $global:InitOrder").Single().BaseObject.Should().Be(true);
        }

        [Fact]
        public async Task ImportPackageInitScript_ParameterSetsAreExclusiveAsync()
        {
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);

            Action invoke = () => fixture.InvokeScript(
                $"Import-PackageInitScript '{Escape(ScriptPath(context, "Root"))}' -Packages Root");

            invoke.Should().Throw<ParameterBindingException>();
            fixture.InvokeScript("$null -eq $global:InitOrder").Single().BaseObject.Should().Be(true);
        }

        [Fact]
        public async Task ImportPackageInitScript_ReportsScriptFailureAsync()
        {
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            File.WriteAllText(ScriptPath(context, "Root"), "throw 'init script failed'");
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);

            Action invoke = () => fixture.InvokeScript("Import-PackageInitScript -Packages Root");

            invoke.Should().Throw<RuntimeException>().WithMessage("*init script failed*");
            fixture.InvokeScript("Test-Path variable:__pc_args").Single().BaseObject.Should().Be(false);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(4)]
        public async Task GetPackageInitScript_PackageReferenceUsesGlobalAndFallbackFoldersAsync(int assetsFileVersion)
        {
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            var root = new SimpleTestPackageContext("Root", "1.0.0");
            root.AddFile("tools/init.ps1", "$global:InitOrder += @('Root')");
            var dependency = new SimpleTestPackageContext("Dependency", "1.0.0");
            dependency.AddFile("tools/init.ps1", "$global:InitOrder += @('Dependency')");
            await SimpleTestPackageUtility.CreateFolderFeedV3Async(context.UserPackagesFolder, PackageSaveMode.Defaultv3, root);
            await SimpleTestPackageUtility.CreateFolderFeedV3Async(context.FallbackFolder, PackageSaveMode.Defaultv3, dependency);
            var lockFile = new LockFile
            {
                Version = assetsFileVersion,
                PackageSpec = new PackageSpec(new[]
                {
                    new TargetFrameworkInformation
                    {
                        FrameworkName = NuGetFramework.Parse("net10.0"),
                        TargetAlias = "net10.0"
                    }
                }),
                PackageFolders = new[]
                {
                    new LockFileItem(context.UserPackagesFolder),
                    new LockFileItem(context.FallbackFolder)
                },
                Libraries = new[]
                {
                    new LockFileLibrary { Name = root.Id, Version = NuGetVersion.Parse(root.Version), Type = LibraryType.Package },
                    new LockFileLibrary { Name = dependency.Id, Version = NuGetVersion.Parse(dependency.Version), Type = LibraryType.Package }
                },
                Targets = new[]
                {
                    new LockFileTarget
                    {
                        TargetFramework = NuGetFramework.Parse("net10.0"),
                        TargetAlias = "net10.0",
                        Name = "net10.0",
                        Libraries = new[]
                        {
                            new LockFileTargetLibrary
                            {
                                Name = root.Id, Version = NuGetVersion.Parse(root.Version), Type = LibraryType.Package,
                                Dependencies = new[] { new PackageDependency(dependency.Id, VersionRange.Parse("1.0.0")) }
                            },
                            new LockFileTargetLibrary { Name = dependency.Id, Version = NuGetVersion.Parse(dependency.Version), Type = LibraryType.Package }
                        }
                    }
                }
            };
            var assetsFile = Path.Combine(context.SolutionRoot, "project.assets.json");
            new LockFileFormat().Write(assetsFile, lockFile);
            var logger = new TestLogger();
            new LockFileFormat().Read(assetsFile, logger).Libraries.Should().HaveCount(2, logger.ShowErrors());
            var project = new Mock<BuildIntegratedNuGetProject>();
            project.Setup(project => project.GetAssetsFilePathOrNullAsync()).ReturnsAsync(assetsFile);
            _solutionManager.Setup(manager => manager.GetNuGetProjectsAsync()).ReturnsAsync(new[] { project.Object });
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);

            var scripts = fixture.InvokeScript("Get-PackageInitScript")
                .Select(result => (PowerShellPackageInitScript)result.BaseObject).ToList();

            fixture.Errors.Should().BeEmpty();
            project.Verify(project => project.GetAssetsFilePathOrNullAsync(), Times.Once);
            File.Exists(Path.Combine(context.UserPackagesFolder, "root", "1.0.0", "tools", "init.ps1")).Should().BeTrue();
            File.Exists(Path.Combine(context.FallbackFolder, "dependency", "1.0.0", "tools", "init.ps1")).Should().BeTrue();
            scripts.Select(script => script.FullName).Should().Equal(
                Path.Combine(context.FallbackFolder, "dependency", "1.0.0", "tools", "init.ps1"),
                Path.Combine(context.UserPackagesFolder, "root", "1.0.0", "tools", "init.ps1"));
            fixture.InvokeScript("Get-PackageInitScript | Import-PackageInitScript");
            fixture.Errors.Should().BeEmpty();
            fixture.InvokeScript("$global:InitOrder").Select(result => result.BaseObject).Should().Equal("Dependency", "Root");
        }

        [Fact]
        public async Task ImportPackageInitScript_AmbiguousVersionsRequireVersionSelectorAsync()
        {
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            var first = new SimpleTestPackageContext("Root", "1.0.0");
            var second = new SimpleTestPackageContext("Root", "2.0.0");
            second.AddFile("tools/init.ps1", "$global:ImportedVersion = '2.0.0'");
            await SimpleTestPackageUtility.CreateFolderFeedPackagesConfigAsync(context.PackagesV2, second);
            SetInstalledPackages(first, second, new SimpleTestPackageContext("Dependency", "1.0.0"));
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);

            fixture.InvokeScript("Import-PackageInitScript -Packages Root");

            fixture.Errors.Should().ContainSingle().Which.FullyQualifiedErrorId.Should().StartWith("NuGetAmbiguousPackageInitScript");
            fixture.InvokeScript("$null -eq $global:InitOrder").Single().BaseObject.Should().Be(true);
            fixture.InvokeScript("Import-PackageInitScript -Packages Root@2.0.0");
            fixture.Errors.Should().BeEmpty();
            fixture.InvokeScript("$global:ImportedVersion").Single().BaseObject.Should().Be("2.0.0");
        }

        [Fact]
        public async Task ImportPackageInitScript_RelativePathsResolveFromPowerShellLocationAsync()
        {
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);

            fixture.InvokeScript($"Set-Location -LiteralPath '{Escape(context.PackagesV2)}'; Import-PackageInitScript '.\\Root.1.0.0\\tools\\init.ps1'");

            fixture.Errors.Should().BeEmpty();
            fixture.InvokeScript("$global:InitOrder").Single().BaseObject.Should().Be("Root");
        }

        [Fact]
        public async Task ImportPackageInitScript_ErrorActionStopPreventsExecutionAsync()
        {
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);

            Action invoke = () => fixture.InvokeScript(
                "@(Get-PackageInitScript; 'not-an-installed-script') | Import-PackageInitScript -ErrorAction Stop");

            invoke.Should().Throw<RuntimeException>();
            fixture.InvokeScript("$null -eq $global:InitOrder").Single().BaseObject.Should().Be(true);
        }

        [Fact]
        public async Task ImportPackageInitScript_DoesNotAutomaticallyImportNewlyInstalledScriptsAsync()
        {
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);
            fixture.InvokeScript("Import-PackageInitScript -Packages Root");
            fixture.Errors.Should().BeEmpty();
            var later = new SimpleTestPackageContext("Later", "1.0.0");
            later.AddFile("tools/init.ps1", "$global:InitOrder += @('Later')");
            await SimpleTestPackageUtility.CreateFolderFeedPackagesConfigAsync(context.PackagesV2, later);
            SetInstalledPackages(later);

            fixture.InvokeScript("Get-PackageInitScript");

            fixture.InvokeScript("$global:InitOrder").Single().BaseObject.Should().Be("Root");
            fixture.InvokeScript("Import-PackageInitScript -Packages Later");
            fixture.Errors.Should().BeEmpty();
            fixture.InvokeScript("$global:InitOrder").Select(result => result.BaseObject).Should().Equal("Root", "Later");
        }

        [Theory]
        [InlineData("Get-PackageInitScript")]
        [InlineData("Import-PackageInitScript")]
        public async Task PackageInitScript_NoInstalledScriptsIsNoOpAsync(string command)
        {
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            SetInstalledPackages();
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);

            fixture.InvokeScript(command).Should().BeEmpty();

            fixture.Errors.Should().BeEmpty();
        }

        [Fact]
        public async Task ImportPackageInitScript_ReceivesLegacyArgumentsAndReturnsOutputAsync()
        {
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            File.WriteAllText(ScriptPath(context, "Root"),
                "param($installPath, $toolsPath, $package, $project) $installPath; $toolsPath; $package.Id; $package.Version; $null -eq $project");
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);

            var results = fixture.InvokeScript("Import-PackageInitScript -Packages Root");

            fixture.Errors.Should().BeEmpty();
            results.Select(result => result.BaseObject).Should().Equal(
                Path.GetDirectoryName(Path.GetDirectoryName(ScriptPath(context, "Root"))!)!,
                Path.GetDirectoryName(ScriptPath(context, "Root"))!,
                "Root", "1.0.0", true);
        }

        [Fact]
        public async Task GetPackageInitScript_NoSolutionProducesErrorAsync()
        {
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            _solutionManager.SetupGet(manager => manager.IsSolutionOpen).Returns(false);
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);

            Action invoke = () => fixture.InvokeScript("Get-PackageInitScript");

            invoke.Should().Throw<RuntimeException>();
        }

        private async Task SetupPackagesAsync(SimpleTestPathContext context)
        {
            var dependency = new SimpleTestPackageContext("Dependency", "1.0.0");
            dependency.AddFile("tools/init.ps1", "$global:InitOrder += @('Dependency')");
            var root = new SimpleTestPackageContext("Root", "1.0.0");
            root.Dependencies.Add(dependency);
            root.AddFile("tools/init.ps1",
                "$global:InitOrder += @('Root'); function global:Get-InitGreeting { 'hello' }");
            root.AddFile("tools/install.ps1", "throw 'install script must not run'");
            root.AddFile("tools/uninstall.ps1", "throw 'uninstall script must not run'");
            var noScript = new SimpleTestPackageContext("NoScript", "1.0.0");
            noScript.AddFile("tools/net45/init.ps1", "throw 'framework-specific init must not run'");
            await SimpleTestPackageUtility.CreateFolderFeedPackagesConfigAsync(context.PackagesV2, root, dependency, noScript);

            var settings = Settings.LoadDefaultSettings(context.WorkingDirectory);
            settings.AddOrUpdate("config", new AddItem("repositoryPath", context.PackagesV2));
            _componentModel.Setup(model => model.GetService<ISettings>()).Returns(settings);
            _componentModel.Setup(model => model.GetService<ISourceRepositoryProvider>()).Returns(
                TestSourceRepositoryUtility.CreateSourceRepositoryProvider(new PackageSource(context.PackageSource)));
            _solutionManager.SetupGet(manager => manager.SolutionDirectory).Returns(context.SolutionRoot);
            _solutionManager.Setup(manager => manager.GetSolutionDirectoryAsync()).ReturnsAsync(context.SolutionRoot);
            SetInstalledPackages(root, dependency, noScript);
        }

        private void SetInstalledPackages(params SimpleTestPackageContext[] packages)
        {
            var project = new Mock<NuGetProject>(new Dictionary<string, object>
            {
                { NuGetProjectMetadataKeys.Name, "TestProject" },
                { NuGetProjectMetadataKeys.TargetFramework, NuGetFramework.AnyFramework }
            });
            project.Setup(project => project.GetInstalledPackagesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(packages.Select(package =>
                    new PackageReference(new PackageIdentity(package.Id, NuGetVersion.Parse(package.Version)), NuGetFramework.AnyFramework)));
            // The same package referenced by multiple projects must only be discovered once.
            _solutionManager.Setup(manager => manager.GetNuGetProjectsAsync()).ReturnsAsync(new[] { project.Object, project.Object });
        }

        private static string ScriptPath(SimpleTestPathContext context, string id) =>
            Path.Combine(context.PackagesV2, id + ".1.0.0", "tools", "init.ps1");

        private static string Escape(string path) => path.Replace("'", "''");

        public Task<object?> GetServiceAsync(Type serviceType) => Task.FromResult<object?>(null);
    }
}
