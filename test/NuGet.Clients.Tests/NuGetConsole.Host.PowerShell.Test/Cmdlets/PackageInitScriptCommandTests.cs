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
    public class PackageInitScriptCommandTests : IAsyncServiceProvider, IDisposable
    {
        private const string FeatureEnvironmentVariable = "NUGET_CONTROL_POWERSHELL_SCRIPT_EXECUTION";
        private readonly string? _originalFeatureEnvironmentVariable;
        private readonly Mock<IComponentModel> _componentModel = new Mock<IComponentModel>();
        private readonly Mock<IVsSolutionManager> _solutionManager = new Mock<IVsSolutionManager>();

        public PackageInitScriptCommandTests(GlobalServiceProvider globalServiceProvider)
        {
            _originalFeatureEnvironmentVariable = Environment.GetEnvironmentVariable(FeatureEnvironmentVariable);
            Environment.SetEnvironmentVariable(FeatureEnvironmentVariable, "1");
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

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(FeatureEnvironmentVariable, _originalFeatureEnvironmentVariable);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("0")]
        public async Task GetPackageInitScript_FeatureDisabled_ThrowsAsync(string? featureValue)
        {
            Environment.SetEnvironmentVariable(FeatureEnvironmentVariable, featureValue);
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);

            var exception = Assert.Throws<CmdletInvocationException>(() => fixture.InvokeScript("Get-PackageInitScript"));

            exception.ErrorRecord.FullyQualifiedErrorId.Should().StartWith("NuGetPackageInitScriptFeatureDisabled");
            fixture.InvokeScript("$null -eq $global:InitOrder").Single().BaseObject.Should().Be(true);
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
        [InlineData("root", "Root")]
        [InlineData("Dependency@1.0", "Dependency")]
        public async Task GetPackageInitScript_SelectsPackagesAsync(string selector, string expected)
        {
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);

            var results = fixture.InvokeScript($"Get-PackageInitScript -Packages '{selector}'");

            fixture.Errors.Should().BeEmpty();
            results.Select(result => ((PowerShellPackageInitScript)result.BaseObject).Id).Should().Equal(expected);
            fixture.InvokeScript("$null -eq $global:InitOrder").Single().BaseObject.Should().Be(true);
        }

        [Fact]
        public async Task GetPackageInitScript_SelectsPackagesInDependencyOrderWithoutDuplicatesAsync()
        {
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);

            var results = fixture.InvokeScript("Get-PackageInitScript -Packages Root,Dependency,root");

            fixture.Errors.Should().BeEmpty();
            results.Select(result => ((PowerShellPackageInitScript)result.BaseObject).Id).Should().Equal("Dependency", "Root");
        }

        [Theory]
        [InlineData("Missing")]
        [InlineData("Root@2.0.0")]
        public async Task GetPackageInitScript_UnknownPackageProducesErrorAsync(string selector)
        {
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);

            fixture.InvokeScript($"Get-PackageInitScript -Packages '{selector}'").Should().BeEmpty();

            fixture.Errors.Should().ContainSingle().Which.FullyQualifiedErrorId.Should().StartWith("NuGetPackageInitScriptNotFound");
        }

        [Theory]
        [InlineData("Root@")]
        [InlineData("Root@invalid")]
        [InlineData("@1.0.0")]
        public async Task GetPackageInitScript_InvalidSelectorPreservesParserExceptionAsync(string selector)
        {
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);
            var expected = Assert.Throws<ArgumentException>(() => PackageIdentity.ParseAtSeparated(selector));

            fixture.InvokeScript($"Get-PackageInitScript -Packages '{selector}'");

            var error = fixture.Errors.Should().ContainSingle().Which;
            error.FullyQualifiedErrorId.Should().StartWith("NuGetPackageInitScriptNotFound");
            error.Exception.InnerException.Should().BeOfType<ArgumentException>()
                .Which.Message.Should().Be(expected.Message);
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
            fixture.InvokeScript("$null -eq $global:InitOrder").Single().BaseObject.Should().Be(true);
        }

        [Fact]
        public async Task GetPackageInitScript_AmbiguousVersionsRequireVersionSelectorAsync()
        {
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            var first = new SimpleTestPackageContext("Root", "1.0.0");
            var second = new SimpleTestPackageContext("Root", "2.0.0");
            second.AddFile("tools/init.ps1", "throw 'must not run'");
            await SimpleTestPackageUtility.CreateFolderFeedPackagesConfigAsync(context.PackagesV2, second);
            SetInstalledPackages(first, second, new SimpleTestPackageContext("Dependency", "1.0.0"));
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);

            fixture.InvokeScript("Get-PackageInitScript -Packages Root").Should().BeEmpty();

            fixture.Errors.Should().ContainSingle().Which.FullyQualifiedErrorId.Should().StartWith("NuGetAmbiguousPackageInitScript");
            var results = fixture.InvokeScript("Get-PackageInitScript -Packages Root@2.0.0");
            fixture.Errors.Should().BeEmpty();
            var script = (PowerShellPackageInitScript)results.Single().BaseObject;
            script.Version.Should().Be(NuGetVersion.Parse("2.0.0"));
            fixture.InvokeScript("$null -eq $global:InitOrder").Single().BaseObject.Should().Be(true);
        }

        [Fact]
        public async Task GetPackageInitScript_NoInstalledScriptsIsNoOpAsync()
        {
            using var context = new SimpleTestPathContext();
            await SetupPackagesAsync(context);
            SetInstalledPackages();
            using var fixture = new CmdletRunspaceFixture(context.PackageSource);

            fixture.InvokeScript("Get-PackageInitScript").Should().BeEmpty();

            fixture.Errors.Should().BeEmpty();
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
            root.AddFile("tools/init.ps1", "$global:InitOrder += @('Root')");
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

        public Task<object?> GetServiceAsync(Type serviceType) => Task.FromResult<object?>(null);
    }
}
