// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;

namespace NuGet.PackageManagement.Test.NuGetPackageManagerTests
{
    [Collection(nameof(NotThreadSafeResourceCollection))]
    public class PackageScriptTests
    {
        [Fact]
        public async Task ExecuteActions_UninstallPreflight_ReadsInstalledPackagesOncePerProject()
        {
            using var feature = new PowerShellScriptExecutionFeatureScope("1");
            using var source = TestDirectory.Create();
            using var solution = new TestSolutionManager();
            var provider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(new[] { new PackageSource(source.Path) });
            var manager = new NuGetPackageManager(provider, CreateSettings(source.Path), solution, new TestDeleteOnRestartManager());
            var originalProject = solution.AddNewMSBuildProject();
            var packages = new[] { CreatePackage("First", "tools/uninstall.ps1"), CreatePackage("Second", "tools/uninstall.ps1") };
            await SimpleTestPackageUtility.CreatePackagesAsync(source, packages);
            var context = new TestNuGetProjectContext
            {
                TestExecutionContext = new TestExecutionContext(null) { PackageScripts = PackageScriptBehavior.Skip }
            };
            foreach (var package in packages)
            {
                await manager.InstallPackageAsync(originalProject, package.Id, new ResolutionContext(), context,
                    provider.GetRepositories().Single(), null, CancellationToken.None);
            }
            var project = new Mock<MSBuildNuGetProject>(
                originalProject.ProjectSystem, solution.PackagesFolder, originalProject.ProjectSystem.ProjectFullPath)
            {
                CallBase = true
            };
            var configPath = Path.Combine(originalProject.ProjectSystem.ProjectFullPath, "packages.config");
            var originalConfig = File.ReadAllText(configPath);
            context.ExecutionContext.PackageScripts = PackageScriptBehavior.Fail;
            var actions = packages.Select(package => NuGetProjectAction.CreateUninstallProjectAction(package.Identity, project.Object));
            using var cache = new SourceCacheContext();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                manager.ExecuteNuGetProjectActionsAsync(project.Object, actions, context, cache, CancellationToken.None));

            project.Verify(project => project.GetInstalledPackagesAsync(It.IsAny<CancellationToken>()), Times.Once);
            foreach (var package in packages)
            {
                Assert.Contains(Path.Combine(solution.PackagesFolder, package.Id + ".1.0.0", "tools", "uninstall.ps1"), exception.Message);
            }
            Assert.Equal(originalConfig, File.ReadAllText(configPath));
        }

        [Fact]
        public async Task Uninstall_DefaultPolicy_DoesNotRemoveEarlierDependencyOrContent()
        {
            using var feature = new PowerShellScriptExecutionFeatureScope("1");
            using var source = TestDirectory.Create();
            using var solution = new TestSolutionManager();
            var provider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(new[] { new PackageSource(source.Path) });
            var manager = new NuGetPackageManager(provider, CreateSettings(source.Path), solution, new TestDeleteOnRestartManager());
            var project = solution.AddNewMSBuildProject();
            var dependency = CreatePackage("Dependency", "tools/uninstall.ps1");
            var root = CreatePackage("Root", "tools/init.ps1");
            root.Dependencies.Add(dependency);
            await SimpleTestPackageUtility.CreatePackagesAsync(source, root);
            var context = new TestNuGetProjectContext
            {
                TestExecutionContext = new TestExecutionContext(null) { PackageScripts = PackageScriptBehavior.Skip }
            };
            await manager.InstallPackageAsync(project, root.Id, new ResolutionContext(), context,
                provider.GetRepositories().Single(), null, CancellationToken.None);
            var configPath = Path.Combine(solution.SolutionDirectory, project.ProjectSystem.ProjectName, "packages.config");
            var originalConfig = File.ReadAllText(configPath);
            context.ExecutionContext.PackageScripts = PackageScriptBehavior.Fail;

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                manager.UninstallPackageAsync(project, root.Id, new UninstallationContext(removeDependencies: true, forceRemove: false),
                    context, CancellationToken.None));

            Assert.Contains("uninstall.ps1", exception.Message);
            Assert.Equal(originalConfig, File.ReadAllText(configPath));
            Assert.Equal(2, (await project.GetInstalledPackagesAsync(CancellationToken.None)).Count());
        }

        [Theory]
        [InlineData(false, "tools/uninstall.ps1")]
        [InlineData(true, "tools/uninstall.ps1")]
        public async Task UpdateOrReinstall_DefaultPolicy_PreservesInstalledVersion(bool reinstall, string script)
        {
            using var feature = new PowerShellScriptExecutionFeatureScope("1");
            using var source = TestDirectory.Create();
            using var solution = new TestSolutionManager();
            var provider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(new[] { new PackageSource(source.Path) });
            var repository = provider.GetRepositories().Single();
            var manager = new NuGetPackageManager(provider, CreateSettings(source.Path), solution, new TestDeleteOnRestartManager());
            var project = solution.AddNewMSBuildProject();
            var oldPackage = CreatePackage("Package", script);
            var newPackage = CreatePackage("Package", script);
            newPackage.Version = "2.0.0";
            await SimpleTestPackageUtility.CreatePackagesAsync(source, oldPackage, newPackage);
            var context = new TestNuGetProjectContext
            {
                TestExecutionContext = new TestExecutionContext(null) { PackageScripts = PackageScriptBehavior.Skip }
            };
            await manager.InstallPackageAsync(project, oldPackage.Identity, new ResolutionContext(), context,
                new[] { repository }, null, CancellationToken.None);
            var installedPackage = (await project.GetInstalledPackagesAsync(CancellationToken.None)).Single().PackageIdentity;
            var target = reinstall ? installedPackage : newPackage.Identity;
            var configPath = Path.Combine(project.ProjectSystem.ProjectFullPath, "packages.config");
            var originalConfig = File.ReadAllText(configPath);
            var actions = new[]
            {
                NuGetProjectAction.CreateUninstallProjectAction(installedPackage, project),
                NuGetProjectAction.CreateInstallProjectAction(target, repository, project)
            };
            context.ExecutionContext.PackageScripts = PackageScriptBehavior.Fail;
            using var cache = new SourceCacheContext();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                manager.ExecuteNuGetProjectActionsAsync(project, actions, context, cache, CancellationToken.None));

            Assert.Contains(script.Replace('/', Path.DirectorySeparatorChar), exception.Message);
            Assert.Equal(originalConfig, File.ReadAllText(configPath));
            Assert.Equal(installedPackage, (await project.GetInstalledPackagesAsync(CancellationToken.None)).Single().PackageIdentity);
            Assert.False(Directory.Exists(Path.Combine(solution.PackagesFolder, "Package.2.0.0")));
        }

        [Fact]
        public async Task Uninstall_DefaultPolicy_UsesInstalledFrameworkAfterRetargeting()
        {
            using var feature = new PowerShellScriptExecutionFeatureScope("1");
            using var source = TestDirectory.Create();
            using var solution = new TestSolutionManager();
            var provider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(new[] { new PackageSource(source.Path) });
            var manager = new NuGetPackageManager(provider, CreateSettings(source.Path), solution, new TestDeleteOnRestartManager());
            var project = solution.AddNewMSBuildProject();
            var package = CreatePackage("Package", "tools/net45/uninstall.ps1");
            await SimpleTestPackageUtility.CreatePackagesAsync(source, package);
            var context = new TestNuGetProjectContext
            {
                TestExecutionContext = new TestExecutionContext(null) { PackageScripts = PackageScriptBehavior.Skip }
            };
            await manager.InstallPackageAsync(project, package.Id, new ResolutionContext(), context,
                provider.GetRepositories().Single(), null, CancellationToken.None);
            var projectDirectory = project.ProjectSystem.ProjectFullPath;
            var originalConfig = File.ReadAllText(Path.Combine(projectDirectory, "packages.config"));
            var retargetedSystem = new TestMSBuildNuGetProjectSystem(NuGetFramework.Parse("net40"), context, projectFullPath: projectDirectory);
            var retargeted = new TestMSBuildNuGetProject(retargetedSystem, solution.PackagesFolder, projectDirectory);
            context.ExecutionContext.PackageScripts = PackageScriptBehavior.Fail;

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                manager.UninstallPackageAsync(retargeted, package.Id, new UninstallationContext(),
                    context, CancellationToken.None));

            Assert.Contains(Path.Combine("tools", "net45", "uninstall.ps1"), exception.Message);
            Assert.Equal(originalConfig, File.ReadAllText(Path.Combine(projectDirectory, "packages.config")));
            Assert.Single(await retargeted.GetInstalledPackagesAsync(CancellationToken.None));
        }

        [Theory]
        [InlineData(PackageScriptBehavior.Run, true)]
        [InlineData(PackageScriptBehavior.RunWithInit, true)]
        [InlineData(PackageScriptBehavior.Run, false)]
        [InlineData(PackageScriptBehavior.RunWithInit, false)]
        public async Task ExecuteActions_UninstallRunWithMissingFiles_PreservesAllProjects(
            PackageScriptBehavior behavior, bool missingPackage)
        {
            using var feature = new PowerShellScriptExecutionFeatureScope("1");
            using var source = TestDirectory.Create();
            using var solution = new TestSolutionManager();
            var provider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(new[] { new PackageSource(source.Path) });
            var manager = new NuGetPackageManager(provider, CreateSettings(source.Path), solution, new TestDeleteOnRestartManager());
            var projects = new[] { solution.AddNewMSBuildProject("First"), solution.AddNewMSBuildProject("Second") };
            var packages = new[] { CreatePackage("Plain", "tools/init.ps1"), CreatePackage("Scripted", "tools/uninstall.ps1") };
            await SimpleTestPackageUtility.CreatePackagesAsync(source, packages);
            var context = new TestNuGetProjectContext
            {
                TestExecutionContext = new TestExecutionContext(null) { PackageScripts = PackageScriptBehavior.Skip }
            };
            for (var i = 0; i < projects.Length; i++)
            {
                await manager.InstallPackageAsync(projects[i], packages[i].Id, new ResolutionContext(), context,
                    provider.GetRepositories().Single(), null, CancellationToken.None);
            }
            var originalConfigs = projects.Select(project => File.ReadAllText(Path.Combine(project.ProjectSystem.ProjectFullPath, "packages.config"))).ToArray();
            var installedPath = projects[1].FolderNuGetProject.GetInstalledPath(packages[1].Identity);
            if (missingPackage)
            {
                Directory.Delete(installedPath, recursive: true);
            }
            else
            {
                File.Delete(Path.Combine(installedPath, "tools", "uninstall.ps1"));
            }
            context.ExecutionContext.PackageScripts = behavior;
            var actions = projects.Select((project, i) => NuGetProjectAction.CreateUninstallProjectAction(packages[i].Identity, project));
            using var cache = new SourceCacheContext();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                manager.ExecuteNuGetProjectActionsAsync(projects, actions, context, cache, CancellationToken.None));

            Assert.Contains(packages[1].Identity.ToString(), exception.Message);
            Assert.Contains("Restore packages before uninstalling", exception.Message);
            Assert.Contains("-PackageScripts Skip", exception.Message);
            for (var i = 0; i < projects.Length; i++)
            {
                Assert.Equal(originalConfigs[i], File.ReadAllText(Path.Combine(projects[i].ProjectSystem.ProjectFullPath, "packages.config")));
                Assert.True(File.Exists(Path.Combine(projects[i].ProjectSystem.ProjectFullPath, "marker.txt")));
                Assert.Equal(packages[i].Identity, (await projects[i].GetInstalledPackagesAsync(CancellationToken.None)).Single().PackageIdentity);
            }
        }

        [Fact]
        public async Task Uninstall_SkipWithMissingPackage_RemovesPackageEntry()
        {
            using var feature = new PowerShellScriptExecutionFeatureScope("1");
            using var source = TestDirectory.Create();
            using var solution = new TestSolutionManager();
            var provider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(new[] { new PackageSource(source.Path) });
            var manager = new NuGetPackageManager(provider, CreateSettings(source.Path), solution, new TestDeleteOnRestartManager());
            var project = solution.AddNewMSBuildProject();
            var package = CreatePackage("Scripted", "tools/uninstall.ps1");
            await SimpleTestPackageUtility.CreatePackagesAsync(source, package);
            var context = new TestNuGetProjectContext
            {
                EnableLogging = true,
                TestExecutionContext = new TestExecutionContext(null) { PackageScripts = PackageScriptBehavior.Skip }
            };
            await manager.InstallPackageAsync(project, package.Id, new ResolutionContext(), context,
                provider.GetRepositories().Single(), null, CancellationToken.None);
            Directory.Delete(project.FolderNuGetProject.GetInstalledPath(package.Identity), recursive: true);

            await manager.UninstallPackageAsync(project, package.Id, new UninstallationContext(),
                context, CancellationToken.None);

            Assert.Empty(await project.GetInstalledPackagesAsync(CancellationToken.None));
            Assert.False(File.Exists(Path.Combine(project.ProjectSystem.ProjectFullPath, "packages.config")));
            Assert.True(File.Exists(Path.Combine(project.ProjectSystem.ProjectFullPath, "marker.txt")));
            Assert.Contains(context.Logs.Value, message => message.Contains("could not be cleaned up"));
        }

        private static ISettings CreateSettings(string root)
        {
            var settings = new Settings(root);
            settings.AddOrUpdate(ConfigurationConstants.Config, new AddItem("globalPackagesFolder", Path.Combine(root, "global-packages")));
            return settings;
        }

        private static SimpleTestPackageContext CreatePackage(string id, string script)
        {
            var package = new SimpleTestPackageContext(id);
            package.AddFile("lib/net45/_._");
            package.AddFile("content/marker.txt", id);
            package.AddFile(script);
            return package;
        }
    }
}
